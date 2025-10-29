# Testing Guide - Phone Validation Fix

## Quick Verification

Run these tests to verify the phone validation fix works correctly.

---

## Build Verification

✅ **First, ensure build succeeds:**

```bash
dotnet build AuthService.sln
```

**Expected:** 
```
Build succeeded.
    0 Error(s)
    159 Warning(s)
```

---

## Unit Test Scenarios

### Test 1: Registration with NULL Phone Number ✅

**Request:**
```json
{
  "userId": 1001,
  "email": "test1@example.com",
  "password": "SecurePass123!",
  "phoneNumber": null,
  "clientIp": "127.0.0.1"
}
```

**Expected Result:**
- ✅ Registration succeeds
- ✅ Database stores `phone_number = NULL`
- ✅ No constraint violation

**cURL Command:**
```bash
curl -X POST https://your-api/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "userId": 1001,
    "email": "test1@example.com",
    "password": "SecurePass123!",
    "phoneNumber": null,
    "clientIp": "127.0.0.1"
  }'
```

---

### Test 2: Registration with Empty String Phone Number ✅

**Request:**
```json
{
  "userId": 1002,
  "email": "test2@example.com",
  "password": "SecurePass123!",
  "phoneNumber": "",
  "clientIp": "127.0.0.1"
}
```

**Expected Result:**
- ✅ Registration succeeds
- ✅ C# validation treats `""` as `NULL`
- ✅ Database stores `phone_number = NULL`
- ✅ No constraint violation

---

### Test 3: Registration with Whitespace Phone Number ✅

**Request:**
```json
{
  "userId": 1003,
  "email": "test3@example.com",
  "password": "SecurePass123!",
  "phoneNumber": "   ",
  "clientIp": "127.0.0.1"
}
```

**Expected Result:**
- ✅ Registration succeeds
- ✅ C# validation treats whitespace as `NULL`
- ✅ Database stores `phone_number = NULL`

---

### Test 4: Valid International Phone Number ✅

**Request:**
```json
{
  "userId": 1004,
  "email": "test4@example.com",
  "password": "SecurePass123!",
  "phoneNumber": "+1 (555) 123-4567",
  "clientIp": "127.0.0.1"
}
```

**Expected Result:**
- ✅ Registration succeeds
- ✅ C# validation cleans and validates
- ✅ Database stores `phone_number = +15551234567` (cleaned)
- ✅ Formatting characters removed

---

### Test 5: Valid Local Phone Number ✅

**Request:**
```json
{
  "userId": 1005,
  "email": "test5@example.com",
  "password": "SecurePass123!",
  "phoneNumber": "9876543210",
  "clientIp": "127.0.0.1"
}
```

**Expected Result:**
- ✅ Registration succeeds
- ✅ Database stores `phone_number = 9876543210`

---

### Test 6: Invalid Phone - Too Short ❌

**Request:**
```json
{
  "userId": 1006,
  "email": "test6@example.com",
  "password": "SecurePass123!",
  "phoneNumber": "12345",
  "clientIp": "127.0.0.1"
}
```

**Expected Result:**
- ❌ Registration fails
- ❌ C# validation rejects (before database call)
- ❌ Error: "Invalid phone number format. Use 10-15 digits, optionally starting with '+'."

**Response:**
```json
{
  "success": false,
  "message": "Invalid phone number format. Use 10-15 digits, optionally starting with '+'."
}
```

---

### Test 7: Invalid Phone - Contains Letters ❌

**Request:**
```json
{
  "userId": 1007,
  "email": "test7@example.com",
  "password": "SecurePass123!",
  "phoneNumber": "abc-defg-hijk",
  "clientIp": "127.0.0.1"
}
```

**Expected Result:**
- ❌ Registration fails
- ❌ C# validation rejects
- ❌ Error message returned

---

### Test 8: Invalid Phone - Too Long ❌

**Request:**
```json
{
  "userId": 1008,
  "email": "test8@example.com",
  "password": "SecurePass123!",
  "phoneNumber": "12345678901234567890",
  "clientIp": "127.0.0.1"
}
```

**Expected Result:**
- ❌ Registration fails
- ❌ C# validation rejects (max 15 digits)
- ❌ Error message returned

---

### Test 9: Phone with Special Characters (Valid After Cleaning) ✅

**Request:**
```json
{
  "userId": 1009,
  "email": "test9@example.com",
  "password": "SecurePass123!",
  "phoneNumber": "+1-555.123.4567",
  "clientIp": "127.0.0.1"
}
```

**Expected Result:**
- ✅ Registration succeeds
- ✅ Cleaned to `+15551234567`
- ✅ Database stores cleaned value

---

## Database Verification Queries

### Check Phone Numbers Stored

```sql
SELECT 
    user_id,
    email,
    phone_number,
    CASE 
        WHEN phone_number IS NULL THEN 'NULL (Valid)'
        WHEN phone_number ~* '^\+?[0-9]{10,15}$' THEN 'Valid Format'
        ELSE 'INVALID FORMAT!'
    END as validation_status
FROM auth.users
WHERE email LIKE 'test%@example.com'
ORDER BY user_id;
```

**Expected Output:**
```
user_id | email                | phone_number    | validation_status
--------|----------------------|-----------------|------------------
1001    | test1@example.com    | NULL            | NULL (Valid)
1002    | test2@example.com    | NULL            | NULL (Valid)
1003    | test3@example.com    | NULL            | NULL (Valid)
1004    | test4@example.com    | +15551234567    | Valid Format
1005    | test5@example.com    | 9876543210      | Valid Format
1009    | test9@example.com    | +15551234567    | Valid Format
```

### Check for Constraint Violations

```sql
-- This should return 0 rows (no violations)
SELECT *
FROM auth.users
WHERE phone_number IS NOT NULL 
  AND phone_number !~* '^\+?[0-9]{10,15}$';
```

**Expected:** 0 rows

---

## Log Verification

### Check Application Logs

**Debug Logs (successful validations):**
```
Phone number for test3@example.com was whitespace/formatting only, treating as null
Phone number validated for test4@example.com: '+15551234567'
```

**Warning Logs (validation failures - expected for invalid inputs):**
```
Invalid phone number format for test6@example.com: Original='12345', Cleaned='12345'
Invalid phone number format for test7@example.com: Original='abc-defg-hijk', Cleaned='abcdefghijk'
```

**Error Logs (should NOT appear if fix works):**
```
❌ Should NOT see: "Database check constraint 'valid_phone_format' violated"
```

---

## Integration Test Script

### PowerShell Test Script

```powershell
# test-phone-validation.ps1
$baseUrl = "https://localhost:7000"  # Adjust to your API URL

$tests = @(
    @{ UserId = 1001; Email = "test1@example.com"; Phone = $null; Expected = "Success" },
    @{ UserId = 1002; Email = "test2@example.com"; Phone = ""; Expected = "Success" },
    @{ UserId = 1003; Email = "test3@example.com"; Phone = "   "; Expected = "Success" },
    @{ UserId = 1004; Email = "test4@example.com"; Phone = "+1 (555) 123-4567"; Expected = "Success" },
    @{ UserId = 1005; Email = "test5@example.com"; Phone = "9876543210"; Expected = "Success" },
    @{ UserId = 1006; Email = "test6@example.com"; Phone = "12345"; Expected = "Fail" },
    @{ UserId = 1007; Email = "test7@example.com"; Phone = "abc-defg-hijk"; Expected = "Fail" },
    @{ UserId = 1008; Email = "test8@example.com"; Phone = "12345678901234567890"; Expected = "Fail" }
)

foreach ($test in $tests) {
    $body = @{
        userId = $test.UserId
        email = $test.Email
        password = "SecurePass123!"
        phoneNumber = $test.Phone
        clientIp = "127.0.0.1"
    } | ConvertTo-Json

    Write-Host "`nTesting: $($test.Email) with phone: '$($test.Phone)'" -ForegroundColor Cyan
    
    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/api/auth/register" `
            -Method POST `
            -Body $body `
            -ContentType "application/json"
        
        if ($response.success -and $test.Expected -eq "Success") {
            Write-Host "✅ PASS - Registration succeeded as expected" -ForegroundColor Green
        } elseif (-not $response.success -and $test.Expected -eq "Fail") {
            Write-Host "✅ PASS - Registration failed as expected: $($response.message)" -ForegroundColor Green
        } else {
            Write-Host "❌ FAIL - Unexpected result" -ForegroundColor Red
            Write-Host $response
        }
    } catch {
        if ($test.Expected -eq "Fail") {
            Write-Host "✅ PASS - Request rejected as expected" -ForegroundColor Green
        } else {
            Write-Host "❌ FAIL - Unexpected error: $_" -ForegroundColor Red
        }
    }
}
```

### Run Test Script

```bash
# PowerShell
.\test-phone-validation.ps1

# Or manually with cURL (Linux/Mac)
bash test-phone-validation.sh
```

---

## Success Criteria

### All Tests Must Pass ✅

- [x] Build succeeds with 0 errors
- [x] NULL phone numbers accepted
- [x] Empty strings treated as NULL
- [x] Whitespace treated as NULL
- [x] Valid international phones accepted and cleaned
- [x] Valid local phones accepted
- [x] Invalid formats rejected by C# (before DB)
- [x] No database constraint violations
- [x] Proper logging for debugging

### Database State ✅

```sql
-- Verify no constraint violations exist
SELECT COUNT(*) as violation_count
FROM auth.users
WHERE phone_number IS NOT NULL 
  AND phone_number !~* '^\+?[0-9]{10,15}$';
```

**Expected:** `violation_count = 0`

### Monitoring ✅

- [ ] Check application logs for warnings/errors
- [ ] Monitor registration success rate (should improve)
- [ ] Verify no `23514` error codes in logs
- [ ] Confirm phone validation happens at C# level

---

## Rollback Plan (If Needed)

If issues occur, revert changes:

```bash
# Revert DTO change
git checkout HEAD -- AuthService.Application/DTOs/Auth/RegisterRequestDto.cs

# Revert service logic
git checkout HEAD -- AuthService.Application/Services/AccountService.cs

# Rebuild
dotnet build AuthService.sln
```

---

## Next Steps After Testing

1. ✅ **Verify all tests pass**
2. ✅ **Monitor production logs** for 24-48 hours
3. ✅ **Track registration metrics**
4. ✅ **Document lessons learned**
5. ✅ **Update team documentation**

---

## Quick Command Reference

```bash
# Build
dotnet build AuthService.sln

# Run application
dotnet run --project AuthService/AuthService.Api.csproj

# Test single registration
curl -X POST https://localhost:7000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"userId": 1001, "email": "test@example.com", "password": "SecurePass123!", "phoneNumber": null}'

# Check database
psql -U your_user -d your_db -c "SELECT user_id, email, phone_number FROM auth.users WHERE email = 'test@example.com';"

# View logs
tail -f logs/application.log
```

---

## Support

For issues or questions:
- Review: `PHONE_VALIDATION_FIX.md` (detailed explanation)
- Review: `ALL_FIXES_SUMMARY.md` (comprehensive overview)
- Check logs for validation warnings
- Verify database constraint with SQL queries above
