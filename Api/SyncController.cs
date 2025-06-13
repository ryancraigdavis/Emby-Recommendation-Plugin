using System;
using System.Threading.Tasks;
using Emby.Recommendation.Plugin.Services;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;
using Microsoft.Extensions.Logging;

namespace Emby.Recommendation.Plugin.Api
{
    [Route("/Plugins/RecommendationPlugin/Sync", "POST", Summary = "Trigger manual sync of all data")]
    [Route("/Plugins/RecommendationPlugin/Sync/Users", "POST", Summary = "Sync all users")]
    [Route("/Plugins/RecommendationPlugin/Sync/Content", "POST", Summary = "Sync content library")]
    [Route("/Plugins/RecommendationPlugin/Test", "GET", Summary = "Test connection to recommendation service")]
    [Authenticated(Roles = "Admin")]
    public class SyncController : IService
    {
        private readonly ISyncService _syncService;
        private readonly IRecommendationApiService _apiService;
        private readonly ILogger<SyncController> _logger;

        public SyncController(
            ISyncService syncService,
            IRecommendationApiService apiService,
            ILogger<SyncController> logger)
        {
            _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<SyncResponse> Post(SyncRequest request)
        {
            try
            {
                _logger.LogInformation("Manual sync triggered");

                var userSyncTask = _syncService.SyncAllUsersAsync();
                var contentSyncTask = _syncService.SyncContentLibraryAsync();

                await Task.WhenAll(userSyncTask, contentSyncTask);

                var userSuccess = await userSyncTask;
                var contentSuccess = await contentSyncTask;

                if (userSuccess || contentSuccess)
                {
                    await _syncService.UpdateLastSyncTimeAsync();
                    
                    return new SyncResponse
                    {
                        Success = true,
                        Message = $"Sync completed. Users: {(userSuccess ? "Success" : "Failed")}, Content: {(contentSuccess ? "Success" : "Failed")}",
                        LastSyncTime = DateTime.UtcNow
                    };
                }

                return new SyncResponse
                {
                    Success = false,
                    Message = "Sync failed for both users and content",
                    LastSyncTime = await _syncService.GetLastSyncTimeAsync()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during manual sync");
                return new SyncResponse
                {
                    Success = false,
                    Message = $"Sync error: {ex.Message}",
                    LastSyncTime = await _syncService.GetLastSyncTimeAsync()
                };
            }
        }

        public async Task<SyncResponse> Post(SyncUsersRequest request)
        {
            try
            {
                _logger.LogInformation("Manual user sync triggered");

                var success = await _syncService.SyncAllUsersAsync();
                
                if (success)
                {
                    await _syncService.UpdateLastSyncTimeAsync();
                    
                    return new SyncResponse
                    {
                        Success = true,
                        Message = "User sync completed successfully",
                        LastSyncTime = DateTime.UtcNow
                    };
                }

                return new SyncResponse
                {
                    Success = false,
                    Message = "User sync failed",
                    LastSyncTime = await _syncService.GetLastSyncTimeAsync()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during manual user sync");
                return new SyncResponse
                {
                    Success = false,
                    Message = $"User sync error: {ex.Message}",
                    LastSyncTime = await _syncService.GetLastSyncTimeAsync()
                };
            }
        }

        public async Task<SyncResponse> Post(SyncContentRequest request)
        {
            try
            {
                _logger.LogInformation("Manual content sync triggered");

                var success = await _syncService.SyncContentLibraryAsync();
                
                if (success)
                {
                    await _syncService.UpdateLastSyncTimeAsync();
                    
                    return new SyncResponse
                    {
                        Success = true,
                        Message = "Content sync completed successfully",
                        LastSyncTime = DateTime.UtcNow
                    };
                }

                return new SyncResponse
                {
                    Success = false,
                    Message = "Content sync failed",
                    LastSyncTime = await _syncService.GetLastSyncTimeAsync()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during manual content sync");
                return new SyncResponse
                {
                    Success = false,
                    Message = $"Content sync error: {ex.Message}",
                    LastSyncTime = await _syncService.GetLastSyncTimeAsync()
                };
            }
        }

        public async Task<TestConnectionResponse> Get(TestConnectionRequest request)
        {
            try
            {
                _logger.LogInformation("Testing connection to recommendation service");

                var success = await _apiService.TestConnectionAsync();
                
                return new TestConnectionResponse
                {
                    Success = success,
                    Message = success ? "Connection successful" : "Connection failed"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing connection");
                return new TestConnectionResponse
                {
                    Success = false,
                    Message = $"Connection test error: {ex.Message}"
                };
            }
        }
    }

    public class SyncRequest : IReturn<SyncResponse> { }
    public class SyncUsersRequest : IReturn<SyncResponse> { }
    public class SyncContentRequest : IReturn<SyncResponse> { }
    public class TestConnectionRequest : IReturn<TestConnectionResponse> { }

    public class SyncResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime? LastSyncTime { get; set; }
    }

    public class TestConnectionResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}