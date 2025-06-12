using System;
using System.Threading.Tasks;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Events.Session;
using MediaBrowser.Controller.Session;
using Emby.Recommendation.Plugin.Models;
using Emby.Recommendation.Plugin.Services;
using Microsoft.Extensions.Logging;

namespace Emby.Recommendation.Plugin.EventHandlers
{
    public class SessionEventHandler : IEventConsumer<PlaybackStartEventArgs>,
                                       IEventConsumer<PlaybackStopEventArgs>,
                                       IEventConsumer<PlaybackPauseEventArgs>,
                                       IEventConsumer<PlaybackUnpauseEventArgs>,
                                       IEventConsumer<PlaybackProgressEventArgs>
    {
        private readonly IKafkaEventService _kafkaService;
        private readonly IRecommendationApiService _apiService;
        private readonly ILogger<SessionEventHandler> _logger;

        public SessionEventHandler(
            IKafkaEventService kafkaService,
            IRecommendationApiService apiService,
            ILogger<SessionEventHandler> logger)
        {
            _kafkaService = kafkaService ?? throw new ArgumentNullException(nameof(kafkaService));
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task OnEvent(PlaybackStartEventArgs eventArgs)
        {
            try
            {
                var watchEvent = CreateWatchEvent(eventArgs.Session, eventArgs.MediaInfo, "play_start");
                await SendWatchEvent(watchEvent);
                
                _logger.LogDebug("Processed playback start event for user {UserId}, item {ItemId}", 
                    eventArgs.Session.UserId, eventArgs.MediaInfo?.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing playback start event");
            }
        }

        public async Task OnEvent(PlaybackStopEventArgs eventArgs)
        {
            try
            {
                var watchEvent = CreateWatchEvent(eventArgs.Session, eventArgs.MediaInfo, "play_stop");
                await SendWatchEvent(watchEvent);
                
                _logger.LogDebug("Processed playback stop event for user {UserId}, item {ItemId}", 
                    eventArgs.Session.UserId, eventArgs.MediaInfo?.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing playback stop event");
            }
        }

        public async Task OnEvent(PlaybackPauseEventArgs eventArgs)
        {
            try
            {
                var watchEvent = CreateWatchEvent(eventArgs.Session, eventArgs.MediaInfo, "pause");
                await SendWatchEvent(watchEvent);
                
                _logger.LogDebug("Processed playback pause event for user {UserId}, item {ItemId}", 
                    eventArgs.Session.UserId, eventArgs.MediaInfo?.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing playback pause event");
            }
        }

        public async Task OnEvent(PlaybackUnpauseEventArgs eventArgs)
        {
            try
            {
                var watchEvent = CreateWatchEvent(eventArgs.Session, eventArgs.MediaInfo, "resume");
                await SendWatchEvent(watchEvent);
                
                _logger.LogDebug("Processed playback resume event for user {UserId}, item {ItemId}", 
                    eventArgs.Session.UserId, eventArgs.MediaInfo?.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing playback resume event");
            }
        }

        public async Task OnEvent(PlaybackProgressEventArgs eventArgs)
        {
            try
            {
                // Only log progress events periodically to avoid spam
                if (ShouldLogProgressEvent(eventArgs))
                {
                    var watchEvent = CreateWatchEvent(eventArgs.Session, eventArgs.MediaInfo, "progress");
                    await SendWatchEvent(watchEvent);
                    
                    _logger.LogDebug("Processed playback progress event for user {UserId}, item {ItemId}", 
                        eventArgs.Session.UserId, eventArgs.MediaInfo?.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing playback progress event");
            }
        }

        private WatchEvent CreateWatchEvent(SessionInfo session, BaseItemDto? mediaInfo, string eventType)
        {
            return new WatchEvent
            {
                UserId = session.UserId ?? Guid.Empty,
                ItemId = Guid.TryParse(mediaInfo?.Id, out var itemId) ? itemId : Guid.Empty,
                EventType = eventType,
                Timestamp = DateTime.UtcNow,
                PlaybackPositionTicks = session.PlayState?.PositionTicks,
                DeviceId = session.DeviceId,
                DeviceName = session.DeviceName,
                ClientName = session.Client
            };
        }

        private async Task SendWatchEvent(WatchEvent watchEvent)
        {
            // Send to both Kafka and HTTP API
            var kafkaTask = _kafkaService.SendWatchEventAsync(watchEvent);
            var apiTask = _apiService.SendWatchEventAsync(watchEvent);

            await Task.WhenAll(kafkaTask, apiTask);
        }

        private bool ShouldLogProgressEvent(PlaybackProgressEventArgs eventArgs)
        {
            // Log progress every 30 seconds or at key milestones
            var positionTicks = eventArgs.Session.PlayState?.PositionTicks ?? 0;
            var positionSeconds = TimeSpan.FromTicks(positionTicks).TotalSeconds;
            
            // Log at 30-second intervals
            return positionSeconds % 30 < 1;
        }
    }
}