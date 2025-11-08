using MediaBrowser.Model.Plugins;

namespace EmbyTestPlugin.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public PluginConfiguration()
        {
            CollectionName = "Test_collection";
            MovieCount = 5;
        }

        public string CollectionName { get; set; }
        public int MovieCount { get; set; }
    }
}
