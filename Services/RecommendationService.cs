using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Emby.Recommendation.Plugin.Models;
using Microsoft.Extensions.Logging;

namespace Emby.Recommendation.Plugin.Services
{
    public interface IRecommendationService
    {
        Task<bool> GenerateRecommendationsForUserAsync(Guid userId);
        Task<bool> GenerateRecommendationsForAllUsersAsync();
        Task<IEnumerable<RecommendationResult>> GetRecommendationsAsync(Guid userId, int count = 20);
        Task<bool> CreateRecommendationCollectionsAsync(Guid userId);
    }

    public class RecommendationService : IRecommendationService
    {
        private readonly IRecommendationApiService _apiService;
        private readonly ICollectionService _collectionService;
        private readonly IEmbyDataService _embyDataService;
        private readonly IKafkaEventService _kafkaService;
        private readonly ILogger<RecommendationService> _logger;

        public RecommendationService(
            IRecommendationApiService apiService,
            ICollectionService collectionService,
            IEmbyDataService embyDataService,
            IKafkaEventService kafkaService,
            ILogger<RecommendationService> logger)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
            _embyDataService = embyDataService ?? throw new ArgumentNullException(nameof(embyDataService));
            _kafkaService = kafkaService ?? throw new ArgumentNullException(nameof(kafkaService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> GenerateRecommendationsForUserAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Generating recommendations for user {UserId}", userId);

                var user = await _embyDataService.GetUserAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User {UserId} not found", userId);
                    return false;
                }

                var recommendations = await _apiService.GetRecommendationsAsync(userId);
                if (!recommendations.Any())
                {
                    _logger.LogInformation("No recommendations returned for user {UserId}", userId);
                    return false;
                }

                var config = Plugin.Instance?.Configuration;
                if (config?.AutoCreateCollections == true)
                {
                    await CreateRecommendationCollectionsAsync(userId, recommendations);
                }

                await _kafkaService.SendUserEventAsync("recommendations_generated", userId, 
                    new { Count = recommendations.Count() });

                _logger.LogInformation("Successfully generated {Count} recommendations for user {UserId}", 
                    recommendations.Count(), userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating recommendations for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> GenerateRecommendationsForAllUsersAsync()
        {
            try
            {
                _logger.LogInformation("Generating recommendations for all users");

                var users = await _embyDataService.GetUsersAsync();
                var successCount = 0;
                var totalCount = users.Count();

                foreach (var user in users)
                {
                    try
                    {
                        var success = await GenerateRecommendationsForUserAsync(user.Id);
                        if (success) successCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error generating recommendations for user {UserId}", user.Id);
                    }
                }

                _logger.LogInformation("Completed recommendation generation: {SuccessCount}/{TotalCount} users processed", 
                    successCount, totalCount);

                return successCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during generate recommendations for all users operation");
                return false;
            }
        }

        public async Task<IEnumerable<RecommendationResult>> GetRecommendationsAsync(Guid userId, int count = 20)
        {
            try
            {
                return await _apiService.GetRecommendationsAsync(userId, count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recommendations for user {UserId}", userId);
                return new List<RecommendationResult>();
            }
        }

        public async Task<bool> CreateRecommendationCollectionsAsync(Guid userId)
        {
            var recommendations = await GetRecommendationsAsync(userId, 50);
            return await CreateRecommendationCollectionsAsync(userId, recommendations);
        }

        private async Task<bool> CreateRecommendationCollectionsAsync(Guid userId, IEnumerable<RecommendationResult> recommendations)
        {
            try
            {
                var config = Plugin.Instance?.Configuration;
                if (config == null) return false;

                // Group recommendations by category/reason for better collections
                var groupedRecommendations = GroupRecommendationsByCategory(recommendations);

                var createdCollections = 0;
                var maxCollections = Math.Min(groupedRecommendations.Count(), config.MaxRecommendationCollections);

                foreach (var group in groupedRecommendations.Take(maxCollections))
                {
                    try
                    {
                        var collectionName = GenerateCollectionName(group.Key, group.Value);
                        var collection = await _collectionService.CreateRecommendationCollectionAsync(
                            collectionName, group.Value, userId);

                        if (collection != null)
                        {
                            createdCollections++;
                            await _kafkaService.SendUserEventAsync("collection_created", userId, 
                                new { CollectionId = collection.Id, CollectionName = collectionName });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error creating collection for category {Category}", group.Key);
                    }
                }

                // Cleanup old collections if we exceed the limit
                await _collectionService.CleanupOldCollectionsAsync(userId, config.MaxRecommendationCollections);

                _logger.LogInformation("Created {CreatedCount} recommendation collections for user {UserId}", 
                    createdCollections, userId);

                return createdCollections > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating recommendation collections for user {UserId}", userId);
                return false;
            }
        }

        private IEnumerable<IGrouping<string, RecommendationResult>> GroupRecommendationsByCategory(IEnumerable<RecommendationResult> recommendations)
        {
            return recommendations
                .GroupBy(r => ExtractPrimaryCategory(r))
                .OrderByDescending(g => g.Average(r => r.Score))
                .Where(g => g.Count() >= 3); // Only create collections with at least 3 items
        }

        private string ExtractPrimaryCategory(RecommendationResult recommendation)
        {
            // Extract meaningful category from reason or tags
            var reason = recommendation.Reason?.ToLowerInvariant() ?? "";
            var tags = recommendation.Tags?.FirstOrDefault()?.ToLowerInvariant() ?? "";

            if (reason.Contains("similar") || reason.Contains("like"))
                return "Similar Content";
            else if (reason.Contains("genre") || tags.Contains("genre"))
                return "Genre Recommendations";
            else if (reason.Contains("actor") || reason.Contains("director"))
                return "Based on Cast & Crew";
            else if (reason.Contains("trending") || reason.Contains("popular"))
                return "Trending Now";
            else if (reason.Contains("recent") || reason.Contains("new"))
                return "New Releases";
            else
                return "For You";
        }

        private string GenerateCollectionName(string category, IEnumerable<RecommendationResult> recommendations)
        {
            var timestamp = DateTime.Now.ToString("MMM dd");
            var itemCount = recommendations.Count();
            
            return category switch
            {
                "Similar Content" => $"More Like Your Favorites ({timestamp})",
                "Genre Recommendations" => $"Discover New Genres ({timestamp})",
                "Based on Cast & Crew" => $"From Your Favorite Creators ({timestamp})",
                "Trending Now" => $"What's Trending ({timestamp})",
                "New Releases" => $"Fresh Picks ({timestamp})",
                _ => $"Recommended for You ({timestamp})"
            };
        }
    }
}