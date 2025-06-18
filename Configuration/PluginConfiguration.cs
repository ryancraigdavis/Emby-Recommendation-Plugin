using MediaBrowser.Model.Plugins;

namespace RecommendationPlugin.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public PluginConfiguration()
        {
            RecommendationCount = 10;
            CollectionName = "Recommended Movies";
            IsEnabled = true;
        }

        public int RecommendationCount { get; set; }
        public string CollectionName { get; set; }
        public bool IsEnabled { get; set; }
    }
}