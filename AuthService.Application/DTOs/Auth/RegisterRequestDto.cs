using AuthService.Application.DTOs.Common;
using MediatR;
using System.ComponentModel.DataAnnotations;

namespace AuthService.Application.DTOs.Auth
{
    public class RegisterRequestDto
    {
        [Required]
        public int UserId { get; set; }


        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [MinLength(8)]
        public string Password { get; set; } = "";

        // Phone number is OPTIONAL - database allows NULL
        [Phone]
        [StringLength(16, MinimumLength = 10, ErrorMessage = "Phone number length must be between 10 and 15 digits (plus optional '+').")]  
        [RegularExpression(@"^\+?[0-9]{10,15}$", ErrorMessage = "Invalid phone number format. Use only digits, optionally starting with '+'.")]
        public string? PhoneNumber { get; set; }
        
        public int? ReferredBy { get; set; }

        [Required(ErrorMessage = "Client IP address is required")]
        [RegularExpression(@"^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$",
            ErrorMessage = "Invalid IP address format. Use valid IPv4 format (e.g., 127.0.0.1)")]
        public string ClientIp { get; set; } = "0.0.0.0";

    }
}