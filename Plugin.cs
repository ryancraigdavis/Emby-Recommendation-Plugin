using System;
using System.Collections.Generic;
using System.IO;
using Emby.Recommendation.Plugin.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Emby.Recommendation.Plugin
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        private readonly ILogger<Plugin> _logger;

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILogger<Plugin> logger) 
            : base(applicationPaths, xmlSerializer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Instance = this;
            
            _logger.LogInformation("Recommendation Plugin initialized");
        }

        public override string Name => "Recommendation Plugin";
        public override Guid Id => Guid.Parse("8c95c4d2-e50c-4fb0-a4f6-8b7c9e3d2a1b");
        public override string Description => "Intelligent recommendation plugin for Emby Media Server";

        public static Plugin? Instance { get; private set; }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = this.Name,
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
                },
                new PluginPageInfo
                {
                    Name = "RecommendationHomeScreen",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.homescreen.js"
                }
            };
        }

        // Configuration update handling can be done through events if needed
    }
}