using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using RecommendationPlugin.Configuration;

namespace RecommendationPlugin.Services
{
    public class RecommendationService
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ICollectionManager _collectionManager;
        private readonly ILogger _logger;

        public RecommendationService(ILibraryManager libraryManager, ICollectionManager collectionManager, ILogger logger)
        {
            _libraryManager = libraryManager;
            _collectionManager = collectionManager;
            _logger = logger;
        }

        public Task<List<Movie>> GetRecommendedMovies(int count)
        {
            try
            {
                var allMovies = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { typeof(Movie).Name },
                    IsVirtualItem = false,
                    OrderBy = new[]
                    {
                        new ValueTuple<string, SortOrder>(ItemSortBy.Random, SortOrder.Ascending)
                    }
                }).OfType<Movie>().ToList();

                if (!allMovies.Any())
                {
                    _logger.Info("No movies found in library");
                    return Task.FromResult(new List<Movie>());
                }

                var recommendedMovies = allMovies
                    .Where(m => m.CommunityRating.HasValue && m.CommunityRating.Value >= 7.0)
                    .OrderByDescending(m => m.CommunityRating.Value)
                    .ThenByDescending(m => m.DateCreated)
                    .Take(count)
                    .ToList();

                if (recommendedMovies.Count < count)
                {
                    var additionalMovies = allMovies
                        .Where(m => !recommendedMovies.Contains(m))
                        .OrderBy(m => Guid.NewGuid())
                        .Take(count - recommendedMovies.Count);
                    
                    recommendedMovies.AddRange(additionalMovies);
                }

                _logger.Info($"Found {recommendedMovies.Count} recommended movies");
                return Task.FromResult(recommendedMovies);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error getting recommended movies", ex);
                return Task.FromResult(new List<Movie>());
            }
        }

        public async Task UpdateRecommendationCollection(PluginConfiguration config)
        {
            try
            {
                if (!config.IsEnabled)
                {
                    _logger.Info("Recommendation collection is disabled");
                    return;
                }

                var recommendedMovies = await GetRecommendedMovies(config.RecommendationCount);
                
                if (!recommendedMovies.Any())
                {
                    _logger.Info("No recommended movies to add to collection");
                    return;
                }

                _logger.Info($"Creating/updating collection '{config.CollectionName}' with {recommendedMovies.Count} movies");
                
                // For now, just log the recommended movies
                // Collection creation will need to be implemented once we understand the correct API
                foreach (var movie in recommendedMovies.Take(5))
                {
                    _logger.Info($"Recommended movie: {movie.Name} (Rating: {movie.CommunityRating})");
                }
                
                if (recommendedMovies.Count > 5)
                {
                    _logger.Info($"... and {recommendedMovies.Count - 5} more movies");
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error updating recommendation collection", ex);
            }
        }
    }
}