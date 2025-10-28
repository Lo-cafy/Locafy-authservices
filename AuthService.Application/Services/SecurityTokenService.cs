using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using AuthService.Infrastructure.Interfaces;

namespace AuthService.Application.Services
{
    public class SecurityTokenService : ISecurityTokenService
    {
        private readonly ISecurityTokenRepository _tokenRepository;

        public SecurityTokenService(ISecurityTokenRepository tokenRepository)
        {
            _tokenRepository = tokenRepository;
        }

        public async Task<string> GenerateTokenAsync(int userId, TokenTypeEnum tokenType)
        {
            var token = GenerateSecureToken();
            var tokenHash = HashToken(token);

            var securityToken = new SecurityToken
            {
                TokenId = new int(),
                UserId = userId,
                TokenType = tokenType,
                TokenHash = tokenHash,
                ExpiresAt = GetExpirationTime(tokenType),
                CreatedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>()
            };

            await _tokenRepository.CreateAsync(securityToken);
            return token;
        }

        public async Task<bool> ValidateTokenAsync(string token, TokenTypeEnum tokenType)
        {
            var tokenHash = HashToken(token);
            var securityToken = await _tokenRepository.GetByTokenHashAsync(tokenHash);

            if (securityToken == null || securityToken.TokenType != tokenType)
                return false;

            if (securityToken.ExpiresAt < DateTime.UtcNow)
                return false;

            if (securityToken.UsedAt.HasValue)
                return false;

            return true;
        }

        public async Task<bool> RevokeTokenAsync(string token)
        {
            var tokenHash = HashToken(token);
            var securityToken = await _tokenRepository.GetByTokenHashAsync(tokenHash);

            if (securityToken == null)
                return false;

            securityToken.UsedAt = DateTime.UtcNow;
            await _tokenRepository.UpdateAsync(securityToken);
            return true;
        }

        private string GenerateSecureToken()
        {
            var randomBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return Convert.ToBase64String(randomBytes);
        }

        private string HashToken(string token)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token));
                return Convert.ToBase64String(hashBytes);
            }
        }

        private DateTime GetExpirationTime(TokenTypeEnum tokenType)
        {
            return tokenType switch
            {
                TokenTypeEnum.EmailVerification => DateTime.UtcNow.AddDays(1),
                TokenTypeEnum.ResetPassword => DateTime.UtcNow.AddHours(1),
                _ => DateTime.UtcNow.AddHours(24)
            };
        }
    }
}