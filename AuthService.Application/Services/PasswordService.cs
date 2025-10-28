using System;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AuthService.Application.Interfaces;
using AuthService.Application.Exceptions;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;  

namespace AuthService.Application.Services
{
    public class PasswordService : IPasswordService
    {
        private const int SaltSize = 128 / 8;
        private const int KeySize = 256 / 8;
        private const int Iterations = 10000;
        private static readonly KeyDerivationPrf _prf = KeyDerivationPrf.HMACSHA256;

        public string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = KeyDerivation.Pbkdf2(password, salt, _prf, Iterations, KeySize);
            var hashBytes = new byte[SaltSize + KeySize];
            Buffer.BlockCopy(salt, 0, hashBytes, 0, SaltSize);
            Buffer.BlockCopy(hash, 0, hashBytes, SaltSize, KeySize);
            return Convert.ToBase64String(hashBytes);
        }

        public bool VerifyPassword(string password, string hashedPassword)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword)) return false;
            try
            {
                var hashBytes = Convert.FromBase64String(hashedPassword);
                if (hashBytes.Length != SaltSize + KeySize) return false;
                var salt = new byte[SaltSize];
                Buffer.BlockCopy(hashBytes, 0, salt, 0, SaltSize);
                var hashToCompare = KeyDerivation.Pbkdf2(password, salt, _prf, Iterations, KeySize);
                uint diff = (uint)KeySize ^ (uint)(hashBytes.Length - SaltSize);
                for (int i = 0; i < KeySize; i++)
                {
                    diff |= (uint)(hashBytes[i + SaltSize] ^ hashToCompare[i]);
                }
                return diff == 0;
            }
            catch { return false; }
        }

        public void ValidatePasswordStrength(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ValidationException("Password cannot be empty.");
            if (password.Length < 8)
                throw new ValidationException("Password must be at least 8 characters long.");
            if (!Regex.IsMatch(password, @"[A-Z]"))
                throw new ValidationException("Password must contain at least one uppercase letter.");
            if (!Regex.IsMatch(password, @"[a-z]"))
                throw new ValidationException("Password must contain at least one lowercase letter.");
            if (!Regex.IsMatch(password, @"[0-9]"))
                throw new ValidationException("Password must contain at least one number.");
            if (!Regex.IsMatch(password, @"[!@#$%^&*()_+=\[{\]};:<>|./?,-]"))
                throw new ValidationException("Password must contain at least one special character.");
            var commonPasswords = new[] { "password", "123456", "qwerty", "letmein", "welcome", "admin", "12345678" };
            if (commonPasswords.Any(p => string.Equals(password, p, StringComparison.OrdinalIgnoreCase) || password.ToLowerInvariant().Contains(p)))
                throw new ValidationException("Password is too common or easily guessable.");
        }
    }
}