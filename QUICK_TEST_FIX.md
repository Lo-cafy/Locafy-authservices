# Quick Fix - Invalid inet Error

## ❌ The Error You're Getting

```
PostgreSQL error 22P02: invalid input syntax for type inet: "string"
```

## ⚠️ The Problem

Your test request has:
```json
{
  "clientIp": "string"  // ❌ WRONG! This is literal text
}
```

## ✅ The Fix (Change ONE Thing)

Use this instead:
```json
{
  "clientIp": "127.0.0.1"  // ✅ CORRECT! Valid IP address
}
```

---

## Complete Correct Request

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

## Test Commands

### cURL
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

### PowerShell
```powershell
$body = @{
    userId = 1001
    email = "test@example.com"
    password = "SecurePass123!"
    phoneNumber = $null
    clientIp = "127.0.0.1"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/api/test-registration" `
    -Method POST -Body $body -ContentType "application/json"
```

### Postman
Set Body to **raw JSON**:
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

## What I Fixed in Your Code

### 1. Added IP Validation (RegisterRequestDto.cs)
```csharp
[Required(ErrorMessage = "Client IP address is required")]
[RegularExpression(@"^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$",
    ErrorMessage = "Invalid IP address format. Use valid IPv4 format (e.g., 127.0.0.1)")]
public string ClientIp { get; set; } = "0.0.0.0";
```

### 2. Added IP Sanitization (TestRegistrationController.cs)
```csharp
// Sanitize ClientIp to prevent common mistakes
if (string.IsNullOrWhiteSpace(request.ClientIp) || 
    request.ClientIp.Equals("string", StringComparison.OrdinalIgnoreCase) ||
    request.ClientIp.Equals("localhost", StringComparison.OrdinalIgnoreCase))
{
    _logger.LogWarning("Invalid ClientIp value '{ClientIp}' provided, using fallback 0.0.0.0", request.ClientIp);
    request.ClientIp = "0.0.0.0";
}
```

### 3. Added Specific Error Handling
```csharp
catch (PostgresException pgEx) when (pgEx.SqlState == "22P02")
{
    return BadRequest(new
    {
        success = false,
        message = $"Invalid IP address format: '{request.ClientIp}'. Use valid IPv4 format like 127.0.0.1",
        error = "INVALID_IP_FORMAT"
    });
}
```

---

## To Apply the Fixes

### Step 1: Stop the Application
```powershell
Get-Process AuthService* -ErrorAction SilentlyContinue | Stop-Process -Force
```

### Step 2: Build
```bash
cd d:\SingleProject1\AuthService\databwseupdate
dotnet clean
dotnet build AuthService.sln
```

### Step 3: Run
```bash
dotnet run --project AuthService\AuthService.Api.csproj
```

### Step 4: Test with Correct IP
```bash
curl -X POST http://localhost:5000/api/test-registration \
  -H "Content-Type: application/json" \
  -d '{"userId":1001,"email":"test@example.com","password":"SecurePass123!","phoneNumber":null,"clientIp":"127.0.0.1"}'
```

---

## Valid ClientIp Values

✅ **USE THESE:**
- `"127.0.0.1"` - Localhost
- `"0.0.0.0"` - Default
- `"192.168.1.100"` - Private IP
- `"10.0.0.50"` - Private IP

❌ **DON'T USE:**
- `"string"` - Literal text
- `""` - Empty string
- `"localhost"` - Hostname
- `"your-ip-here"` - Placeholder text

---

## Summary

**Problem:** `"clientIp": "string"` in your test request  
**Solution:** Change to `"clientIp": "127.0.0.1"`  
**Result:** Registration will work! ✅

That's it! Just change `"string"` to `"127.0.0.1"` in your test request.
