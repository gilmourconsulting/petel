using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Petel.BlazorCore.Models;

namespace Petel.BlazorCore.Extensions
{
    public static class DocumentProxyExtensions
    {
        public static void MapDocumentProxy(
            this WebApplication app,
            string pattern = "/api/documents/{documentId}/proxy")
        {
            app.MapGet(pattern, async (
                long documentId,
                HttpContext httpContext,
                IHttpClientFactory httpClientFactory,
                IOptions<ApiSettings> apiSettings,
                ILogger<WebApplication> logger) =>
            {
                try
                {
                    logger.LogInformation("📥 Document proxy request for ID: {DocumentId}", documentId);

                    // Extract Authorization header from browser request
                    if (!httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader) ||
                        string.IsNullOrEmpty(authHeader))
                    {
                        logger.LogWarning("⚠️ No authorization header in proxy request for document {DocumentId}", documentId);
                        return Results.Unauthorized();
                    }

                    // Create HTTP client and forward browser's token to API
                    var client = httpClientFactory.CreateClient("PetelApi");
                    client.DefaultRequestHeaders.Add("Authorization", authHeader.ToString());

                    var apiUrl = $"{apiSettings.Value.BaseUrl}/Documents/{documentId}/download";
                    logger.LogDebug("Proxying request to: {ApiUrl}", apiUrl);

                    var apiResponse = await client.GetAsync(apiUrl);

                    if (!apiResponse.IsSuccessStatusCode)
                    {
                        logger.LogWarning("⚠️ API returned {StatusCode} for document {DocumentId}",
                            apiResponse.StatusCode, documentId);

                        if (apiResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                            return Results.Unauthorized();

                        if (apiResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                            return Results.NotFound(new { error = "מסמך לא נמצא" });

                        return Results.Problem($"שגיאה בטעינת המסמך: {apiResponse.StatusCode}");
                    }

                    var content = await apiResponse.Content.ReadAsByteArrayAsync();
                    var contentType = apiResponse.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";

                    var fileName = $"document_{documentId}";
                    if (apiResponse.Content.Headers.ContentDisposition?.FileName != null)
                    {
                        fileName = apiResponse.Content.Headers.ContentDisposition.FileName.Trim('"');
                    }

                    logger.LogInformation("✅ Returning document {DocumentId}, size: {Size} bytes, type: {ContentType}",
                        documentId, content.Length, contentType);

                    return Results.File(content, contentType, fileName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "❌ Error proxying document {DocumentId}", documentId);
                    return Results.Problem("שגיאה בטעינת המסמך");
                }
            }).DisableAntiforgery();
        }
    }
}
