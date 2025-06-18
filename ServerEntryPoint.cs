using System;
using System.Threading.Tasks;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using RecommendationPlugin.Services;

namespace RecommendationPlugin
{
    public class ServerEntryPoint : IServerEntryPoint
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ICollectionManager _collectionManager;
        private readonly ILogger _logger;
        public RecommendationService _recommendationService;
        
        public static ServerEntryPoint Instance { get; private set; }
        
        public ServerEntryPoint(ILibraryManager libraryManager, ICollectionManager collectionManager, ILogManager logManager)
        {
            Instance = this;
            _libraryManager = libraryManager;
            _collectionManager = collectionManager;
            _logger = logManager.GetLogger(GetType().Name);
        }

        public void Run()
        {
            _logger.Info("Recommendation Plugin starting up");
            
            _recommendationService = new RecommendationService(_libraryManager, _collectionManager, _logger);
            
            _libraryManager.ItemAdded += OnLibraryItemAdded;
            _libraryManager.ItemRemoved += OnLibraryItemRemoved;
            
            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30));
                await InitializeRecommendationCollection();
            });
        }

        private void OnLibraryItemAdded(object sender, ItemChangeEventArgs e)
        {
            if (e.Item is MediaBrowser.Controller.Entities.Movies.Movie)
            {
                _logger.Debug("New movie added to library, will update recommendations on next scheduled task");
            }
        }

        private void OnLibraryItemRemoved(object sender, ItemChangeEventArgs e)
        {
            if (e.Item is MediaBrowser.Controller.Entities.Movies.Movie)
            {
                _logger.Debug("Movie removed from library, will update recommendations on next scheduled task");
            }
        }

        private async Task InitializeRecommendationCollection()
        {
            try
            {
                var config = Plugin.Instance.Configuration;
                if (config.IsEnabled)
                {
                    _logger.Info("Initializing recommendation collection on startup");
                    await _recommendationService.UpdateRecommendationCollection(config);
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error initializing recommendation collection", ex);
            }
        }

        public void Dispose()
        {
            if (_libraryManager != null)
            {
                _libraryManager.ItemAdded -= OnLibraryItemAdded;
                _libraryManager.ItemRemoved -= OnLibraryItemRemoved;
            }
            
            _logger.Info("Recommendation Plugin shutting down");
        }
    }
}