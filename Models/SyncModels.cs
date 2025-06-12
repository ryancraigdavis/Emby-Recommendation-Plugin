using System;
using System.Collections.Generic;

namespace Emby.Recommendation.Plugin.Models
{
    public class UserSyncData
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public IEnumerable<UserWatchData> WatchHistory { get; set; } = new List<UserWatchData>();
        public IEnumerable<UserRating> Ratings { get; set; } = new List<UserRating>();
        public DateTime LastSyncTime { get; set; }
    }

    public class UserWatchData
    {
        public Guid ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public int? TmdbId { get; set; }
        public int? TvdbId { get; set; }
        public DateTime? LastPlayedDate { get; set; }
        public long? PlaybackPositionTicks { get; set; }
        public int PlayCount { get; set; }
        public bool IsFavorite { get; set; }
        public double? UserRating { get; set; }
    }

    public class UserRating
    {
        public Guid ItemId { get; set; }
        public double Rating { get; set; }
        public DateTime RatedAt { get; set; }
    }

    public class ContentMetadata
    {
        public Guid ItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public int? TmdbId { get; set; }
        public int? TvdbId { get; set; }
        public string? ImdbId { get; set; }
        public DateTime? PremiereDate { get; set; }
        public IEnumerable<string> Genres { get; set; } = new List<string>();
        public IEnumerable<string> Tags { get; set; } = new List<string>();
        public IEnumerable<string> Studios { get; set; } = new List<string>();
        public string? Overview { get; set; }
        public float? CommunityRating { get; set; }
        public string? OfficialRating { get; set; }
        public long? RunTimeTicks { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateModified { get; set; }
    }

    public class WatchEvent
    {
        public Guid UserId { get; set; }
        public Guid ItemId { get; set; }
        public string EventType { get; set; } = string.Empty; // "play_start", "play_stop", "pause", "resume"
        public DateTime Timestamp { get; set; }
        public long? PlaybackPositionTicks { get; set; }
        public string? DeviceId { get; set; }
        public string? DeviceName { get; set; }
        public string? ClientName { get; set; }
    }

    public class RecommendationResult
    {
        public Guid ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public int? TmdbId { get; set; }
        public double Score { get; set; }
        public string Reason { get; set; } = string.Empty;
        public IEnumerable<string> Tags { get; set; } = new List<string>();
    }
}