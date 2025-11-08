using System;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;

namespace EmbyTestPlugin
{
    public class ServerEntryPoint : IServerEntryPoint
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ICollectionManager _collectionManager;
        private readonly ILogger _logger;

        public ServerEntryPoint(
            ILibraryManager libraryManager,
            ICollectionManager collectionManager,
            ILogManager logManager)
        {
            _libraryManager = libraryManager;
            _collectionManager = collectionManager;
            _logger = logManager.GetLogger(GetType().Name);
        }

        public void Run()
        {
            _logger.Info("Test Collection Plugin starting up");

            // Delay initialization to allow server to fully start
            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                await CreateTestCollection();
            });
        }

        private async Task CreateTestCollection()
        {
            try
            {
                var config = Plugin.Instance.Configuration;
                var collectionName = config.CollectionName;

                _logger.Info($"Creating/updating collection: {collectionName}");

                // Get all movies from the library, sorted alphabetically
                var movies = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { typeof(Movie).Name },
                    IsVirtualItem = false,
                    Recursive = true,
                    OrderBy = new[]
                    {
                        new ValueTuple<string, SortOrder>(ItemSortBy.SortName, SortOrder.Ascending)
                    }
                }).OfType<Movie>().ToList();

                if (!movies.Any())
                {
                    _logger.Warn("No movies found in library. Collection will be empty.");
                    return;
                }

                // Take the first 5 alphabetical movies
                var selectedMovies = movies.Take(config.MovieCount).ToList();

                _logger.Info($"Selected {selectedMovies.Count} movies for collection:");
                foreach (var movie in selectedMovies)
                {
                    _logger.Info($"  - {movie.Name} (Year: {movie.ProductionYear})");
                }

                // Check if collection already exists
                var existingCollections = _collectionManager.GetCollections(null);
                var existingCollection = existingCollections.FirstOrDefault(c =>
                    c.Name.Equals(collectionName, StringComparison.OrdinalIgnoreCase));

                if (existingCollection != null)
                {
                    _logger.Info($"Collection '{collectionName}' already exists. Updating items...");

                    // Remove all existing items from the collection
                    var currentItems = existingCollection.GetLinkedChildren().ToList();
                    if (currentItems.Any())
                    {
                        await _collectionManager.RemoveFromCollection(
                            existingCollection.Id,
                            currentItems.Select(i => i.Id).ToArray());
                    }

                    // Add the new items
                    await _collectionManager.AddToCollection(
                        existingCollection.Id,
                        selectedMovies.Select(m => m.Id).ToArray());

                    _logger.Info($"Collection '{collectionName}' updated successfully with {selectedMovies.Count} movies");
                }
                else
                {
                    _logger.Info($"Creating new collection '{collectionName}'...");

                    // Create new collection
                    var collection = await _collectionManager.CreateCollection(new CollectionCreationOptions
                    {
                        Name = collectionName,
                        ItemIdList = selectedMovies.Select(m => m.Id.ToString("N")).ToArray()
                    });

                    _logger.Info($"Collection '{collectionName}' created successfully with {selectedMovies.Count} movies");
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error creating test collection", ex);
            }
        }

        public void Dispose()
        {
            _logger.Info("Test Collection Plugin shutting down");
        }
    }
}
