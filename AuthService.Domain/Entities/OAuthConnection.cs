using System;
using System.Collections.Generic;

namespace AuthService.Domain.Entities
{
    public class OAuthConnection
    {
        public int ConnectionId { get; set; }
        public int UserId { get; set; }
        public int ProviderId { get; set; }
        public string ProviderUserId { get; set; } = string.Empty;
        public string? ProviderEmail { get; set; }
        public Dictionary<string, object>? ProviderData { get; set; }
        public string? ProviderDataJson { get; set; }
        public string? AccessTokenEncrypted { get; set; }
        public string? RefreshTokenEncrypted { get; set; }
        public DateTime? TokenExpiresAt { get; set; }
        public bool IsPrimary { get; set; }
        public DateTime ConnectedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public bool IsActive { get; set; } = true;
        public virtual OAuthProvider? Provider { get; set; }
    }
}