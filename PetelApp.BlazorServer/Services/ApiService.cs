using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PetelApp.BlazorServer.Models;

namespace PetelApp.BlazorServer.Services
{
    /// <summary>
    /// Response model for file downloads
    /// </summary>
    public class FileDownloadResponse
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = "application/octet-stream";
        public Dictionary<string, string> Headers { get; set; } = new();
    }

    /// <summary>
    /// Exception thrown when HTTP request returns non-success status code
    /// </summary>
    public class HttpStatusException : Exception
    {
        public System.Net.HttpStatusCode StatusCode { get; }
        public string? ResponseContent { get; }

        public HttpStatusException(
            System.Net.HttpStatusCode statusCode,
            string message,
            string? responseContent = null)
            : base(message)
        {
            StatusCode = statusCode;
            ResponseContent = responseContent;
        }
    }

    /// <summary>
    /// Centralized HTTP client service for all API calls
    /// Automatically includes JWT token in Authorization header
    /// </summary>
    public class ApiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TokenService _tokenService;
        private readonly ILogger<ApiService> _logger;
        private readonly string _baseUrl;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiService(
            IHttpClientFactory httpClientFactory,
            TokenService tokenService,
            IOptions<ApiSettings> apiSettings,
            ILogger<ApiService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _tokenService = tokenService;
            _logger = logger;
            _baseUrl = apiSettings.Value.BaseUrl;
            
            // Configure JSON options to handle camelCase from backend API
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        private async Task<HttpClient> GetAuthorizedClientAsync()
        {
            var client = _httpClientFactory.CreateClient("PetelApi");
            
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", token);
                }
            }
            catch (Exception ex)
            {
                // Token retrieval failed (e.g., during prerender or no circuit)
                _logger.LogDebug(ex, "Could not retrieve token, proceeding without auth header");
            }
            return client;
        }

        /// <summary>
        /// GET request without authentication (for public endpoints like login)
        /// </summary>
        public async Task<T?> GetPublicAsync<T>(string endpoint)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("PetelApi");
                var url = $"{_baseUrl}/{endpoint}";
                
                _logger.LogDebug("Public GET request to {Url}", url);
                
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogWarning(ex, "HttpClient disposed during request to {Endpoint}", endpoint);
                return default;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogDebug(ex, "Request cancelled for {Endpoint}", endpoint);
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Public GET request failed for {Endpoint}", endpoint);
                throw;
            }
        }

        public async Task<T?> GetAsync<T>(string endpoint)
        {
            try
            {
                var client = await GetAuthorizedClientAsync();
                var url = $"{_baseUrl}/{endpoint}";
                
                _logger.LogDebug("GET request to {Url}", url);
                
                var response = await client.GetAsync(url);
                
                _logger.LogInformation("GET {Endpoint} returned status {StatusCode}", endpoint, response.StatusCode);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized request to {Endpoint} - invalid or missing token", endpoint);
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpStatusException(
                        System.Net.HttpStatusCode.Unauthorized,
                        "Authentication required",
                        errorContent
                    );
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("GET {Endpoint} failed with {StatusCode}: {Error}", endpoint, response.StatusCode, errorContent);
                    
                    // Handle rate limiting specifically
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        throw new HttpRequestException($"Rate limit exceeded. Please wait before retrying. Details: {errorContent}");
                    }
                }

                response.EnsureSuccessStatusCode();
                
                // Read raw content for logging
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("GET {Endpoint} response: {Content}", endpoint, content);
                
                // Deserialize with custom options
                var result = JsonSerializer.Deserialize<T>(content, _jsonOptions);
                return result;
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogWarning(ex, "HttpClient disposed during GET request to {Endpoint}", endpoint);
                return default;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogDebug(ex, "GET request cancelled for {Endpoint}", endpoint);
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GET request failed for {Endpoint}", endpoint);
                throw;
            }
        }

        /// <summary>
        /// GET request for downloading files (returns raw byte array with headers)
        /// </summary>
        public async Task<FileDownloadResponse?> GetFileAsync(string endpoint)
        {
            try
            {
                var client = await GetAuthorizedClientAsync();
                var url = $"{_baseUrl}/{endpoint}";
                
                _logger.LogDebug("GET file request to {Url}", url);
                
                var response = await client.GetAsync(url);
                
                _logger.LogInformation("GET file {Endpoint} returned status {StatusCode}", endpoint, response.StatusCode);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized file request to {Endpoint} - invalid or missing token", endpoint);
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("GET file {Endpoint} failed with {StatusCode}: {Error}", endpoint, response.StatusCode, errorContent);
                    return null;
                }

                var content = await response.Content.ReadAsByteArrayAsync();
                var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
                
                // Extract headers
                var headers = new Dictionary<string, string>();
                foreach (var header in response.Headers)
                {
                    headers[header.Key] = string.Join(", ", header.Value);
                }
                foreach (var header in response.Content.Headers)
                {
                    headers[header.Key] = string.Join(", ", header.Value);
                }

                _logger.LogDebug("Downloaded file from {Endpoint}: {Size} bytes, ContentType: {ContentType}", 
                    endpoint, content.Length, contentType);

                return new FileDownloadResponse
                {
                    Content = content,
                    ContentType = contentType,
                    Headers = headers
                };
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogWarning(ex, "HttpClient disposed during GET file request to {Endpoint}", endpoint);
                return null;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogDebug(ex, "GET file request cancelled for {Endpoint}", endpoint);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GET file request failed for {Endpoint}", endpoint);
                return null;
            }
        }

        public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            return await PostAsync<TRequest, TResponse>(endpoint, data, 0);
        }

        public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data, int maxRetries = 0)
        {
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var client = await GetAuthorizedClientAsync();
                    var url = $"{_baseUrl}/{endpoint}";
                    
                    _logger.LogDebug("POST request to {Url} (attempt {Attempt})", url, attempt + 1);
                    
                    var response = await client.PostAsJsonAsync(url, data);
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        _logger.LogWarning("Unauthorized request to {Endpoint} - invalid or missing token", endpoint);
                        var errorContent = await response.Content.ReadAsStringAsync();
                        throw new HttpStatusException(
                            System.Net.HttpStatusCode.Unauthorized,
                            "Authentication required",
                            errorContent
                        );
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("POST request failed for {Endpoint}: {StatusCode} - {ErrorContent}", 
                            endpoint, response.StatusCode, errorContent);
                        
                        // Handle rate limiting with retry
                        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests && attempt < maxRetries)
                        {
                            var delayMs = (int)Math.Pow(2, attempt) * 1000; // Exponential backoff
                            _logger.LogWarning("Rate limited on {Endpoint}, retrying in {Delay}ms (attempt {Attempt})", 
                                endpoint, delayMs, attempt + 1);
                            await Task.Delay(delayMs);
                            continue;
                        }
                        
                        // Handle rate limiting specifically
                        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                        {
                            throw new HttpRequestException($"Rate limit exceeded. Please wait before retrying. Details: {errorContent}");
                        }
                        
                        throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}). Details: {errorContent}");
                    }

                    return await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
                }
                catch (ObjectDisposedException ex) when (attempt == maxRetries)
                {
                    _logger.LogWarning(ex, "HttpClient disposed during POST request to {Endpoint}", endpoint);
                    return default;
                }
                catch (TaskCanceledException ex) when (attempt == maxRetries)
                {
                    _logger.LogDebug(ex, "POST request cancelled for {Endpoint}", endpoint);
                    return default;
                }
                catch (HttpRequestException) when (attempt == maxRetries)
                {
                    // Re-throw on final attempt
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "POST request failed for {Endpoint} (attempt {Attempt})", endpoint, attempt + 1);
                    if (attempt == maxRetries) throw;
                }
            }
            
            return default; // Should never reach here
        }

        public async Task<HttpResponseMessage> PostAsync<TRequest>(string endpoint, TRequest data)
        {
            try
            {
                var client = await GetAuthorizedClientAsync();
                var url = $"{_baseUrl}/{endpoint}";
                
                _logger.LogDebug("POST request to {Url}", url);
                
                return await client.PostAsJsonAsync(url, data);
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogWarning(ex, "HttpClient disposed during POST request to {Endpoint}", endpoint);
                throw;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogDebug(ex, "POST request cancelled for {Endpoint}", endpoint);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "POST request failed for {Endpoint}", endpoint);
                throw;
            }
        }

        public async Task<TResponse?> PutAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            try
            {
                var client = await GetAuthorizedClientAsync();
                var url = $"{_baseUrl}/{endpoint}";
                
                _logger.LogDebug("PUT request to {Url}", url);
                
                var response = await client.PutAsJsonAsync(url, data);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized request to {Endpoint} - invalid or missing token", endpoint);
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpStatusException(
                        System.Net.HttpStatusCode.Unauthorized,
                        "Authentication required",
                        errorContent
                    );
                }
                
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogWarning(ex, "HttpClient disposed during PUT request to {Endpoint}", endpoint);
                return default;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogDebug(ex, "PUT request cancelled for {Endpoint}", endpoint);
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PUT request failed for {Endpoint}", endpoint);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(string endpoint)
        {
            try
            {
                var client = await GetAuthorizedClientAsync();
                var url = $"{_baseUrl}/{endpoint}";
                
                _logger.LogDebug("DELETE request to {Url}", url);
                
                var response = await client.DeleteAsync(url);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized request to {Endpoint} - invalid or missing token", endpoint);
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpStatusException(
                        System.Net.HttpStatusCode.Unauthorized,
                        "Authentication required",
                        errorContent
                    );
                }
                
                return response.IsSuccessStatusCode;
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogWarning(ex, "HttpClient disposed during DELETE request to {Endpoint}", endpoint);
                return false;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogDebug(ex, "DELETE request cancelled for {Endpoint}", endpoint);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DELETE request failed for {Endpoint}", endpoint);
                throw;
            }
        }

        /// <summary>
        /// DELETE request with response deserialization
        /// </summary>
        public async Task<T?> DeleteAsync<T>(string endpoint)
        {
            try
            {
                var client = await GetAuthorizedClientAsync();
                var url = $"{_baseUrl}/{endpoint}";
                
                _logger.LogDebug("DELETE request to {Url}", url);
                
                var response = await client.DeleteAsync(url);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized request to {Endpoint} - invalid or missing token", endpoint);
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpStatusException(
                        System.Net.HttpStatusCode.Unauthorized,
                        "Authentication required",
                        errorContent
                    );
                }
                
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogWarning(ex, "HttpClient disposed during DELETE request to {Endpoint}", endpoint);
                return default;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogDebug(ex, "DELETE request cancelled for {Endpoint}", endpoint);
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DELETE request with response failed for {Endpoint}", endpoint);
                throw;
            }
        }

        /// <summary>
        /// POST multipart/form-data request (for file uploads)
        /// </summary>
        public async Task<T?> PostMultipartAsync<T>(string endpoint, MultipartFormDataContent content)
        {
            try
            {
                var client = await GetAuthorizedClientAsync();
                var url = $"{_baseUrl}/{endpoint}";
                
                _logger.LogDebug("POST multipart request to {Url}", url);
                
                var response = await client.PostAsync(url, content);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized multipart request to {Endpoint} - invalid or missing token", endpoint);
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpStatusException(
                        System.Net.HttpStatusCode.Unauthorized,
                        "Authentication required",
                        errorContent
                    );
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogWarning(ex, "HttpClient disposed during multipart POST to {Endpoint}", endpoint);
                return default;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogDebug(ex, "Multipart POST cancelled for {Endpoint}", endpoint);
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "POST multipart request failed for {Endpoint}", endpoint);
                throw;
            }
        }


        public async Task<T?> GetAsync<T>(string endpoint, string? customToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("PetelApi");
                var url = $"{_baseUrl}/{endpoint}";
                
                _logger.LogDebug("GET request with custom token to {Url}", url);
                
                // Create new request with custom token
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrEmpty(customToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customToken);
                }
                
                var response = await client.SendAsync(request);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized request to {Endpoint}", endpoint);
                    return default;
                }

                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("GET {Endpoint} response: {Content}", endpoint, content);
                
                var result = JsonSerializer.Deserialize<T>(content, _jsonOptions);
                return result;
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogWarning(ex, "HttpClient disposed during GET with custom token to {Endpoint}", endpoint);
                return default;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogDebug(ex, "GET with custom token cancelled for {Endpoint}", endpoint);
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GET request with custom token failed for {Endpoint}", endpoint);
                throw;
            }
        }

        /// <summary>
        /// POST request with custom token (for OTP setup flow with temp token)
        /// </summary>
        public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data, string? customToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("PetelApi");
                var url = $"{_baseUrl}/{endpoint}";
                
                _logger.LogDebug("POST request with custom token to {Url}", url);
                
                // Create new request with custom token
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                if (!string.IsNullOrEmpty(customToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customToken);
                }
                request.Content = JsonContent.Create(data);
                
                var response = await client.SendAsync(request);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized request to {Endpoint}", endpoint);
                    return default;
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogWarning(ex, "HttpClient disposed during POST with custom token to {Endpoint}", endpoint);
                return default;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogDebug(ex, "POST with custom token cancelled for {Endpoint}", endpoint);
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "POST request with custom token failed for {Endpoint}", endpoint);
                throw;
            }
        }

        /// <summary>
        /// POST request with custom token (for OTP setup flow with temp token)
        /// </summary>
        public async Task<TResponse?> PostWithTokenAsync<TRequest, TResponse>(string endpoint, TRequest data, string? customToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("PetelApi");
                var url = $"{_baseUrl}/{endpoint}";
                
                _logger.LogDebug("POST request with custom token to {Url}", url);
                
                // Create new request with custom token
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                if (!string.IsNullOrEmpty(customToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customToken);
                }
                
                // Add JSON content
                var jsonContent = JsonSerializer.Serialize(data, _jsonOptions);
                request.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                
                var response = await client.SendAsync(request);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized request to {Endpoint}", endpoint);
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpStatusException(
                        System.Net.HttpStatusCode.Unauthorized,
                        "Authentication required",
                        errorContent
                    );
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("POST request with custom token failed for {Endpoint}: {StatusCode} - {ErrorContent}", 
                        endpoint, response.StatusCode, errorContent);
                    
                    // Handle rate limiting specifically
                    if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        throw new HttpRequestException($"Rate limit exceeded. Please wait before retrying. Details: {errorContent}");
                    }
                    
                    throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}). Details: {errorContent}");
                }

                return await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogWarning(ex, "HttpClient disposed during POST with token to {Endpoint}", endpoint);
                return default;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogDebug(ex, "POST with token cancelled for {Endpoint}", endpoint);
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "POST request with custom token failed for {Endpoint}", endpoint);
                throw;
            }
        }
    }
}
