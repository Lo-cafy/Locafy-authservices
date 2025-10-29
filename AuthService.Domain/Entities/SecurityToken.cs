using AuthService.Domain.Enums;
using System;
using System.Collections.Generic;

namespace AuthService.Domain.Entities
{
    public class SecurityToken
    {
        public int TokenId { get; set; }
        public int UserId { get; set; }
        public TokenTypeEnum TokenType { get; set; }
        public string TokenHash { get; set; }
        public string TokenPlain { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? UsedAt { get; set; }
        public VerificationStatus VerificationStatus { get; set; }
        public bool IsActive { get; set; } = true;
        public Dictionary<string, object> Metadata { get; set; }
        public string MetadataJson { get; set; }
        public string CreatedIp { get; set; }
        public DateTime CreatedAt { get; set; }
    }


    public enum VerificationStatus
    {
        Pending,
        Verified,
        Expired,
        Revoked
    }
}