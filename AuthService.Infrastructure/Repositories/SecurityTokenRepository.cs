using System;
using System.Threading.Tasks;
using Dapper;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using AuthService.Infrastructure.Data.Interfaces;
using AuthService.Infrastructure.Interfaces;
using Newtonsoft.Json;

namespace AuthService.Infrastructure.Repositories
{
    public class SecurityTokenRepository : BaseRepository, ISecurityTokenRepository
    {
        public SecurityTokenRepository(IDbConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        public async Task<SecurityToken> GetByTokenHashAsync(string tokenHash)
        {
            const string sql = @"
                SELECT 
                    token_id as TokenId,
                    user_id as UserId,
                    token_type as TokenType,
                    token_hash as TokenHash,
                    expires_at as ExpiresAt,
                    used_at as UsedAt,
                    verification_status as VerificationStatus,
                    is_active as IsActive,
                    metadata as MetadataJson,
                    created_ip as CreatedIp,
                    created_at as CreatedAt
                FROM auth.security_tokens 
                WHERE token_hash = @TokenHash
                AND verification_status = 'pending'
                AND is_active = true
                AND expires_at > CURRENT_TIMESTAMP";

            var token = await ExecuteAsync<SecurityToken>(sql, new { TokenHash = tokenHash });

            if (token != null && !string.IsNullOrEmpty(token.MetadataJson))
            {
                token.Metadata = JsonConvert.DeserializeObject<Dictionary<string, object>>(token.MetadataJson);
            }

            return token;
        }

        public async Task<SecurityToken> CreateAsync(SecurityToken token)
        {
            const string sql = @"
                INSERT INTO auth.security_tokens (
                    user_id, token_type, token_hash, token_plain,
                    expires_at, metadata, created_ip, verification_status, is_active
                ) VALUES (
                    @UserId, @TokenType, @TokenHash, @TokenPlain,
                    @ExpiresAt, @Metadata::jsonb, @CreatedIp::inet, 'pending', true
                ) RETURNING 
                    token_id as TokenId,
                    created_at as CreatedAt";

            var parameters = new
            {
                token.UserId,
                TokenType = token.TokenType.ToString(),
                token.TokenHash,
                token.TokenPlain,
                token.ExpiresAt,
                Metadata = JsonConvert.SerializeObject(token.Metadata ?? new Dictionary<string, object>()),
                token.CreatedIp
            };

            var result = await ExecuteAsync<SecurityToken>(sql, parameters);
            token.TokenId = result.TokenId;
            token.CreatedAt = result.CreatedAt;

            return token;
        }

        public async Task<bool> UpdateAsync(SecurityToken token)
        {
            const string sql = @"
                UPDATE auth.security_tokens 
                SET used_at = @UsedAt,
                    verification_status = @VerificationStatus,
                    is_active = @IsActive
                WHERE token_id = @TokenId";

            var affected = await ExecuteCommandAsync(sql, new
            {
                token.TokenId,
                token.UsedAt,
                VerificationStatus = token.VerificationStatus.ToString(),
                token.IsActive
            });

            return affected > 0;
        }

        public async Task<bool> MarkAsUsedAsync(int tokenId)
        {
            const string sql = @"
                UPDATE auth.security_tokens 
                SET used_at = CURRENT_TIMESTAMP,
                    verification_status = 'verified'
                WHERE token_id = @TokenId";

            var affected = await ExecuteCommandAsync(sql, new { TokenId = tokenId });
            return affected > 0;
        }
    }
}