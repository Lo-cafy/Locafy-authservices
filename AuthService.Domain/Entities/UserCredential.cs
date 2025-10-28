// AuthService.Domain/Entities/UserCredential.cs
using AuthService.Domain.Enums;
using System;
using System.Text.Json.Serialization; // Added

namespace AuthService.Domain.Entities
{
    public class UserCredential
    {
        public int CredentialId { get; set; } // Changed to int
        public int UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string PasswordSalt { get; set; } = string.Empty;
        public RoleType Role { get; set; }  
        public bool IsActive { get; set; } = true;
        public int FailedAttempts { get; set; } = 0;
        public DateTime? LockedUntil { get; set; }
        public DateTime? PasswordChangedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? PasswordAlgorithm { get; set; }
        public int? PasswordIterations { get; set; }
    }

    public class LoginResult
    {
        public bool Success { get; set; }

        [JsonPropertyName("user_id")] // Correct attribute
        public int UserId { get; set; }
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("email_verified")] // Correct attribute
        public bool EmailVerified { get; set; }

        public string Role { get; set; } = "Customer";
        public string? Message { get; set; }
        public int? Code { get; set; }
        public string? Error { get; set; }
    }
} // Added missing closing brace