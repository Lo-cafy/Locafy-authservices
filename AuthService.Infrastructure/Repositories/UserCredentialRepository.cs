using AuthService.Domain.Entities;
using AuthService.Infrastructure.Data.Interfaces;
using AuthService.Infrastructure.Interfaces;
using Dapper;
using System.Data;
using System.Text.Json;
using AuthService.Domain.Enums;
using Npgsql;

namespace AuthService.Infrastructure.Repositories
{
    public class UserCredentialRepository : IUserCredentialRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public UserCredentialRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<UserCredential?> GetByEmailAsync(string email)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            var normalizedEmail = email?.ToLowerInvariant().Trim();

            var sql = @"
                SELECT
                    uc.credential_id AS CredentialId,
                    uc.user_id AS UserId,
                    uc.email AS Email,
                    uc.password_hash AS PasswordHash,
                    uc.password_salt AS PasswordSalt,
                    uc.role::text AS RoleAsString,
                    uc.is_active AS IsActive,
                    uc.failed_attempts AS FailedAttempts,
                    uc.locked_until AS LockedUntil,
                    uc.password_changed_at AS PasswordChangedAt,
                    uc.created_at AS CreatedAt,
                    uc.updated_at AS UpdatedAt,
                    uc.password_algorithm as PasswordAlgorithm,
                    uc.password_iterations as PasswordIterations
                FROM auth.user_credentials uc
                JOIN auth.users u ON uc.user_id = u.user_id
                WHERE u.email_normalized = @NormalizedEmail
                  AND uc.is_active = true
                  AND u.is_deleted = false";

            var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { NormalizedEmail = normalizedEmail });

            if (result == null) return null;

            var credential = new UserCredential
            {
                CredentialId = result.credentialid,
                UserId = result.userid,
                Email = result.email,
                PasswordHash = result.passwordhash,
                PasswordSalt = result.passwordsalt,
                IsActive = result.isactive,
                FailedAttempts = result.failedattempts,
                LockedUntil = result.lockeduntil,
                PasswordChangedAt = result.passwordchangedat,
                CreatedAt = result.createdat,
                UpdatedAt = result.updatedat,
                PasswordAlgorithm = result.passwordalgorithm,
                PasswordIterations = result.passworditerations
            };

            RoleType roleEnum = RoleType.Customer;
            if (!string.IsNullOrEmpty(result.roleasstring) &&
                Enum.TryParse<RoleType>(result.roleasstring, true, out roleEnum))
            {
                credential.Role = roleEnum;
            }
            else
            {
                Console.WriteLine($"Warning: Could not parse role '{result.roleasstring}' for user email {email}.");
                credential.Role = RoleType.Customer; // Default fallback
            }

            return credential;
        }

        public async Task<UserCredential?> GetByUserIdAsync(int userId)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            var sql = @"
                SELECT
                    credential_id AS CredentialId,
                    user_id AS UserId,
                    email AS Email,
                    password_hash AS PasswordHash,
                    password_salt AS PasswordSalt,
                    role::text AS RoleAsString,
                    is_active AS IsActive,
                    failed_attempts AS FailedAttempts,
                    locked_until AS LockedUntil,
                    password_changed_at AS PasswordChangedAt,
                    created_at AS CreatedAt,
                    updated_at AS UpdatedAt,
                    password_algorithm as PasswordAlgorithm,
                    password_iterations as PasswordIterations
                FROM auth.user_credentials
                WHERE user_id = @UserId AND is_active = true";

            var result = await connection.QueryFirstOrDefaultAsync<dynamic>(sql, new { UserId = userId });

            if (result == null) return null;

            var credential = new UserCredential
            {
                CredentialId = result.credentialid,
                UserId = result.userid,
                Email = result.email,
                PasswordHash = result.passwordhash,
                PasswordSalt = result.passwordsalt,
                IsActive = result.isactive,
                FailedAttempts = result.failedattempts,
                LockedUntil = result.lockeduntil,
                PasswordChangedAt = result.passwordchangedat,
                CreatedAt = result.createdat,
                UpdatedAt = result.updatedat,
                PasswordAlgorithm = result.passwordalgorithm,
                PasswordIterations = result.passworditerations
            };

            RoleType roleEnum = RoleType.Customer;
            if (!string.IsNullOrEmpty(result.roleasstring) &&
                Enum.TryParse<RoleType>(result.roleasstring, true, out roleEnum))
            {
                credential.Role = roleEnum;
            }
            else
            {
                credential.Role = RoleType.Customer; // Default fallback
            }

            return credential;
        }

        public async Task<UserCredential> CreateAsync(UserCredential credential)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            var roleString = credential.Role.ToString();

            var sql = @"
                INSERT INTO auth.user_credentials (
                    user_id, email, password_hash, password_salt,
                    password_algorithm, password_iterations,
                    role, created_at, updated_at, is_active
                ) VALUES (
                    @UserId, @Email, @PasswordHash, @PasswordSalt,
                    'bcrypt', 12,
                    @RoleString::auth.role_type_enum,
                    CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, @IsActive
                ) RETURNING credential_id";

            var parameters = new DynamicParameters();
            parameters.Add("UserId", credential.UserId);
            parameters.Add("Email", credential.Email);
            parameters.Add("PasswordHash", credential.PasswordHash);
            parameters.Add("PasswordSalt", credential.PasswordSalt);
            parameters.Add("RoleString", roleString);
            parameters.Add("IsActive", credential.IsActive);

            var credentialId = await connection.ExecuteScalarAsync<int>(sql, parameters);
            credential.CredentialId = credentialId;
            return credential;
        }

        public async Task<(int UserId, int CredentialId)> RegisterUserEnhancedAsync(
            int userId, string email, string passwordHash, string passwordSalt,
            string role, string? phoneNumber, int? referredBy, string createdIp)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            var sql = @"
                SELECT user_id, credential_id
                FROM auth.register_user_enhanced(
                    p_user_id       => @UserId,
                    p_email         => @Email,
                    p_password_hash => @PasswordHash,
                    p_password_salt => @PasswordSalt,
                    p_role          => @Role,
                    p_phone_number  => @PhoneNumber,
                    p_referred_by   => @ReferredBy,
                    p_created_ip    => @CreatedIp::inet -- Cast INET remains in SQL
                );";

            var parameters = new
            {
                UserId = userId,
                Email = email,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                Role = role,
                PhoneNumber = phoneNumber,
                ReferredBy = referredBy,
                CreatedIp = createdIp // Pass string IP
            };
            try
            {
                var result = await connection.QueryFirstAsync<(int UserId, int CredentialId)>(sql, parameters);
                return result;
            }
            catch (PostgresException pex)
            {
                Console.WriteLine($"DB Error in RegisterUserEnhancedAsync Repo Call: {pex.Message} (Code: {pex.SqlState})");
                throw new InfrastructureException($"Database error during registration function call: {pex.Message}", pex);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected Error in RegisterUserEnhancedAsync Repo Call: {ex.Message}");
                throw new InfrastructureException("An unexpected error occurred calling registration function.", ex);
            }
        }

        // Fix: Return type matches interface Task<LoginResult?>
        public async Task<LoginResult?> AuthenticateUserAsync(string email, string password)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            var sql = "SELECT auth.authenticate_user_secure(@Email, @Password)::jsonb";
            var parameters = new { Email = email?.ToLowerInvariant().Trim(), Password = password };

            try
            {
                var resultJson = await connection.QueryFirstOrDefaultAsync<string>(sql, parameters);
                if (string.IsNullOrEmpty(resultJson))
                {
                    Console.WriteLine($"Authentication failed for email {email}: User not found or invalid password.");
                    // Return null or a specific failure LoginResult if preferred by interface contract
                    return null;
                    // return new LoginResult { Success = false, Message = "Invalid credentials.", Code = 401 };
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var loginResult = JsonSerializer.Deserialize<LoginResult>(resultJson, options);

                if (loginResult == null || !loginResult.Success)
                {
                    Console.WriteLine($"Authentication failed for email {email}: Function indicated failure. Message: {loginResult?.Message}");
                    return loginResult ?? new LoginResult { Success = false, Message = "Authentication failed.", Code = 401 };
                }

                return loginResult;
            }
            catch (PostgresException pex)
            {
                Console.WriteLine($"DB Error in AuthenticateUserAsync: {pex.Message} (Code: {pex.SqlState})");
                return new LoginResult { Success = false, Message = "Database error during authentication.", Code = 500 };
            }
            catch (JsonException jsonEx)
            {
                Console.WriteLine($"Error deserializing login result: {jsonEx.Message}");
                return new LoginResult { Success = false, Message = "Error processing login response.", Code = 500 };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected Error in AuthenticateUserAsync: {ex.Message}");
                return new LoginResult { Success = false, Message = "An unexpected error occurred.", Code = 500 };
            }
        }

        public async Task UpdateAsync(UserCredential credential)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            var roleString = credential.Role.ToString();

            // Fix: Cast Role enum and include optional algorithm/iterations
            var sql = @"
                UPDATE auth.user_credentials
                SET
                    password_hash = @PasswordHash,
                    password_salt = @PasswordSalt,
                    role = @RoleString::auth.role_type_enum,
                    is_active = @IsActive,
                    failed_attempts = @FailedAttempts,
                    locked_until = @LockedUntil,
                    password_changed_at = @PasswordChangedAt,
                    password_algorithm = @PasswordAlgorithm,
                    password_iterations = @PasswordIterations,
                    updated_at = CURRENT_TIMESTAMP
                WHERE credential_id = @CredentialId";

            var parameters = new DynamicParameters();
            parameters.AddDynamicParams(credential);
            parameters.Add("RoleString", roleString);
            // Ensure Algorithm and Iterations are set if not null in credential, otherwise provide defaults
            parameters.Add("PasswordAlgorithm", credential.PasswordAlgorithm ?? "bcrypt");
            parameters.Add("PasswordIterations", credential.PasswordIterations ?? 12);

            await connection.ExecuteAsync(sql, parameters);
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            var normalizedEmail = email?.ToLowerInvariant().Trim();

            // Fix: Check auth.users table
            var sql = @"
                SELECT EXISTS(
                    SELECT 1
                    FROM auth.users
                    WHERE email_normalized = @NormalizedEmail
                      AND is_deleted = false
                )";
            return await connection.ExecuteScalarAsync<bool>(sql, new { NormalizedEmail = normalizedEmail });
        }
    }

    public class InfrastructureException : Exception
    {
        public InfrastructureException(string message) : base(message) { }
        public InfrastructureException(string message, Exception innerException) : base(message, innerException) { }
    }
}