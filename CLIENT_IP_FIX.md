# PostgreSQL Error 22P02 - Invalid inet Input Fix

## ❌ The Error

```
PostgreSQL error 22P02: invalid input syntax for type inet: "string"
```

**Location:** `auth.register_user_enhanced()` function  
**Parameter:** `p_created_ip` (type: `inet`)  
**Problem:** Receiving literal string `"string"` instead of valid IP address

---

## 🔍 Root Cause

Someone is sending a test request with:

```json
{
  "userId": 1001,
  "email": "test@example.com",
  "password": "SecurePass123!",
  "phoneNumber": null,
  "clientIp": "string"  // ❌ WRONG! This is a literal string, not a placeholder
}
```

PostgreSQL expects an **actual IP address** like `"127.0.0.1"`, not the word `"string"`.

---

## ✅ Quick Fix - Update Your Test Request

### ❌ WRONG Request
```json
{
  "userId": 1001,
  "email": "test@example.com",
  "password": "SecurePass123!",
  "phoneNumber": null,
  "clientIp": "string"  // ❌ This causes the error!
}
```

### ✅ CORRECT Request
```json
{
  "userId": 1001,
  "email": "test@example.com",
  "password": "SecurePass123!",
  "phoneNumber": null,
  "clientIp": "127.0.0.1"  // ✅ Valid IP address
}
```

---

## 🛠️ Permanent Fix - Add Validation

### 1. Update RegisterRequestDto.cs

Add IP address validation to prevent this error:

```csharp
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

    [Phone]
    [StringLength(16, MinimumLength = 10)]
    [RegularExpression(@"^\+?[0-9]{10,15}$")]
    public string? PhoneNumber { get; set; }
    
    public int? ReferredBy { get; set; }

    // ✅ ADD VALIDATION HERE
    [Required(ErrorMessage = "Client IP address is required")]
    [RegularExpression(@"^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$|^(?:[0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}$",
        ErrorMessage = "Invalid IP address format. Use IPv4 (e.g., 127.0.0.1) or IPv6 format")]
    public string ClientIp { get; set; } = "0.0.0.0";  // ✅ Default to valid IP
}
```

### 2. Add Fallback in TestRegistrationController

Add IP validation/fallback in the controller:

```csharp
[HttpPost]
public async Task<IActionResult> TestRegister([FromBody] RegisterRequestDto request)
{
    _logger.LogInformation(
        "Test registration request received for Email: {Email}, PhoneNumber: '{PhoneNumber}', UserId: {UserId}, ClientIp: '{ClientIp}'",
        request.Email,
        request.PhoneNumber ?? "(null)",
        request.UserId,
        request.ClientIp
    );

    // ✅ ADD THIS: Validate and sanitize ClientIp
    if (string.IsNullOrWhiteSpace(request.ClientIp) || 
        request.ClientIp.Equals("string", StringComparison.OrdinalIgnoreCase))
    {
        _logger.LogWarning("Invalid or missing ClientIp '{ClientIp}', defaulting to 0.0.0.0", request.ClientIp);
        request.ClientIp = "0.0.0.0";  // Fallback to valid IP
    }

    try
    {
        var response = await _accountService.RegisterGrpcAsync(request);
        // ... rest of the code
    }
    catch (Exception ex)
    {
        // ... error handling
    }
}
```

---

## 📝 Correct Test Examples

### cURL Example
```bash
curl -X POST http://localhost:5000/api/test-registration \
  -H "Content-Type: application/json" \
  -d '{
    "userId": 1001,
    "email": "test@example.com",
    "password": "SecurePass123!",
    "phoneNumber": null,
    "clientIp": "127.0.0.1"
  }'
```

### PowerShell Example
```powershell
$body = @{
    userId = 1001
    email = "test@example.com"
    password = "SecurePass123!"
    phoneNumber = $null
    clientIp = "127.0.0.1"  # ✅ Valid IP
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/api/test-registration" `
    -Method POST `
    -Body $body `
    -ContentType "application/json"
```

### Postman Example

**Body (raw JSON):**
```json
{
  "userId": 1001,
  "email": "test@example.com",
  "password": "SecurePass123!",
  "phoneNumber": null,
  "clientIp": "127.0.0.1"
}
```

---

## 🎯 Valid ClientIp Values

### For Testing
```json
"clientIp": "127.0.0.1"     // ✅ Localhost
"clientIp": "0.0.0.0"       // ✅ Default/unknown
"clientIp": "192.168.1.100" // ✅ Private network
"clientIp": "10.0.0.50"     // ✅ Private network
```

### In Production (Get Real IP)

If you want to get the actual client IP from the request:

```csharp
[HttpPost]
public async Task<IActionResult> TestRegister([FromBody] RegisterRequestDto request)
{
    // Get real client IP from request context
    var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
    
    // Override the DTO value with real IP
    request.ClientIp = clientIp;
    
    _logger.LogInformation("Registration from IP: {ClientIp}", clientIp);
    
    var response = await _accountService.RegisterGrpcAsync(request);
    // ...
}
```

---

## 🚫 Common Mistakes

### ❌ DON'T Use These Values
```json
"clientIp": "string"           // ❌ Literal text
"clientIp": ""                 // ❌ Empty string
"clientIp": "your-ip-here"     // ❌ Placeholder text
"clientIp": "localhost"        // ❌ Hostname (use 127.0.0.1)
"clientIp": "192.168.1"        // ❌ Incomplete IP
"clientIp": "999.999.999.999"  // ❌ Invalid octets
```

### ✅ DO Use These Values
```json
"clientIp": "127.0.0.1"        // ✅ Valid IPv4
"clientIp": "0.0.0.0"          // ✅ Valid default
"clientIp": "192.168.1.100"    // ✅ Valid private IP
"clientIp": "::1"              // ✅ Valid IPv6 (localhost)
```

---

## 🔧 Complete Fixed Code

### RegisterRequestDto.cs
```csharp
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

        [Phone]
        [StringLength(16, MinimumLength = 10, ErrorMessage = "Phone number length must be between 10 and 15 digits.")]  
        [RegularExpression(@"^\+?[0-9]{10,15}$", ErrorMessage = "Invalid phone number format.")]
        public string? PhoneNumber { get; set; }
        
        public int? ReferredBy { get; set; }

        [Required(ErrorMessage = "Client IP address is required")]
        [RegularExpression(@"^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$",
            ErrorMessage = "Invalid IP address format. Use valid IPv4 format (e.g., 127.0.0.1)")]
        public string ClientIp { get; set; } = "0.0.0.0";
    }
}
```

### TestRegistrationController.cs (with safeguard)
```csharp
[HttpPost]
public async Task<IActionResult> TestRegister([FromBody] RegisterRequestDto request)
{
    // Sanitize ClientIp to prevent common mistakes
    if (string.IsNullOrWhiteSpace(request.ClientIp) || 
        request.ClientIp.Equals("string", StringComparison.OrdinalIgnoreCase) ||
        request.ClientIp.Equals("localhost", StringComparison.OrdinalIgnoreCase))
    {
        _logger.LogWarning("Invalid ClientIp value '{ClientIp}' provided, using fallback", request.ClientIp);
        request.ClientIp = "0.0.0.0";
    }

    _logger.LogInformation(
        "Test registration request received for Email: {Email}, ClientIp: {ClientIp}",
        request.Email,
        request.ClientIp
    );

    try
    {
        var response = await _accountService.RegisterGrpcAsync(request);

        if (response.Success)
        {
            _logger.LogInformation(
                "Test registration succeeded for Email: {Email}, UserId: {UserId}",
                request.Email,
                response.UserId
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
    catch (Npgsql.PostgresException pgEx) when (pgEx.SqlState == "22P02")
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
            error = "INVALID_IP_FORMAT"
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
```

---

## 📊 Testing Checklist

After applying the fixes:

- [ ] Update `RegisterRequestDto.cs` with IP validation
- [ ] Update `TestRegistrationController.cs` with sanitization
- [ ] Rebuild the solution: `dotnet build`
- [ ] Test with valid IP: `"clientIp": "127.0.0.1"` ✅
- [ ] Test with invalid IP: `"clientIp": "string"` → Should get clear error message
- [ ] Verify database receives valid `inet` value

---

## 🎯 Expected Behavior

### Before Fix ❌
```
Request: {"clientIp": "string"}
PostgreSQL Error: 22P02: invalid input syntax for type inet: "string"
```

### After Fix ✅
```
Request: {"clientIp": "string"}
Controller: Sanitizes to "0.0.0.0"
PostgreSQL: Receives valid inet value
Response: Success with warning logged
```

Or with validation:
```
Request: {"clientIp": "string"}
Validation: Fails at model binding
Response: 400 Bad Request - "Invalid IP address format"
```

---

## 📖 Summary

**The Problem:** Test requests are using `"clientIp": "string"` (literal text) instead of a valid IP address.

**The Solution:**
1. ✅ Use `"clientIp": "127.0.0.1"` in test requests
2. ✅ Add IP validation to DTO
3. ✅ Add sanitization fallback in controller
4. ✅ Update all test documentation

**Valid Test IP:** `127.0.0.1` or `0.0.0.0`

**Your tests should now work!** 🚀
