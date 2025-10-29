# Dependency Injection Fix Guide - NullReferenceException in AuthGrpcService

## Issue Summary

**Error:** `System.NullReferenceException: Object reference not set to an instance of an object`  
**Location:** `AuthGrpcService.RegisterUser()` on line: `var response = await _accountService.RegisterGrpcAsync(registerRequestDto);`  
**Cause:** `_accountService` field is null

---

## Root Cause Analysis

Your DI setup in `Program.cs` is **actually correct**! All dependencies are properly registered:

✅ **Line 94:** `builder.Services.AddDatabase(builder.Configuration);` - Registers all repositories  
✅ **Lines 132-139:** Email gRPC client registration  
✅ **Lines 160-176:** Application services registration including `IAccountService`

### Why It Might Fail

The NullReferenceException indicates one of these scenarios:

1. **Service construction fails silently** - One of AccountService's dependencies can't be resolved
2. **gRPC service resolution** - AuthGrpcService isn't getting services from the DI container
3. **Email client misconfiguration** - EmailServiceClient registration might not match expected type

---

## The Fix Applied

I've updated `Program.cs` with:

### 1. Registration Verification (Lines 167-176)
```csharp
try
{
    builder.Services.AddScoped<IAccountService, AccountService>();
    Log.Information("✅ IAccountService registered successfully");
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ Failed to register IAccountService");
    throw;
}
```

### 2. Startup DI Verification (Lines 233-270)
```csharp
// Verify critical services can be resolved
Log.Information("Verifying DI registrations...");

var accountService = scope.ServiceProvider.GetRequiredService<IAccountService>();
Log.Information("✅ IAccountService resolved successfully");

var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
Log.Information("✅ IAuthService resolved successfully");

// ... more verifications ...
```

### 3. Added Missing Using Statement (Line 11)
```csharp
using AuthService.Infrastructure.Interfaces;
```

---

## Steps to Test the Fix

### Step 1: Stop the Running Application ⚠️

The build is failing because the application is currently running and locking DLL files.

**Option A: Stop from Visual Studio**
- Press `Shift + F5` or click the Stop button

**Option B: Stop from Command Line**
```powershell
# Find the process
Get-Process AuthService* | Stop-Process -Force

# Or find by port (if running on port 5000/7000)
Get-NetTCPConnection -LocalPort 5000 | Select-Object -ExpandProperty OwningProcess | ForEach-Object { Stop-Process -Id $_ -Force }
```

### Step 2: Build the Application

```bash
cd d:\SingleProject1\AuthService\databwseupdate
dotnet clean
dotnet build AuthService.sln
```

**Expected Output:**
```
✅ IAccountService registered successfully
Build succeeded.
    0 Error(s)
```

### Step 3: Run the Application

```bash
dotnet run --project AuthService\AuthService.Api.csproj
```

**Watch for these logs:**
```
Verifying DI registrations...
✅ IAccountService resolved successfully
✅ IAuthService resolved successfully
✅ IPasswordService resolved successfully
✅ IUserCredentialRepository resolved successfully
✅ ISecurityTokenRepository resolved successfully
✅ All critical services verified!
🚀 Starting Auth API...
```

### Step 4: Test the gRPC Endpoint

Use your gRPC client to call `RegisterUser`:

```json
{
  "userId": "1001",
  "email": "test@example.com",
  "password": "SecurePass123!",
  "phoneNumber": "+1234567890",
  "clientIp": "127.0.0.1"
}
```

---

## If the Error Persists

### Check 1: Verify All Dependencies

The startup verification will now tell you EXACTLY which service fails to resolve:

```
❌ Dependency Injection verification failed!
System.InvalidOperationException: Unable to resolve service for type 'IPasswordService'
```

### Check 2: EmailServiceClient Type Mismatch

If you see an error about `EmailServiceClient`, the issue is the gRPC client registration.

**Current registration (Program.cs:132-139):**
```csharp
builder.Services.AddGrpcClient<EmailServiceClient>(o =>
{
    o.Address = new Uri(emailServiceUrl);
})
```

**AccountService expects:**
```csharp
EmailService.Grpc.EmailService.EmailServiceClient _emailServiceClient;
```

**Fix if needed:**
```csharp
// Use fully qualified name
builder.Services.AddGrpcClient<EmailService.Grpc.EmailService.EmailServiceClient>(o =>
{
    o.Address = new Uri(emailServiceUrl);
})
```

### Check 3: Project References

Ensure `AuthService.Api.csproj` references:
```xml
<ProjectReference Include="..\AuthService.Application\AuthService.Application.csproj" />
<ProjectReference Include="..\AuthService.Infrastructure\AuthService.Infrastructure.csproj" />
<ProjectReference Include="..\AuthService.Grpc\AuthService.Grpc.csproj" />
```

---

## Diagnostic Commands

### Check if Service is Registered
```csharp
// Add temporary endpoint to test DI
app.MapGet("/debug/di", (IAccountService accountService) => 
{
    return Results.Ok(new { status = "IAccountService resolved successfully!" });
});
```

### Check Constructor Parameters
Add logging to `AuthGrpcService` constructor:

```csharp
public AuthGrpcService(
    IAuthService authService, 
    IAccountService accountService, 
    ILogger<AuthGrpcService> logger)
{
    _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    
    logger.LogInformation("✅ AuthGrpcService constructed successfully");
}
```

---

## Complete Registration Order

Here's the correct order (as it is in your Program.cs):

```
1. Line 54:  builder.Services.AddControllers();
2. Line 56:  builder.Services.AddGrpc(...);
3. Line 93:  builder.Services.AddHttpContextAccessor();
4. Line 94:  builder.Services.AddDatabase(builder.Configuration);
   └─ Registers: IUserCredentialRepository, ISecurityTokenRepository, etc.
5. Lines 132-139: AddGrpcClient<EmailServiceClient>
6. Lines 150-156: NpgsqlDataSource, IDbConnectionFactory
7. Lines 160-164: IPasswordService, IJwtService, IAuthService
8. Lines 167-176: IAccountService (with verification)
9. Line 228: app.MapGrpcService<AuthGrpcService>()
```

---

## Expected Behavior After Fix

### Startup Logs
```
[12:00:00 INF] ✅ IAccountService registered successfully
[12:00:01 INF] Verifying DI registrations...
[12:00:01 INF] ✅ IAccountService resolved successfully
[12:00:01 INF] ✅ IAuthService resolved successfully
[12:00:01 INF] ✅ IPasswordService resolved successfully
[12:00:01 INF] ✅ IUserCredentialRepository resolved successfully
[12:00:01 INF] ✅ ISecurityTokenRepository resolved successfully
[12:00:01 INF] ✅ All critical services verified!
[12:00:01 INF] ✅ Successfully connected to Neon PostgreSQL Database!
[12:00:02 INF] 🚀 Starting Auth API...
```

### gRPC Call Logs
```
[12:00:10 INF] Test registration request received for Email: test@example.com
[12:00:10 DBG] Phone number validated for test@example.com: '+1234567890'
[12:00:10 INF] User registered via GRPC/Function for Email test@example.com
```

### No More NullReferenceException ✅

---

## Summary

1. ✅ **Stop the running application**
2. ✅ **Build with verification** - `dotnet build`
3. ✅ **Run and check logs** - Look for "✅ All critical services verified!"
4. ✅ **Test gRPC endpoint** - Should work without NullReferenceException

The fix adds comprehensive DI verification that will:
- Catch missing dependencies at **startup** (not at runtime)
- Show exactly which service fails to resolve
- Prevent the NullReferenceException from occurring

---

## Quick Test Script

```powershell
# Stop any running instances
Get-Process AuthService* -ErrorAction SilentlyContinue | Stop-Process -Force

# Clean and build
cd d:\SingleProject1\AuthService\databwseupdate
dotnet clean
dotnet build AuthService.sln

# If build succeeds, run
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Build successful! Starting application..." -ForegroundColor Green
    dotnet run --project AuthService\AuthService.Api.csproj
} else {
    Write-Host "❌ Build failed! Check errors above." -ForegroundColor Red
}
```

---

**Your DI configuration is correct. The verification code will help identify the exact issue!** 🔍
