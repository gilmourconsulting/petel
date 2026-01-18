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
                return await response.Content.ReadFromJsonAsync<T>();
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
                
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized request to {Endpoint}", endpoint);
                    return default;
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<T>();
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
                return await response.Content.ReadFromJsonAsync<TResponse>();
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
                return await response.Content.ReadFromJsonAsync<TResponse>();
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
                return await response.Content.ReadFromJsonAsync<T>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "POST multipart request failed for {Endpoint}", endpoint);
                throw;
            }
        }
    }
}
