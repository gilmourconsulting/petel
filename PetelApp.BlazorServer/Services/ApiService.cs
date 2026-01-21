using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PetelApp.BlazorServer.Models;

namespace PetelApp.BlazorServer.Services
{
    /// <summary>
    /// Centralized HTTP client service for all API calls
    /// Automatically includes JWT token in Authorization header
    /// </summary>
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly TokenService _tokenService;
        private readonly ILogger<ApiService> _logger;
        private readonly string _baseUrl;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiService(
            HttpClient httpClient,
            TokenService tokenService,
            IOptions<ApiSettings> apiSettings,
            ILogger<ApiService> logger)
        {
            _httpClient = httpClient;
            _tokenService = tokenService;
            _logger = logger;
            _baseUrl = apiSettings.Value.BaseUrl;

            // Set default timeout
            _httpClient.Timeout = TimeSpan.FromSeconds(apiSettings.Value.Timeout);
            
            // Configure JSON options to handle camelCase from backend API
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        private async Task<HttpClient> GetAuthorizedClientAsync()
        {
            try
            {
                var token = await _tokenService.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", token);
                }
            }
            catch (Exception ex)
            {
                // Token retrieval failed (e.g., during prerender or no circuit)
                _logger.LogDebug(ex, "Could not retrieve token, proceeding without auth header");
            }
            return _httpClient;
        }

        /// <summary>
        /// GET request without authentication (for public endpoints like login)
        /// </summary>
        public async Task<T?> GetPublicAsync<T>(string endpoint)
        {
            try
            {
                var url = $"{_baseUrl}/{endpoint}";
                
                _logger.LogDebug("Public GET request to {Url}", url);
                
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
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
                    _logger.LogWarning("Unauthorized request to {Endpoint}", endpoint);
                    return default;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("GET {Endpoint} failed with {StatusCode}: {Error}", endpoint, response.StatusCode, errorContent);
                }

                response.EnsureSuccessStatusCode();
                
                // Read raw content for logging
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("GET {Endpoint} response: {Content}", endpoint, content);
                
                // Deserialize with custom options
                var result = JsonSerializer.Deserialize<T>(content, _jsonOptions);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GET request failed for {Endpoint}", endpoint);
                throw;
            }
        }

        public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            try
            {
                var client = await GetAuthorizedClientAsync();
                var url = $"{_baseUrl}/{endpoint}";
                
                _logger.LogDebug("POST request to {Url}", url);
                
                var response = await client.PostAsJsonAsync(url, data);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized request to {Endpoint}", endpoint);
                    return default;
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "POST request failed for {Endpoint}", endpoint);
                throw;
            }
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
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
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
                return response.IsSuccessStatusCode;
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
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
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
                    _logger.LogWarning("Unauthorized multipart request to {Endpoint}", endpoint);
                    return default;
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "POST multipart request failed for {Endpoint}", endpoint);
                throw;
            }
        }

        /// <summary>
        /// GET request with custom token (for OTP setup flow with temp token)
        /// </summary>
        public async Task<T?> GetAsync<T>(string endpoint, string? customToken)
        {
            try
            {
                var url = $"{_baseUrl}/{endpoint}";
                
                _logger.LogDebug("GET request with custom token to {Url}", url);
                
                // Create new request with custom token
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrEmpty(customToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customToken);
                }
                
                var response = await _httpClient.SendAsync(request);
                
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
                var url = $"{_baseUrl}/{endpoint}";
                
                _logger.LogDebug("POST request with custom token to {Url}", url);
                
                // Create new request with custom token
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                if (!string.IsNullOrEmpty(customToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customToken);
                }
                request.Content = JsonContent.Create(data);
                
                var response = await _httpClient.SendAsync(request);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized request to {Endpoint}", endpoint);
                    return default;
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "POST request with custom token failed for {Endpoint}", endpoint);
                throw;
            }
        }
    }
}
