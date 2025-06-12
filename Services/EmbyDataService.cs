using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Logging;

namespace Emby.Recommendation.Plugin.Services
{
    public interface IEmbyDataService
    {
        Task<IEnumerable<BaseItem>> GetUserLibraryAsync(Guid userId, string? mediaType = null);
        Task<IEnumerable<SessionInfo>> GetActiveSessionsAsync();
        Task<UserItemData?> GetUserItemDataAsync(Guid userId, Guid itemId);
        Task<IEnumerable<BaseItem>> GetRecentlyWatchedAsync(Guid userId, int limit = 50);
        Task<User?> GetUserAsync(Guid userId);
        Task<IEnumerable<User>> GetUsersAsync();
    }

    public class EmbyDataService : IEmbyDataService
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ISessionManager _sessionManager;
        private readonly IUserManager _userManager;
        private readonly ILogger<EmbyDataService> _logger;

        public EmbyDataService(
            ILibraryManager libraryManager,
            ISessionManager sessionManager, 
            IUserManager userManager,
            ILogger<EmbyDataService> logger)
        {
            _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<BaseItem>> GetUserLibraryAsync(Guid userId, string? mediaType = null)
        {
            try
            {
                var user = await GetUserAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User {UserId} not found", userId);
                    return Enumerable.Empty<BaseItem>();
                }

                var query = new InternalItemsQuery(user)
                {
                    Recursive = true,
                    IsFolder = false,
                    HasTmdbId = true
                };

                if (!string.IsNullOrEmpty(mediaType))
                {
                    query.MediaTypes = new[] { mediaType };
                }

                var result = _libraryManager.GetItemsResult(query);
                return result.Items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user library for user {UserId}", userId);
                return Enumerable.Empty<BaseItem>();
            }
        }

        public async Task<IEnumerable<SessionInfo>> GetActiveSessionsAsync()
        {
            try
            {
                return await Task.FromResult(_sessionManager.Sessions.Where(s => s.IsActive));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active sessions");
                return Enumerable.Empty<SessionInfo>();
            }
        }

        public async Task<UserItemData?> GetUserItemDataAsync(Guid userId, Guid itemId)
        {
            try
            {
                var user = await GetUserAsync(userId);
                if (user == null) return null;

                var item = _libraryManager.GetItemById(itemId);
                if (item == null) return null;

                return _libraryManager.GetUserData(user, item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user item data for user {UserId}, item {ItemId}", userId, itemId);
                return null;
            }
        }

        public async Task<IEnumerable<BaseItem>> GetRecentlyWatchedAsync(Guid userId, int limit = 50)
        {
            try
            {
                var user = await GetUserAsync(userId);
                if (user == null) return Enumerable.Empty<BaseItem>();

                var query = new InternalItemsQuery(user)
                {
                    Recursive = true,
                    IsFolder = false,
                    HasTmdbId = true,
                    IsPlayed = true,
                    OrderBy = new[] { (ItemSortBy.DatePlayed, SortOrder.Descending) },
                    Limit = limit
                };

                var result = _libraryManager.GetItemsResult(query);
                return result.Items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recently watched items for user {UserId}", userId);
                return Enumerable.Empty<BaseItem>();
            }
        }

        public async Task<User?> GetUserAsync(Guid userId)
        {
            try
            {
                return await Task.FromResult(_userManager.GetUserById(userId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user {UserId}", userId);
                return null;
            }
        }

        public async Task<IEnumerable<User>> GetUsersAsync()
        {
            try
            {
                return await Task.FromResult(_userManager.Users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users");
                return Enumerable.Empty<User>();
            }
        }
    }
}