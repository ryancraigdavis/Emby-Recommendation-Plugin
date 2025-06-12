using System;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Emby.Recommendation.Plugin.Services;
using Microsoft.Extensions.Logging;

namespace Emby.Recommendation.Plugin
{
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            // Register HTTP client factory
            serviceCollection.AddHttpClient("RecommendationApi", client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Emby-Recommendation-Plugin/1.0");
            });

            // Register plugin services
            serviceCollection.AddTransient<IEmbyDataService, EmbyDataService>();
            serviceCollection.AddTransient<IRecommendationApiService, RecommendationApiService>();
            serviceCollection.AddSingleton<IKafkaEventService, KafkaEventService>();
            serviceCollection.AddTransient<ICollectionService, CollectionService>();
            serviceCollection.AddTransient<ISyncService, SyncService>();
            serviceCollection.AddTransient<IRecommendationService, RecommendationService>();
        }
    }
}