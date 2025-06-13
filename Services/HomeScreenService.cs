using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Querying;
using Emby.Recommendation.Plugin.Models;
using Microsoft.Extensions.Logging;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Entities;

namespace Emby.Recommendation.Plugin.Services
{
    public interface IHomeScreenService
    {
        Task<IEnumerable<BaseItem>> GetRecommendedItemsForHomeScreenAsync(Guid userId, int limit = 20);
        Task<IEnumerable<BaseItem>> GetTrendingItemsAsync(Guid userId, int limit = 15);
        Task<IEnumerable<BaseItem>> GetSimilarToFavoritesAsync(Guid userId, int limit = 15);
        Task<IEnumerable<BaseItem>> GetNewReleasesAsync(Guid userId, int limit = 15);
    }

    public class HomeScreenService : IHomeScreenService
    {
        private readonly IRecommendationApiService _apiService;
        private readonly ILibraryManager _libraryManager;
        private readonly IEmbyDataService _embyDataService;
        private readonly IUserDataManager _userDataManager;
        private readonly ILogger<HomeScreenService> _logger;

        public HomeScreenService(
            IRecommendationApiService apiService,
            ILibraryManager libraryManager,
            IEmbyDataService embyDataService,
            IUserDataManager userDataManager,
            ILogger<HomeScreenService> logger)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
            _embyDataService = embyDataService ?? throw new ArgumentNullException(nameof(embyDataService));
            _userDataManager = userDataManager ?? throw new ArgumentNullException(nameof(userDataManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<BaseItem>> GetRecommendedItemsForHomeScreenAsync(Guid userId, int limit = 20)
        {
            try
            {
                _logger.LogDebug("Getting home screen recommendations for user {UserId}", userId);

                var config = Plugin.Instance?.Configuration;
                
                // Check if forced fallback mode is enabled
                if (config?.UseEmbyFallbackOnly == true)
                {
                    _logger.LogInformation("Emby fallback mode enabled, using Emby recommendations for user {UserId}", userId);
                    return await GetEmbyRecommendationsAsync(userId, limit);
                }

                // Try AI recommendations first
                var recommendations = await _apiService.GetRecommendationsAsync(userId, limit);
                if (recommendations.Any())
                {
                    var items = await ConvertRecommendationsToBaseItems(recommendations);
                    if (items.Any())
                    {
                        _logger.LogInformation("Retrieved {Count} AI recommendations for user {UserId}", 
                            items.Count(), userId);
                        return items.Take(limit);
                    }
                }

                // Fallback to Emby's built-in recommendations
                _logger.LogInformation("AI recommendations unavailable, falling back to Emby recommendations for user {UserId}", userId);
                return await GetEmbyRecommendationsAsync(userId, limit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recommendations for user {UserId}, falling back to Emby", userId);
                return await GetEmbyRecommendationsAsync(userId, limit);
            }
        }

        public async Task<IEnumerable<BaseItem>> GetTrendingItemsAsync(Guid userId, int limit = 15)
        {
            try
            {
                var config = Plugin.Instance?.Configuration;
                
                // Check if forced fallback mode is enabled
                if (config?.UseEmbyFallbackOnly == true)
                {
                    _logger.LogInformation("Emby fallback mode enabled, using Emby recent additions for user {UserId}", userId);
                    return await GetEmbyRecentlyAddedAsync(userId, limit);
                }

                var recommendations = await _apiService.GetRecommendationsAsync(userId, 50);
                if (recommendations.Any())
                {
                    var trendingRecs = recommendations.Where(r => 
                        r.Reason?.ToLowerInvariant().Contains("trending") == true ||
                        r.Reason?.ToLowerInvariant().Contains("popular") == true ||
                        r.Tags?.Any(t => t.ToLowerInvariant().Contains("trending")) == true);

                    var items = await ConvertRecommendationsToBaseItems(trendingRecs);
                    if (items.Any())
                    {
                        return items.Take(limit);
                    }
                }

                // Fallback to Emby's recently added items as "trending"
                _logger.LogInformation("AI trending recommendations unavailable, falling back to Emby recent additions for user {UserId}", userId);
                return await GetEmbyRecentlyAddedAsync(userId, limit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting trending recommendations for user {UserId}, falling back to Emby", userId);
                return await GetEmbyRecentlyAddedAsync(userId, limit);
            }
        }

        public async Task<IEnumerable<BaseItem>> GetSimilarToFavoritesAsync(Guid userId, int limit = 15)
        {
            try
            {
                var config = Plugin.Instance?.Configuration;
                
                // Check if forced fallback mode is enabled
                if (config?.UseEmbyFallbackOnly == true)
                {
                    _logger.LogInformation("Emby fallback mode enabled, using Emby genre recommendations for user {UserId}", userId);
                    return await GetEmbyGenreBasedRecommendationsAsync(userId, limit);
                }

                var recommendations = await _apiService.GetRecommendationsAsync(userId, 50);
                if (recommendations.Any())
                {
                    var similarRecs = recommendations.Where(r => 
                        r.Reason?.ToLowerInvariant().Contains("similar") == true ||
                        r.Reason?.ToLowerInvariant().Contains("like") == true ||
                        r.Reason?.ToLowerInvariant().Contains("favorite") == true);

                    var items = await ConvertRecommendationsToBaseItems(similarRecs);
                    if (items.Any())
                    {
                        return items.Take(limit);
                    }
                }

                // Fallback to Emby's genre-based recommendations
                _logger.LogInformation("AI similarity recommendations unavailable, falling back to Emby genre recommendations for user {UserId}", userId);
                return await GetEmbyGenreBasedRecommendationsAsync(userId, limit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting similar-to-favorites recommendations for user {UserId}, falling back to Emby", userId);
                return await GetEmbyGenreBasedRecommendationsAsync(userId, limit);
            }
        }

        public async Task<IEnumerable<BaseItem>> GetNewReleasesAsync(Guid userId, int limit = 15)
        {
            try
            {
                var recommendations = await _apiService.GetRecommendationsAsync(userId, 50);
                var newReleaseRecs = recommendations.Where(r => 
                    r.Reason?.ToLowerInvariant().Contains("new") == true ||
                    r.Reason?.ToLowerInvariant().Contains("recent") == true ||
                    r.Reason?.ToLowerInvariant().Contains("release") == true);

                var items = await ConvertRecommendationsToBaseItems(newReleaseRecs);
                return items.Take(limit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting new release recommendations for user {UserId}", userId);
                return Enumerable.Empty<BaseItem>();
            }
        }

        private Task<IEnumerable<BaseItem>> ConvertRecommendationsToBaseItems(IEnumerable<RecommendationResult> recommendations)
        {
            var items = new List<BaseItem>();

            foreach (var recommendation in recommendations)
            {
                try
                {
                    BaseItem? item = null;

                    // Try to find by ItemId first
                    if (recommendation.ItemId != Guid.Empty)
                    {
                        item = _libraryManager.GetItemById(recommendation.ItemId);
                    }

                    // Try to find by TMDB ID
                    if (item == null && recommendation.TmdbId.HasValue)
                    {
                        var query = new InternalItemsQuery
                        {
                            Recursive = true,
                            IsFolder = false
                        };

                        var allItems = _libraryManager.GetItemList(query);
                        item = allItems.FirstOrDefault(i => 
                            i.ProviderIds.ContainsKey("Tmdb") && 
                            i.ProviderIds["Tmdb"] == recommendation.TmdbId.Value.ToString());
                    }

                    // Fallback to name search
                    if (item == null && !string.IsNullOrEmpty(recommendation.ItemName))
                    {
                        var nameQuery = new InternalItemsQuery
                        {
                            Name = recommendation.ItemName,
                            Recursive = true,
                            IsFolder = false
                        };

                        if (!string.IsNullOrEmpty(recommendation.ItemType))
                        {
                            if (recommendation.ItemType.Equals("Movie", StringComparison.OrdinalIgnoreCase))
                            {
                                nameQuery.IncludeItemTypes = new[] { "Movie" };
                            }
                            else if (recommendation.ItemType.Equals("Series", StringComparison.OrdinalIgnoreCase))
                            {
                                nameQuery.IncludeItemTypes = new[] { "Series" };
                            }
                        }

                        var nameMatches = _libraryManager.GetItemList(nameQuery);
                        item = nameMatches.FirstOrDefault(i => 
                            string.Equals(i.Name, recommendation.ItemName, StringComparison.OrdinalIgnoreCase));
                    }

                    if (item != null)
                    {
                        items.Add(item);
                    }
                    else
                    {
                        _logger.LogDebug("Could not find item for recommendation: {ItemName} (TMDB: {TmdbId})", 
                            recommendation.ItemName, recommendation.TmdbId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing recommendation for item {ItemName}", recommendation.ItemName);
                }
            }

            return Task.FromResult(items.Distinct().ToList().AsEnumerable());
        }

        #region Emby Fallback Methods

        private async Task<IEnumerable<BaseItem>> GetEmbyRecommendationsAsync(Guid userId, int limit)
        {
            try
            {
                var user = await _embyDataService.GetUserAsync(userId);
                if (user == null) return Enumerable.Empty<BaseItem>();

                // Get user's highly rated items to base recommendations on
                var favoriteGenres = await GetUserFavoriteGenresAsync(userId);
                var recentlyWatched = await _embyDataService.GetRecentlyWatchedAsync(userId, 10);
                
                var recommendations = new List<BaseItem>();

                // Strategy 1: Items from favorite genres that user hasn't seen
                if (favoriteGenres.Any())
                {
                    var genreQuery = new InternalItemsQuery(user)
                    {
                        Genres = favoriteGenres.Take(3).ToArray(),
                        Recursive = true,
                        IsFolder = false,
                        Limit = limit
                    };

                    var genreItems = _libraryManager.GetItemsResult(genreQuery).Items;
                    recommendations.AddRange(genreItems.Where(item => !HasUserWatched(userId, item)));
                }

                // Strategy 2: Highly rated items user hasn't seen
                if (recommendations.Count < limit)
                {
                    var highRatedQuery = new InternalItemsQuery(user)
                    {
                        MinCommunityRating = 7.0f,
                        Recursive = true,
                        IsFolder = false,
                        Limit = limit * 2
                    };

                    var highRated = _libraryManager.GetItemsResult(highRatedQuery).Items;
                    recommendations.AddRange(highRated.Where(item => !HasUserWatched(userId, item))
                                                     .Take(limit - recommendations.Count));
                }

                _logger.LogInformation("Generated {Count} Emby fallback recommendations for user {UserId}", 
                    recommendations.Count, userId);

                return recommendations.Take(limit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Emby fallback recommendations for user {UserId}", userId);
                return Enumerable.Empty<BaseItem>();
            }
        }

        private async Task<IEnumerable<BaseItem>> GetEmbyRecentlyAddedAsync(Guid userId, int limit)
        {
            try
            {
                var user = await _embyDataService.GetUserAsync(userId);
                if (user == null) return Enumerable.Empty<BaseItem>();

                var query = new InternalItemsQuery(user)
                {
                    Recursive = true,
                    IsFolder = false,
                    Limit = limit
                };

                var items = _libraryManager.GetItemsResult(query).Items;
                return items.Where(item => !HasUserWatched(userId, item));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Emby recently added items for user {UserId}", userId);
                return Enumerable.Empty<BaseItem>();
            }
        }

        private async Task<IEnumerable<BaseItem>> GetEmbyGenreBasedRecommendationsAsync(Guid userId, int limit)
        {
            try
            {
                var user = await _embyDataService.GetUserAsync(userId);
                if (user == null) return Enumerable.Empty<BaseItem>();

                var favoriteGenres = await GetUserFavoriteGenresAsync(userId);
                if (!favoriteGenres.Any()) 
                {
                    // Fallback to popular items if no genre preferences
                    return await GetEmbyRecentlyAddedAsync(userId, limit);
                }

                var query = new InternalItemsQuery(user)
                {
                    Genres = favoriteGenres.Take(2).ToArray(),
                    Recursive = true,
                    IsFolder = false,
                    Limit = limit * 2,
                    MinCommunityRating = 6.0f
                };

                var items = _libraryManager.GetItemsResult(query).Items;
                return items.Where(item => !HasUserWatched(userId, item)).Take(limit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Emby genre-based recommendations for user {UserId}", userId);
                return Enumerable.Empty<BaseItem>();
            }
        }

        private async Task<IEnumerable<string>> GetUserFavoriteGenresAsync(Guid userId)
        {
            try
            {
                var user = await _embyDataService.GetUserAsync(userId);
                if (user == null) return Enumerable.Empty<string>();
                
                var recentlyWatched = await _embyDataService.GetRecentlyWatchedAsync(userId, 50);
                var favoriteItems = recentlyWatched.Where(item => 
                {
                    var userData = _userDataManager.GetUserData(user, item);
                    return userData.IsFavorite || userData.Rating >= 4.0;
                });

                var genreCounts = new Dictionary<string, int>();
                foreach (var item in favoriteItems)
                {
                    if (item.Genres != null)
                    {
                        foreach (var genre in item.Genres)
                        {
                            genreCounts[genre] = genreCounts.GetValueOrDefault(genre, 0) + 1;
                        }
                    }
                }

                return genreCounts.OrderByDescending(g => g.Value)
                                 .Take(5)
                                 .Select(g => g.Key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error determining favorite genres for user {UserId}", userId);
                return Enumerable.Empty<string>();
            }
        }

        private bool HasUserWatched(Guid userId, BaseItem item)
        {
            try
            {
                var user = _embyDataService.GetUserAsync(userId).Result;
                if (user == null) return false;
                var userData = _userDataManager.GetUserData(user, item);
                return userData.Played || userData.PlayCount > 0;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}