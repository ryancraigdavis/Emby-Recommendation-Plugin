using System;
using System.ComponentModel.DataAnnotations;
using MediaBrowser.Model.Plugins;

namespace Emby.Recommendation.Plugin.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        [Required]
        public string MicroserviceBaseUrl { get; set; } = "https://localhost:5001";

        [Required]
        public string ApiKey { get; set; } = string.Empty;

        public int SyncIntervalMinutes { get; set; } = 60;

        public DateTime? LastSyncTime { get; set; }

        public bool EnableKafkaEvents { get; set; } = true;

        [Required]
        public string KafkaBootstrapServers { get; set; } = "localhost:9092";

        public string KafkaTopic { get; set; } = "emby-events";

        public int HttpTimeoutSeconds { get; set; } = 30;

        public bool EnableDebugLogging { get; set; } = false;

        public int MaxRecommendationCollections { get; set; } = 10;

        public bool AutoCreateCollections { get; set; } = true;
    }
}