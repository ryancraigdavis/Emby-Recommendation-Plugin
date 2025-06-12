using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using Emby.Recommendation.Plugin.Models;
using Microsoft.Extensions.Logging;

namespace Emby.Recommendation.Plugin.Services
{
    public interface ISyncService
    {
        Task<bool> SyncAllUsersAsync();
        Task<bool> SyncUserAsync(Guid userId);
        Task<bool> SyncContentLibraryAsync();
        Task<DateTime?> GetLastSyncTimeAsync();
        Task UpdateLastSyncTimeAsync();
    }

    public class SyncService : ISyncService
    {
        private readonly IEmbyDataService _embyDataService;
        private readonly IRecommendationApiService _apiService;
        private readonly IKafkaEventService _kafkaService;
        private readonly ILogger<SyncService> _logger;

        public SyncService(
            IEmbyDataService embyDataService,
            IRecommendationApiService apiService,
            IKafkaEventService kafkaService,
            ILogger<SyncService> logger)
        {
            _embyDataService = embyDataService ?? throw new ArgumentNullException(nameof(embyDataService));
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _kafkaService = kafkaService ?? throw new ArgumentNullException(nameof(kafkaService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> SyncAllUsersAsync()
        {
            try
            {
                _logger.LogInformation("Starting sync of all users");
                
                var users = await _embyDataService.GetUsersAsync();
                var successCount = 0;
                var totalCount = users.Count();

                foreach (var user in users)
                {
                    try
                    {
                        var success = await SyncUserAsync(user.Id);
                        if (success) successCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error syncing user {UserId}", user.Id);
                    }
                }

                _logger.LogInformation("Completed user sync: {SuccessCount}/{TotalCount} users synced successfully", 
                    successCount, totalCount);

                return successCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sync all users operation");
                return false;
            }
        }

        public async Task<bool> SyncUserAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Starting sync for user {UserId}", userId);

                var user = await _embyDataService.GetUserAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User {UserId} not found for sync", userId);
                    return false;
                }

                // Get user's watch history
                var recentlyWatched = await _embyDataService.GetRecentlyWatchedAsync(userId, 500);
                var userLibrary = await _embyDataService.GetUserLibraryAsync(userId);

                var watchData = new List<UserWatchData>();
                var ratings = new List<UserRating>();

                foreach (var item in recentlyWatched)
                {
                    var userData = await _embyDataService.GetUserItemDataAsync(userId, item.Id);
                    if (userData == null) continue;

                    var watchItem = new UserWatchData
                    {
                        ItemId = item.Id,
                        ItemName = item.Name,
                        ItemType = item.GetType().Name,
                        TmdbId = GetTmdbId(item),
                        TvdbId = GetTvdbId(item),
                        LastPlayedDate = userData.LastPlayedDate,
                        PlaybackPositionTicks = userData.PlaybackPositionTicks,
                        PlayCount = userData.PlayCount,
                        IsFavorite = userData.IsFavorite,
                        UserRating = userData.Rating
                    };

                    watchData.Add(watchItem);

                    if (userData.Rating.HasValue)
                    {
                        ratings.Add(new UserRating
                        {
                            ItemId = item.Id,
                            Rating = userData.Rating.Value,
                            RatedAt = userData.LastPlayedDate ?? DateTime.UtcNow
                        });
                    }
                }

                var userSyncData = new UserSyncData
                {
                    UserId = userId,
                    UserName = user.Username,
                    WatchHistory = watchData,
                    Ratings = ratings,
                    LastSyncTime = DateTime.UtcNow
                };

                var success = await _apiService.SyncUserDataAsync(userSyncData);
                if (success)
                {
                    await _kafkaService.SendUserEventAsync("user_synced", userId, new { ItemCount = watchData.Count });
                    _logger.LogInformation("Successfully synced user {UserId} with {ItemCount} items", 
                        userId, watchData.Count);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> SyncContentLibraryAsync()
        {
            try
            {
                _logger.LogInformation("Starting content library sync");

                // Get all movies and series from the library
                var allContent = await _embyDataService.GetUserLibraryAsync(Guid.Empty);
                var syncedCount = 0;

                foreach (var item in allContent)
                {
                    try
                    {
                        var contentMetadata = new ContentMetadata
                        {
                            ItemId = item.Id,
                            Name = item.Name,
                            ItemType = item.GetType().Name,
                            TmdbId = GetTmdbId(item),
                            TvdbId = GetTvdbId(item),
                            ImdbId = item.GetProviderId("Imdb"),
                            PremiereDate = item.PremiereDate,
                            Genres = item.Genres?.ToList() ?? new List<string>(),
                            Tags = item.Tags?.ToList() ?? new List<string>(),
                            Studios = item.Studios?.ToList() ?? new List<string>(),
                            Overview = item.Overview,
                            CommunityRating = item.CommunityRating,
                            OfficialRating = item.OfficialRating,
                            RunTimeTicks = item.RunTimeTicks,
                            DateCreated = item.DateCreated,
                            DateModified = item.DateModified
                        };

                        var success = await _apiService.SyncContentMetadataAsync(contentMetadata);
                        if (success)
                        {
                            syncedCount++;
                            await _kafkaService.SendContentEventAsync("content_synced", item.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error syncing content item {ItemId}: {ItemName}", 
                            item.Id, item.Name);
                    }
                }

                _logger.LogInformation("Content library sync completed: {SyncedCount} items synced", syncedCount);
                return syncedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during content library sync");
                return false;
            }
        }

        public async Task<DateTime?> GetLastSyncTimeAsync()
        {
            var config = Plugin.Instance?.Configuration;
            return await Task.FromResult(config?.LastSyncTime);
        }

        public async Task UpdateLastSyncTimeAsync()
        {
            var config = Plugin.Instance?.Configuration;
            if (config != null)
            {
                config.LastSyncTime = DateTime.UtcNow;
                Plugin.Instance.SaveConfiguration();
            }
            await Task.CompletedTask;
        }

        private int? GetTmdbId(BaseItem item)
        {
            var providerId = item.GetProviderId("Tmdb");
            return int.TryParse(providerId, out var tmdbId) ? tmdbId : null;
        }

        private int? GetTvdbId(BaseItem item)
        {
            var providerId = item.GetProviderId("Tvdb");
            return int.TryParse(providerId, out var tvdbId) ? tvdbId : null;
        }
    }
}