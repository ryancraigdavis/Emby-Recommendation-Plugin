using System;
using System.Text.Json;
using System.Threading.Tasks;
using Confluent.Kafka;
using Emby.Recommendation.Plugin.Configuration;
using Emby.Recommendation.Plugin.Models;
using Microsoft.Extensions.Logging;

namespace Emby.Recommendation.Plugin.Services
{
    public interface IKafkaEventService
    {
        Task<bool> SendEventAsync<T>(string eventType, T eventData) where T : class;
        Task<bool> SendWatchEventAsync(WatchEvent watchEvent);
        Task<bool> SendUserEventAsync(string eventType, Guid userId, object? eventData = null);
        Task<bool> SendContentEventAsync(string eventType, Guid itemId, object? eventData = null);
        void Dispose();
    }

    public class KafkaEventService : IKafkaEventService, IDisposable
    {
        private readonly IProducer<string, string> _producer;
        private readonly ILogger<KafkaEventService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly string _topicName;
        private bool _disposed = false;

        public KafkaEventService(ILogger<KafkaEventService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            var config = Plugin.Instance?.Configuration;
            if (config == null)
            {
                throw new InvalidOperationException("Plugin configuration not available");
            }

            _topicName = config.KafkaTopic;
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            var producerConfig = new ProducerConfig
            {
                BootstrapServers = config.KafkaBootstrapServers,
                MessageTimeoutMs = 30000,
                RequestTimeoutMs = 30000,
                DeliveryReportFields = "key,value,status,error",
                EnableIdempotence = true,
                Acks = Acks.All,
                RetryBackoffMs = 1000,
                MessageSendMaxRetries = 3
            };

            try
            {
                _producer = new ProducerBuilder<string, string>(producerConfig)
                    .SetErrorHandler((_, e) => _logger.LogError("Kafka producer error: {Error}", e.Reason))
                    .SetLogHandler((_, log) => 
                    {
                        if (config.EnableDebugLogging)
                        {
                            _logger.LogDebug("Kafka log: {Message}", log.Message);
                        }
                    })
                    .Build();

                _logger.LogInformation("Kafka producer initialized for topic {Topic}", _topicName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Kafka producer");
                throw;
            }
        }

        public async Task<bool> SendEventAsync<T>(string eventType, T eventData) where T : class
        {
            var config = Plugin.Instance?.Configuration;
            if (config == null || !config.EnableKafkaEvents)
            {
                return false;
            }

            try
            {
                var envelope = new KafkaEventEnvelope
                {
                    EventType = eventType,
                    Timestamp = DateTime.UtcNow,
                    Source = "emby-recommendation-plugin",
                    Version = "1.0",
                    Data = eventData
                };

                var json = JsonSerializer.Serialize(envelope, _jsonOptions);
                var key = $"{eventType}_{DateTime.UtcNow:yyyyMMddHHmmss}";

                var message = new Message<string, string>
                {
                    Key = key,
                    Value = json,
                    Headers = new Headers
                    {
                        { "event-type", System.Text.Encoding.UTF8.GetBytes(eventType) },
                        { "source", System.Text.Encoding.UTF8.GetBytes("emby-plugin") },
                        { "timestamp", System.Text.Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()) }
                    }
                };

                var deliveryResult = await _producer.ProduceAsync(_topicName, message);
                
                if (deliveryResult.Status == PersistenceStatus.Persisted)
                {
                    _logger.LogDebug("Successfully sent {EventType} event to Kafka", eventType);
                    return true;
                }

                _logger.LogWarning("Failed to send {EventType} event to Kafka. Status: {Status}", 
                    eventType, deliveryResult.Status);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending {EventType} event to Kafka", eventType);
                return false;
            }
        }

        public async Task<bool> SendWatchEventAsync(WatchEvent watchEvent)
        {
            return await SendEventAsync("watch_event", watchEvent);
        }

        public async Task<bool> SendUserEventAsync(string eventType, Guid userId, object? eventData = null)
        {
            var userEvent = new
            {
                UserId = userId,
                EventType = eventType,
                Data = eventData
            };

            return await SendEventAsync($"user_{eventType}", userEvent);
        }

        public async Task<bool> SendContentEventAsync(string eventType, Guid itemId, object? eventData = null)
        {
            var contentEvent = new
            {
                ItemId = itemId,
                EventType = eventType,
                Data = eventData
            };

            return await SendEventAsync($"content_{eventType}", contentEvent);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    _producer?.Flush(TimeSpan.FromSeconds(10));
                    _producer?.Dispose();
                    _logger.LogInformation("Kafka producer disposed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing Kafka producer");
                }
                finally
                {
                    _disposed = true;
                }
            }
        }
    }

    public class KafkaEventEnvelope
    {
        public string EventType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public object? Data { get; set; }
    }
}