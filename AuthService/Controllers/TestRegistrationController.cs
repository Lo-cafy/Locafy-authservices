using AuthService.Application.DTOs.Auth;
using AuthService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace AuthService.Controllers
{
    /// <summary>
    /// Test controller for validating user registration with phone number validation.
    /// This controller is for testing the phone validation fix and can be removed in production.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class TestRegistrationController : ControllerBase
    {
        private readonly IAccountService _accountService;
        private readonly ILogger<TestRegistrationController> _logger;

        public TestRegistrationController(
            IAccountService accountService,
            ILogger<TestRegistrationController> logger)
        {
            _accountService = accountService;
            _logger = logger;
        }
        [HttpPost]
        public async Task<IActionResult> TestRegister([FromBody] RegisterRequestDto request)
        {
            // Sanitize ClientIp to prevent common mistakes
            if (string.IsNullOrWhiteSpace(request.ClientIp) || 
                request.ClientIp.Equals("string", StringComparison.OrdinalIgnoreCase) ||
                request.ClientIp.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Invalid ClientIp value '{ClientIp}' provided, using fallback 0.0.0.0", request.ClientIp);
                request.ClientIp = "0.0.0.0";
            }

            _logger.LogInformation(
                "Test registration request received for Email: {Email}, PhoneNumber: '{PhoneNumber}', UserId: {UserId}, ClientIp: '{ClientIp}'",
                request.Email,
                request.PhoneNumber ?? "(null)",
                request.UserId,
                request.ClientIp
            );

            try
            {
                // Call the registration service
                var response = await _accountService.RegisterGrpcAsync(request);

                // Log the response
                if (response.Success)
                {
                    _logger.LogInformation(
                        "Test registration succeeded for Email: {Email}, UserId: {UserId}, CredentialId: {CredentialId}",
                        request.Email,
                        response.UserId,
                        response.CredentialId
                    );
                    return Ok(response);
                }
                else
                {
                    _logger.LogWarning(
                        "Test registration failed for Email: {Email}. Reason: {Message}",
                        request.Email,
                        response.Message
                    );
                    return BadRequest(response);
                }
            }
            catch (PostgresException pgEx) when (pgEx.SqlState == "22P02")
            {
                _logger.LogError(
                    pgEx,
                    "PostgreSQL inet type error for Email: {Email}, ClientIp: '{ClientIp}'",
                    request.Email,
                    request.ClientIp
                );
                
                return BadRequest(new
                {
                    success = false,
                    message = $"Invalid IP address format: '{request.ClientIp}'. Use valid IPv4 format like 127.0.0.1",
                    error = "INVALID_IP_FORMAT",
                    detail = pgEx.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error during test registration for Email: {Email}",
                    request.Email
                );

                return StatusCode(500, new
                {
                    success = false,
                    message = "An unexpected error occurred during registration.",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Health check endpoint to verify the test controller is working.
        /// GET: api/test-registration/health
        /// </summary>
        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            _logger.LogDebug("Test registration controller health check called");
            return Ok(new
            {
                status = "healthy",
                controller = "TestRegistrationController",
                timestamp = DateTime.UtcNow,
                message = "Test controller is ready for phone validation testing"
            });
        }

        /// <summary>
        /// Get test scenarios for phone validation.
        /// GET: api/test-registration/scenarios
        /// </summary>
        [HttpGet("scenarios")]
        public IActionResult GetTestScenarios()
        {
            var scenarios = new[]
            {
                new
                {
                    scenario = "NULL Phone Number",
                    phoneNumber = (string?)null,
                    expectedResult = "Success",
                    description = "Phone number is null - should be stored as NULL in database"
                },
                new
                {
                    scenario = "Empty String Phone",
                    phoneNumber = "",
                    expectedResult = "Success",
                    description = "Empty string should be treated as NULL"
                },
                new
                {
                    scenario = "Whitespace Only Phone",
                    phoneNumber = "   ",
                    expectedResult = "Success",
                    description = "Whitespace should be treated as NULL"
                },
                new
                {
                    scenario = "Valid International Phone",
                    phoneNumber = "+1 (555) 123-4567",
                    expectedResult = "Success",
                    description = "Valid international format - should be cleaned to +15551234567"
                },
                new
                {
                    scenario = "Valid Local Phone",
                    phoneNumber = "9876543210",
                    expectedResult = "Success",
                    description = "Valid 10-digit local number"
                },
                new
                {
                    scenario = "Invalid Phone - Too Short",
                    phoneNumber = "12345",
                    expectedResult = "Fail",
                    description = "Less than 10 digits - should be rejected by C# validation"
                },
                new
                {
                    scenario = "Invalid Phone - Contains Letters",
                    phoneNumber = "abc-defg-hijk",
                    expectedResult = "Fail",
                    description = "Non-numeric characters - should be rejected"
                },
                new
                {
                    scenario = "Invalid Phone - Too Long",
                    phoneNumber = "12345678901234567890",
                    expectedResult = "Fail",
                    description = "More than 15 digits - should be rejected"
                }
            };

            _logger.LogInformation("Test scenarios retrieved - {Count} scenarios available", scenarios.Length);

            return Ok(new
            {
                totalScenarios = scenarios.Length,
                scenarios,
                documentation = "See TESTING_GUIDE.md for detailed test instructions"
            });
        }
    }
}
