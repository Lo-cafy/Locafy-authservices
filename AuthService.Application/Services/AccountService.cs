using AuthService.Application.DTOs.Account;
using AuthService.Application.DTOs.Auth;
using AuthService.Application.Exceptions;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using AuthService.Infrastructure.Interfaces;
using AuthService.Infrastructure.Repositories;  
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions; 
using System.Threading.Tasks;
using Npgsql;  

namespace AuthService.Application.Services
{
    public class AccountService : IAccountService
    {
        private readonly IUserCredentialRepository _credentialRepository;
        private readonly ISecurityTokenRepository _tokenRepository;
        private readonly IPasswordService _passwordService;
        private readonly ILogger<AccountService> _logger;
        private readonly EmailService.Grpc.EmailService.EmailServiceClient _emailServiceClient;
        private readonly IConfiguration _configuration;

        // Phone number validation regex (matching DB constraint ^\+?[0-9]{10,15}$)
        private static readonly Regex _phoneRegex = new Regex(@"^\+?[0-9]{10,15}$", RegexOptions.Compiled);


        public AccountService(
            IUserCredentialRepository credentialRepository,
            ISecurityTokenRepository tokenRepository,
            IPasswordService passwordService,
            ILogger<AccountService> logger,
            EmailService.Grpc.EmailService.EmailServiceClient emailServiceClient,
            IConfiguration configuration)
        {
            _credentialRepository = credentialRepository;
            _tokenRepository = tokenRepository;
            _passwordService = passwordService;
            _logger = logger;
            _emailServiceClient = emailServiceClient;
            _configuration = configuration;
        }

         
        public async Task<bool> RequestPasswordResetAsync(string email)
        {
       try
            {
                var credential = await _credentialRepository.GetByEmailAsync(email);
                if (credential == null)
                {
                    _logger.LogInformation("Password reset requested for non-existent email: {Email}", email);
                    return true;
                }

                var resetToken = GenerateSecureToken();
                var tokenHash = HashToken(resetToken);

                var securityToken = new SecurityToken
                {
                    UserId = credential.UserId,
                    TokenType = TokenTypeEnum.ResetPassword,
                    TokenHash = tokenHash,
                    TokenPlain = resetToken,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    Metadata = new Dictionary<string, object>()
                };

                await _tokenRepository.CreateAsync(securityToken);
                _logger.LogInformation("Password reset token generated for user: {UserId}", credential.UserId);

                try
                {
                    var clientAppUrl = _configuration["AppSettings:ClientAppUrl"];
                    if (string.IsNullOrEmpty(clientAppUrl))
                    {
                        _logger.LogError("ClientAppUrl is not configured in appsettings.json under AppSettings:ClientAppUrl");
                        return false;
                    }

                    var resetLink = $"{clientAppUrl}/reset-password?token={resetToken}";

                    var emailRequest = new EmailService.Grpc.SendEmailRequest
                    {
                        ToEmail = email,
                        Subject = "Reset Your Password",
                        ViewName = "PasswordReset",
                        ModelJson = System.Text.Json.JsonSerializer.Serialize(new { ResetLink = resetLink })
                    };

                    await _emailServiceClient.SendEmailAsync(emailRequest);
                    _logger.LogInformation("Password reset email sent to: {Email}", email);
                }
                catch (Grpc.Core.RpcException grpcEx)
                {
                    _logger.LogError(grpcEx, "Failed to send password reset email via gRPC to {Email}. Status: {StatusCode}", email, grpcEx.StatusCode);
                    return false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send password reset email to {Email}", email);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Password reset request process failed for {Email}", email);
                return false;
            }
        }

        public async Task<bool> ResetPasswordAsync(ResetPasswordDto request)
        {
           try
            {
                var tokenHash = HashToken(request.Token);
                var token = await _tokenRepository.GetByTokenHashAsync(tokenHash);

                const string invalidTokenMsg = "Invalid or expired reset token.";
                const string usedTokenMsg = "Reset token has already been used.";
                const string userNotFoundMsg = "User account not found for this token.";

                if (token == null || token.TokenType != TokenTypeEnum.ResetPassword || !token.IsActive)
                {
                    throw new ValidationException(invalidTokenMsg);
                }
                if (token.ExpiresAt < DateTime.UtcNow)
                {
                    throw new ValidationException(invalidTokenMsg);
                }
                if (token.UsedAt.HasValue)
                {
                    throw new ValidationException(usedTokenMsg);
                }

                _passwordService.ValidatePasswordStrength(request.NewPassword);

                var credential = await _credentialRepository.GetByUserIdAsync((int)token.UserId);
                if (credential == null)
                {
                    throw new ValidationException(userNotFoundMsg);
                }

                var passwordSalt = GeneratePasswordSalt();
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword + passwordSalt, 12);

                credential.PasswordHash = passwordHash;
                credential.PasswordSalt = passwordSalt;
                credential.PasswordChangedAt = DateTime.UtcNow;
                credential.FailedAttempts = 0;
                credential.LockedUntil = null;

                await _credentialRepository.UpdateAsync(credential);

                await _tokenRepository.MarkAsUsedAsync(token.TokenId);

                _logger.LogInformation("Password reset completed successfully for user: {UserId}", token.UserId);
                return true;
            }
            catch (ValidationException vex)
            {
                _logger.LogWarning("Password reset validation failed: {ErrorMessage}", vex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during password reset.");
                return false;
            }
        }

        public async Task<bool> ChangePasswordAsync(ChangePasswordDto request)
        {
           try
            {
                var credential = await _credentialRepository.GetByUserIdAsync(request.UserId);
                if (credential == null)
                {
                    throw new ValidationException("User not found");
                }

                var currentPasswordWithSalt = request.CurrentPassword + credential.PasswordSalt;
                if (!BCrypt.Net.BCrypt.Verify(currentPasswordWithSalt, credential.PasswordHash))
                {
                    throw new ValidationException("Current password is incorrect");
                }

                _passwordService.ValidatePasswordStrength(request.NewPassword);

                var newPasswordSalt = GeneratePasswordSalt();
                var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword + newPasswordSalt, 12);

                credential.PasswordHash = newPasswordHash;
                credential.PasswordSalt = newPasswordSalt;
                credential.PasswordChangedAt = DateTime.UtcNow;

                await _credentialRepository.UpdateAsync(credential);

                _logger.LogInformation("Password changed successfully for user: {UserId}", request.UserId);
                return true;
            }
            catch (ValidationException vex)
            {
                _logger.LogWarning("Password change validation failed for UserId {UserId}: {ErrorMessage}", request.UserId, vex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Password change failed unexpectedly for user: {UserId}", request.UserId);
                return false;
            }
        }

      public async Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto request)
        {
     try
            {
                if (await _credentialRepository.EmailExistsAsync(request.Email))
                {
                    return new RegisterResponseDto { Success = false, Message = "Email already exists" };
                }

                _passwordService.ValidatePasswordStrength(request.Password);

                var passwordSalt = GeneratePasswordSalt();
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password + passwordSalt, 12);

                var userCredential = new UserCredential
                {
                    UserId = request.UserId,
                    Email = request.Email,
                    PasswordHash = passwordHash,
                    PasswordSalt = passwordSalt,
                    Role = RoleType.Customer,  
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var created = await _credentialRepository.CreateAsync(userCredential);

                _logger.LogInformation("User credential created via RegisterAsync for Email {Email}", request.Email);

                return new RegisterResponseDto
                {
                    Success = true,
                    Message = "User registered successfully",
                    UserId = created.UserId,
                    CredentialId = created.CredentialId
                };
            }
            catch (ValidationException vex)
            {
                _logger.LogWarning("User registration (RegisterAsync) validation failed: {ErrorMessage}", vex.Message);
                return new RegisterResponseDto { Success = false, Message = vex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "User registration (RegisterAsync) failed unexpectedly for {Email}", request.Email);
                return new RegisterResponseDto { Success = false, Message = "An internal error occurred." };
            }
        }

   public async Task<RegisterResponseDto> RegisterGrpcAsync(RegisterRequestDto request)
        {
            try
            {
                var passwordSalt = GeneratePasswordSalt();
                var passwordHash = request.Password;

                var result = await _credentialRepository.RegisterUserEnhancedAsync(
                    userId: request.UserId,
                    email: request.Email,
                    passwordHash: passwordHash,
                    passwordSalt: passwordSalt,
                    role: "customer",
                    phoneNumber: request.PhoneNumber,
                    referredBy: request.ReferredBy,
                    createdIp: request.ClientIp
                );

                _logger.LogInformation("User registered successfully with Email {Email}", request.Email);

                return new RegisterResponseDto
                {
                    Success = true,
                    Message = "User registered successfully",
                    UserId = result.UserId,
                    CredentialId = result.CredentialId  
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "User registration failed for {Email}", request.Email);
                return new RegisterResponseDto { Success = false, Message = "Internal server error" };
            }
        }

         

        private string GenerateSecureToken()
        {
            var randomBytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(randomBytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        private string GeneratePasswordSalt()
        {
            var saltBytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(saltBytes);
        }

        private string HashToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return string.Empty;
            var hashedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(hashedBytes).ToLowerInvariant();
        }
    }
}