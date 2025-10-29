# Test Registration Controller - Usage Guide

## Overview

The `TestRegistrationController` is a dedicated testing endpoint for validating the phone number validation fix. This controller provides easy-to-use endpoints for testing registration scenarios.

**Location:** `AuthService\Controllers\TestRegistrationController.cs`

---

## Endpoints

### 1. POST /api/test-registration
**Purpose:** Test user registration with phone validation

**Request Body:**
```json
{
  "userId": 1001,
  "email": "test@example.com",
  "password": "SecurePass123!",
  "phoneNumber": "+1234567890",
  "referredBy": null,
  "clientIp": "127.0.0.1"
}
```

**Success Response (200 OK):**
```json
{
  "success": true,
  "message": "User registered successfully",
  "userId": 1001,
  "credentialId": 5001
}
```

**Error Response (400 Bad Request):**
```json
{
  "success": false,
  "message": "Invalid phone number format. Use 10-15 digits, optionally starting with '+'."
}
```

---

### 2. GET /api/test-registration/health
**Purpose:** Health check to verify controller is working

**Response:**
```json
{
  "status": "healthy",
  "controller": "TestRegistrationController",
  "timestamp": "2025-10-29T08:40:00Z",
  "message": "Test controller is ready for phone validation testing"
}
```

---

### 3. GET /api/test-registration/scenarios
**Purpose:** Get list of all test scenarios

**Response:**
```json
{
  "totalScenarios": 8,
  "scenarios": [
    {
      "scenario": "NULL Phone Number",
      "phoneNumber": null,
      "expectedResult": "Success",
      "description": "Phone number is null - should be stored as NULL in database"
    },
    // ... 7 more scenarios
  ],
  "documentation": "See TESTING_GUIDE.md for detailed test instructions"
}
```

---

## Quick Test Commands

### Using cURL

#### Test 1: NULL Phone Number ✅
```bash
curl -X POST http://localhost:5000/api/test-registration \
  -H "Content-Type: application/json" \
  -d '{
    "userId": 1001,
    "email": "test1@example.com",
    "password": "SecurePass123!",
    "phoneNumber": null,
    "clientIp": "127.0.0.1"
  }'
```

#### Test 2: Empty String Phone ✅
```bash
curl -X POST http://localhost:5000/api/test-registration \
  -H "Content-Type: application/json" \
  -d '{
    "userId": 1002,
    "email": "test2@example.com",
    "password": "SecurePass123!",
    "phoneNumber": "",
    "clientIp": "127.0.0.1"
  }'
```

#### Test 3: Valid International Phone ✅
```bash
curl -X POST http://localhost:5000/api/test-registration \
  -H "Content-Type: application/json" \
  -d '{
    "userId": 1003,
    "email": "test3@example.com",
    "password": "SecurePass123!",
    "phoneNumber": "+1 (555) 123-4567",
    "clientIp": "127.0.0.1"
  }'
```

#### Test 4: Invalid Phone (Too Short) ❌
```bash
curl -X POST http://localhost:5000/api/test-registration \
  -H "Content-Type: application/json" \
  -d '{
    "userId": 1004,
    "email": "test4@example.com",
    "password": "SecurePass123!",
    "phoneNumber": "12345",
    "clientIp": "127.0.0.1"
  }'
```

#### Health Check
```bash
curl http://localhost:5000/api/test-registration/health
```

#### Get Test Scenarios
```bash
curl http://localhost:5000/api/test-registration/scenarios
```

---

### Using PowerShell (Invoke-RestMethod)

#### Test 1: NULL Phone
```powershell
$body = @{
    userId = 1001
    email = "test1@example.com"
    password = "SecurePass123!"
    phoneNumber = $null
    clientIp = "127.0.0.1"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/api/test-registration" `
    -Method POST `
    -Body $body `
    -ContentType "application/json"
```

#### Test 2: Valid Phone
```powershell
$body = @{
    userId = 1002
    email = "test2@example.com"
    password = "SecurePass123!"
    phoneNumber = "+15551234567"
    clientIp = "127.0.0.1"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/api/test-registration" `
    -Method POST `
    -Body $body `
    -ContentType "application/json"
```

#### Health Check
```powershell
Invoke-RestMethod -Uri "http://localhost:5000/api/test-registration/health"
```

---

### Using Postman

1. **Create New Request**
   - Method: `POST`
   - URL: `http://localhost:5000/api/test-registration`

2. **Headers**
   ```
   Content-Type: application/json
   ```

3. **Body (raw JSON)**
   ```json
   {
     "userId": 1001,
     "email": "test@example.com",
     "password": "SecurePass123!",
     "phoneNumber": null,
     "clientIp": "127.0.0.1"
   }
   ```

4. **Send Request**

---

## Automated Test Script

### PowerShell Script

Save as `test-registration.ps1`:

```powershell
# Test Registration Script
$baseUrl = "http://localhost:5000"

Write-Host "=== Testing TestRegistrationController ===" -ForegroundColor Cyan

# Health Check
Write-Host "`n1. Health Check..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$baseUrl/api/test-registration/health"
    Write-Host "✅ Health Status: $($health.status)" -ForegroundColor Green
} catch {
    Write-Host "❌ Health check failed: $_" -ForegroundColor Red
    exit 1
}

# Get Scenarios
Write-Host "`n2. Getting Test Scenarios..." -ForegroundColor Yellow
$scenarios = Invoke-RestMethod -Uri "$baseUrl/api/test-registration/scenarios"
Write-Host "✅ Found $($scenarios.totalScenarios) test scenarios" -ForegroundColor Green

# Test Cases
$testCases = @(
    @{ 
        UserId = 2001
        Email = "null-test@example.com"
        Phone = $null
        Description = "NULL Phone"
        ExpectSuccess = $true
    },
    @{ 
        UserId = 2002
        Email = "empty-test@example.com"
        Phone = ""
        Description = "Empty String Phone"
        ExpectSuccess = $true
    },
    @{ 
        UserId = 2003
        Email = "valid-test@example.com"
        Phone = "+1 (555) 123-4567"
        Description = "Valid International Phone"
        ExpectSuccess = $true
    },
    @{ 
        UserId = 2004
        Email = "invalid-test@example.com"
        Phone = "12345"
        Description = "Invalid Phone (Too Short)"
        ExpectSuccess = $false
    }
)

$passCount = 0
$failCount = 0

Write-Host "`n3. Running Test Cases..." -ForegroundColor Yellow

foreach ($test in $testCases) {
    Write-Host "`nTest: $($test.Description)" -ForegroundColor Cyan
    Write-Host "  Email: $($test.Email)" -ForegroundColor Gray
    Write-Host "  Phone: '$($test.Phone)'" -ForegroundColor Gray
    
    $body = @{
        userId = $test.UserId
        email = $test.Email
        password = "SecurePass123!"
        phoneNumber = $test.Phone
        clientIp = "127.0.0.1"
    } | ConvertTo-Json
    
    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/api/test-registration" `
            -Method POST `
            -Body $body `
            -ContentType "application/json" `
            -ErrorAction Stop
        
        if ($response.success -eq $test.ExpectSuccess) {
            Write-Host "  ✅ PASS - Got expected result" -ForegroundColor Green
            $passCount++
        } else {
            Write-Host "  ❌ FAIL - Unexpected result" -ForegroundColor Red
            Write-Host "  Expected success: $($test.ExpectSuccess), Got: $($response.success)" -ForegroundColor Red
            $failCount++
        }
        
        if (-not $response.success) {
            Write-Host "  Message: $($response.message)" -ForegroundColor Yellow
        }
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        
        if (-not $test.ExpectSuccess -and $statusCode -eq 400) {
            Write-Host "  ✅ PASS - Correctly rejected" -ForegroundColor Green
            $passCount++
        } else {
            Write-Host "  ❌ FAIL - Unexpected error" -ForegroundColor Red
            Write-Host "  Error: $_" -ForegroundColor Red
            $failCount++
        }
    }
}

# Summary
Write-Host "`n=== Test Summary ===" -ForegroundColor Cyan
Write-Host "Total Tests: $($testCases.Count)" -ForegroundColor White
Write-Host "Passed: $passCount" -ForegroundColor Green
Write-Host "Failed: $failCount" -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Red" })

if ($failCount -eq 0) {
    Write-Host "`n🎉 All tests passed!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n❌ Some tests failed" -ForegroundColor Red
    exit 1
}
```

**Run the script:**
```powershell
.\test-registration.ps1
```

---

## Logging

The controller logs detailed information for debugging:

### Info Logs (Successful Requests)
```
Test registration request received for Email: test@example.com, PhoneNumber: '+1234567890', UserId: 1001
Test registration succeeded for Email: test@example.com, UserId: 1001, CredentialId: 5001
```

### Warning Logs (Validation Failures)
```
Test registration failed for Email: test@example.com. Reason: Invalid phone number format. Use 10-15 digits, optionally starting with '+'.
```

### Error Logs (Unexpected Errors)
```
Unexpected error during test registration for Email: test@example.com
```

---

## Verification Steps

### 1. Start the Application
```bash
dotnet run --project AuthService/AuthService.Api.csproj
```

### 2. Test Health Endpoint
```bash
curl http://localhost:5000/api/test-registration/health
```

**Expected:** `status: "healthy"`

### 3. Run Test Cases
Use the PowerShell script or individual cURL commands above.

### 4. Check Database
```sql
SELECT user_id, email, phone_number 
FROM auth.users 
WHERE email LIKE 'test%@example.com'
ORDER BY user_id;
```

**Expected Results:**
- NULL phones stored as `NULL`
- Empty strings stored as `NULL`
- Valid phones stored in cleaned format

### 5. Check Application Logs
Look for log entries from `TestRegistrationController`

---

## Expected Behaviors

| Phone Input | C# Validation | DB Storage | HTTP Status | Success |
|-------------|---------------|------------|-------------|---------|
| `null` | ✅ Pass | `NULL` | 200 | `true` |
| `""` | ✅ Pass (→ null) | `NULL` | 200 | `true` |
| `"   "` | ✅ Pass (→ null) | `NULL` | 200 | `true` |
| `"+1 (555) 123-4567"` | ✅ Pass (→ cleaned) | `+15551234567` | 200 | `true` |
| `"9876543210"` | ✅ Pass | `9876543210` | 200 | `true` |
| `"12345"` | ❌ Reject | n/a | 400 | `false` |
| `"abc"` | ❌ Reject | n/a | 400 | `false` |

---

## Troubleshooting

### Issue: Health endpoint returns 404
**Solution:** Ensure application is running and controller is registered

### Issue: Registration always fails
**Check:**
1. Database connection is working
2. `auth.register_user_enhanced()` function exists
3. Application logs for specific errors

### Issue: Phone validation not working
**Check:**
1. `RegisterRequestDto.PhoneNumber` is `string?` (nullable)
2. `[Required]` attribute is removed
3. `AccountService.RegisterGrpcAsync()` has enhanced validation logic

---

## Cleanup

### Delete Test Users
```sql
DELETE FROM auth.user_credentials 
WHERE user_id IN (SELECT user_id FROM auth.users WHERE email LIKE 'test%@example.com');

DELETE FROM auth.users 
WHERE email LIKE 'test%@example.com';
```

### Remove Test Controller (Production)
When deploying to production, you may want to remove or disable this controller:

1. Delete `TestRegistrationController.cs`, or
2. Add `#if DEBUG` conditional compilation, or
3. Use feature flags to disable in production

---

## Related Documentation

- **PHONE_VALIDATION_FIX.md** - Detailed explanation of the fix
- **TESTING_GUIDE.md** - Comprehensive testing scenarios
- **ALL_FIXES_SUMMARY.md** - Overview of all fixes

---

## Quick Reference

```bash
# Health Check
GET /api/test-registration/health

# Get Test Scenarios
GET /api/test-registration/scenarios

# Test Registration
POST /api/test-registration
Body: { userId, email, password, phoneNumber, clientIp }

# Response Codes
200 - Success
400 - Validation failed
500 - Server error
```

---

**Note:** This test controller is for development and testing purposes. Consider removing or securing it before production deployment.
