using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Collections;
using Emby.Recommendation.Plugin.Models;
using Microsoft.Extensions.Logging;

namespace Emby.Recommendation.Plugin.Services
{
    public interface ICollectionService
    {
        Task<BoxSet?> CreateRecommendationCollectionAsync(string collectionName, IEnumerable<RecommendationResult> recommendations, Guid userId);
        Task<bool> UpdateRecommendationCollectionAsync(Guid collectionId, IEnumerable<RecommendationResult> recommendations);
        Task<bool> DeleteRecommendationCollectionAsync(Guid collectionId);
        Task<IEnumerable<BoxSet>> GetRecommendationCollectionsAsync(Guid userId);
        Task<bool> CleanupOldCollectionsAsync(Guid userId, int maxCollections);
        Task<BoxSet?> FindCollectionByNameAsync(string name, Guid userId);
    }

    public class CollectionService : ICollectionService
    {
        private readonly ICollectionManager _collectionManager;
        private readonly ILibraryManager _libraryManager;
        private readonly IEmbyDataService _embyDataService;
        private readonly ILogger<CollectionService> _logger;
        private const string RECOMMENDATION_PREFIX = "AI Recommendations: ";

        public CollectionService(
            ICollectionManager collectionManager,
            ILibraryManager libraryManager,
            IEmbyDataService embyDataService,
            ILogger<CollectionService> logger)
        {
            _collectionManager = collectionManager ?? throw new ArgumentNullException(nameof(collectionManager));
            _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
            _embyDataService = embyDataService ?? throw new ArgumentNullException(nameof(embyDataService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<BoxSet?> CreateRecommendationCollectionAsync(string collectionName, IEnumerable<RecommendationResult> recommendations, Guid userId)
        {
            try
            {
                var user = await _embyDataService.GetUserAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User {UserId} not found for collection creation", userId);
                    return null;
                }

                var fullCollectionName = RECOMMENDATION_PREFIX + collectionName;
                
                // Check if collection already exists
                var existingCollection = await FindCollectionByNameAsync(fullCollectionName, userId);
                if (existingCollection != null)
                {
                    _logger.LogInformation("Collection {CollectionName} already exists, updating instead", fullCollectionName);
                    await UpdateRecommendationCollectionAsync(existingCollection.Id, recommendations);
                    return existingCollection;
                }

                var itemIds = await GetItemIdsFromRecommendationsAsync(recommendations, userId);
                if (!itemIds.Any())
                {
                    _logger.LogWarning("No valid items found for recommendations in collection {CollectionName}", collectionName);
                    return null;
                }

                var options = new CollectionCreationOptions
                {
                    Name = fullCollectionName,
                    ItemIdList = itemIds.ToArray(),
                    IsLocked = false,
                    ParentId = Guid.Empty
                };

                var result = await _collectionManager.CreateCollectionAsync(options);
                if (result?.Item != null)
                {
                    _logger.LogInformation("Successfully created recommendation collection {CollectionName} with {ItemCount} items", 
                        fullCollectionName, itemIds.Count());
                    
                    // Set collection metadata
                    var collection = result.Item;
                    collection.Overview = $"AI-generated recommendations based on your viewing preferences. Generated on {DateTime.Now:yyyy-MM-dd HH:mm}";
                    collection.Tags = new[] { "AI Recommendations", "Auto-Generated" };
                    
                    await _libraryManager.UpdateItemAsync(collection, collection.GetParent(), ItemUpdateType.MetadataEdit, null);
                    
                    return collection;
                }

                _logger.LogError("Failed to create collection {CollectionName}", fullCollectionName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating recommendation collection {CollectionName}", collectionName);
                return null;
            }
        }

        public async Task<bool> UpdateRecommendationCollectionAsync(Guid collectionId, IEnumerable<RecommendationResult> recommendations)
        {
            try
            {
                var collection = _libraryManager.GetItemById(collectionId) as BoxSet;
                if (collection == null)
                {
                    _logger.LogWarning("Collection {CollectionId} not found for update", collectionId);
                    return false;
                }

                var itemIds = await GetItemIdsFromRecommendationsAsync(recommendations, Guid.Empty);
                if (!itemIds.Any())
                {
                    _logger.LogWarning("No valid items found for recommendations in collection update {CollectionId}", collectionId);
                    return false;
                }

                // Clear existing items and add new ones
                var currentChildren = collection.Children.ToList();
                foreach (var child in currentChildren)
                {
                    collection.RemoveChild(child);
                }

                foreach (var itemId in itemIds)
                {
                    var item = _libraryManager.GetItemById(itemId);
                    if (item != null)
                    {
                        collection.AddChild(item, CancellationToken.None);
                    }
                }

                // Update metadata
                collection.Overview = $"AI-generated recommendations based on your viewing preferences. Last updated on {DateTime.Now:yyyy-MM-dd HH:mm}";
                collection.DateModified = DateTime.UtcNow;

                await _libraryManager.UpdateItemAsync(collection, collection.GetParent(), ItemUpdateType.MetadataEdit, null);

                _logger.LogInformation("Successfully updated recommendation collection {CollectionId} with {ItemCount} items", 
                    collectionId, itemIds.Count());

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating recommendation collection {CollectionId}", collectionId);
                return false;
            }
        }

        public async Task<bool> DeleteRecommendationCollectionAsync(Guid collectionId)
        {
            try
            {
                var collection = _libraryManager.GetItemById(collectionId);
                if (collection == null)
                {
                    _logger.LogWarning("Collection {CollectionId} not found for deletion", collectionId);
                    return false;
                }

                await _libraryManager.DeleteItem(collection, new DeleteOptions
                {
                    DeleteFileLocation = false,
                    DeleteFromExternalProvider = false
                }, collection.GetParent(), false);

                _logger.LogInformation("Successfully deleted recommendation collection {CollectionId}", collectionId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting recommendation collection {CollectionId}", collectionId);
                return false;
            }
        }

        public async Task<IEnumerable<BoxSet>> GetRecommendationCollectionsAsync(Guid userId)
        {
            try
            {
                var user = await _embyDataService.GetUserAsync(userId);
                if (user == null) return Enumerable.Empty<BoxSet>();

                var collections = _libraryManager.GetItemList(new InternalItemsQuery(user)
                {
                    IncludeItemTypes = new[] { nameof(BoxSet) },
                    Recursive = true
                }).OfType<BoxSet>()
                .Where(c => c.Name.StartsWith(RECOMMENDATION_PREFIX))
                .OrderByDescending(c => c.DateModified);

                return collections;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recommendation collections for user {UserId}", userId);
                return Enumerable.Empty<BoxSet>();
            }
        }

        public async Task<bool> CleanupOldCollectionsAsync(Guid userId, int maxCollections)
        {
            try
            {
                var collections = await GetRecommendationCollectionsAsync(userId);
                var collectionsToDelete = collections.Skip(maxCollections).ToList();

                if (!collectionsToDelete.Any())
                {
                    return true;
                }

                foreach (var collection in collectionsToDelete)
                {
                    await DeleteRecommendationCollectionAsync(collection.Id);
                }

                _logger.LogInformation("Cleaned up {Count} old recommendation collections for user {UserId}", 
                    collectionsToDelete.Count, userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old collections for user {UserId}", userId);
                return false;
            }
        }

        public async Task<BoxSet?> FindCollectionByNameAsync(string name, Guid userId)
        {
            try
            {
                var collections = await GetRecommendationCollectionsAsync(userId);
                return collections.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding collection by name {CollectionName} for user {UserId}", name, userId);
                return null;
            }
        }

        private async Task<IEnumerable<Guid>> GetItemIdsFromRecommendationsAsync(IEnumerable<RecommendationResult> recommendations, Guid userId)
        {
            var itemIds = new List<Guid>();

            foreach (var recommendation in recommendations)
            {
                try
                {
                    // First try to find by ItemId if it's provided
                    if (recommendation.ItemId != Guid.Empty)
                    {
                        var item = _libraryManager.GetItemById(recommendation.ItemId);
                        if (item != null)
                        {
                            itemIds.Add(item.Id);
                            continue;
                        }
                    }

                    // Try to find by TMDB ID
                    if (recommendation.TmdbId.HasValue)
                    {
                        var tmdbItems = _libraryManager.GetItemList(new InternalItemsQuery
                        {
                            HasTmdbId = true,
                            Recursive = true,
                            IsFolder = false
                        }).Where(i => i.GetProviderId("Tmdb") == recommendation.TmdbId.Value.ToString());

                        var matchingItem = tmdbItems.FirstOrDefault();
                        if (matchingItem != null)
                        {
                            itemIds.Add(matchingItem.Id);
                            continue;
                        }
                    }

                    // Fallback: try to find by name and type
                    if (!string.IsNullOrEmpty(recommendation.ItemName))
                    {
                        var query = new InternalItemsQuery
                        {
                            Name = recommendation.ItemName,
                            Recursive = true,
                            IsFolder = false
                        };

                        if (!string.IsNullOrEmpty(recommendation.ItemType))
                        {
                            if (recommendation.ItemType.Equals("Movie", StringComparison.OrdinalIgnoreCase))
                            {
                                query.IncludeItemTypes = new[] { nameof(Movie) };
                            }
                            else if (recommendation.ItemType.Equals("Series", StringComparison.OrdinalIgnoreCase))
                            {
                                query.IncludeItemTypes = new[] { nameof(Series) };
                            }
                        }

                        var nameMatches = _libraryManager.GetItemList(query);
                        var exactMatch = nameMatches.FirstOrDefault(i => 
                            string.Equals(i.Name, recommendation.ItemName, StringComparison.OrdinalIgnoreCase));

                        if (exactMatch != null)
                        {
                            itemIds.Add(exactMatch.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing recommendation for item {ItemName}", 
                        recommendation.ItemName);
                }
            }

            return itemIds.Distinct();
        }
    }
}