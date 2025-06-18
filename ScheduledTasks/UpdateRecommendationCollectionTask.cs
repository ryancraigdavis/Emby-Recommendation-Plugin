using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using RecommendationPlugin.Services;

namespace RecommendationPlugin.ScheduledTasks
{
    public class UpdateRecommendationCollectionTask : IScheduledTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ICollectionManager _collectionManager;
        private readonly ILogger _logger;

        public UpdateRecommendationCollectionTask(ILibraryManager libraryManager, ICollectionManager collectionManager, ILogManager logManager)
        {
            _libraryManager = libraryManager;
            _collectionManager = collectionManager;
            _logger = logManager.GetLogger(GetType().Name);
        }

        public string Name => "Update Recommendation Collection";
        public string Key => "UpdateRecommendationCollection";
        public string Description => "Updates the recommendation collection with new movies";
        public string Category => "Recommendations";

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerDaily,
                    TimeOfDayTicks = TimeSpan.FromHours(3).Ticks
                }
            };
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            progress?.Report(0);

            try
            {
                var config = Plugin.Instance.Configuration;
                
                if (ServerEntryPoint.Instance?._recommendationService != null)
                {
                    _logger.Info("Starting recommendation collection update");
                    await ServerEntryPoint.Instance._recommendationService.UpdateRecommendationCollection(config);
                    _logger.Info("Recommendation collection update completed");
                }
                else
                {
                    _logger.Warn("RecommendationService not available");
                }
                
                progress?.Report(100);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error during recommendation collection update", ex);
                throw;
            }
        }
    }
}