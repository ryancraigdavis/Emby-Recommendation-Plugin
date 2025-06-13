using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Emby.Recommendation.Plugin.Configuration;
using Emby.Recommendation.Plugin.Models;
using Microsoft.Extensions.Logging;

namespace Emby.Recommendation.Plugin.Services
{
    public interface IRecommendationApiService
    {
        Task<bool> SyncUserDataAsync(UserSyncData userData);
        Task<bool> SyncContentMetadataAsync(ContentMetadata contentData);
        Task<IEnumerable<RecommendationResult>> GetRecommendationsAsync(Guid userId, int count = 20);
        Task<bool> SendWatchEventAsync(WatchEvent watchEvent);
        Task<bool> TestConnectionAsync();
    }

    public class RecommendationApiService : IRecommendationApiService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<RecommendationApiService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        private bool _disposed = false;

        public RecommendationApiService(IHttpClientFactory httpClientFactory, ILogger<RecommendationApiService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("RecommendationApi");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            ConfigureHttpClient();
        }

        private void ConfigureHttpClient()
        {
            var config = Plugin.Instance?.Configuration;
            if (config == null) return;

            _httpClient.BaseAddress = new Uri(config.MicroserviceBaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(config.HttpTimeoutSeconds);
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", config.ApiKey);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Emby-Recommendation-Plugin/1.0");
        }

        public async Task<bool> SyncUserDataAsync(UserSyncData userData)
        {
            try
            {
                var json = JsonSerializer.Serialize(userData, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/sync/user", content);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully synced user data for user {UserId}", userData.UserId);
                    return true;
                }

                _logger.LogWarning("Failed to sync user data for user {UserId}. Status: {StatusCode}", 
                    userData.UserId, response.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing user data for user {UserId}", userData.UserId);
                return false;
            }
        }

        public async Task<bool> SyncContentMetadataAsync(ContentMetadata contentData)
        {
            try
            {
                var json = JsonSerializer.Serialize(contentData, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/sync/content", content);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully synced content metadata for item {ItemId}", contentData.ItemId);
                    return true;
                }

                _logger.LogWarning("Failed to sync content metadata for item {ItemId}. Status: {StatusCode}", 
                    contentData.ItemId, response.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing content metadata for item {ItemId}", contentData.ItemId);
                return false;
            }
        }

        public async Task<IEnumerable<RecommendationResult>> GetRecommendationsAsync(Guid userId, int count = 20)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/recommendations/{userId}?count={count}");
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var recommendations = JsonSerializer.Deserialize<IEnumerable<RecommendationResult>>(json, _jsonOptions);
                    
                    _logger.LogInformation("Retrieved {Count} recommendations for user {UserId}", 
                        recommendations?.Count() ?? 0, userId);
                    
                    return recommendations ?? new List<RecommendationResult>();
                }

                _logger.LogWarning("Failed to get recommendations for user {UserId}. Status: {StatusCode}", 
                    userId, response.StatusCode);
                return new List<RecommendationResult>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recommendations for user {UserId}", userId);
                return new List<RecommendationResult>();
            }
        }

        public async Task<bool> SendWatchEventAsync(WatchEvent watchEvent)
        {
            try
            {
                var json = JsonSerializer.Serialize(watchEvent, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/events/watch", content);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Successfully sent watch event for user {UserId}, item {ItemId}", 
                        watchEvent.UserId, watchEvent.ItemId);
                    return true;
                }

                _logger.LogWarning("Failed to send watch event. Status: {StatusCode}", response.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending watch event for user {UserId}, item {ItemId}", 
                    watchEvent.UserId, watchEvent.ItemId);
                return false;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/health");
                var isHealthy = response.IsSuccessStatusCode;
                
                _logger.LogInformation("Connection test {Result}", isHealthy ? "successful" : "failed");
                return isHealthy;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection to recommendation service");
                return false;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
        }
    }
}