# SecurityToken Error Fixes

## Issues Resolved

### 1. Missing `IsActive` Property
**Error:** `'SecurityToken' does not contain a definition for 'IsActive'`

**Fix:** Added `IsActive` property to the `SecurityToken` entity with a default value of `true`.

```csharp
public bool IsActive { get; set; } = true;
```

**Changes Made:**
- Updated `AuthService.Domain.Entities.SecurityToken` to include `IsActive` property
- Updated `SecurityTokenRepository.GetByTokenHashAsync()` to SELECT and filter by `is_active`
- Updated `SecurityTokenRepository.CreateAsync()` to INSERT `is_active` as `true`
- Updated `SecurityTokenRepository.UpdateAsync()` to support updating `is_active`

### 2. Type Conversion Between Metadata Properties
**Error:** `Cannot implicitly convert type 'string' to 'System.Collections.Generic.Dictionary<string, object>'`

**Location:** `AccountService.cs` line 75 was attempting to serialize a Dictionary and assign the JSON string to `Metadata`.

**Cause:** The `SecurityToken` entity has two metadata properties:
- `Metadata` (Dictionary<string, object>) - for in-memory use
- `MetadataJson` (string) - for database storage

**The Fix:** Changed line 75 in AccountService from:
```csharp
// ❌ WRONG
Metadata = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>())
```
to:
```csharp
// ✅ CORRECT
Metadata = new Dictionary<string, object>()
```

**How to Use Correctly:**

```csharp
// ✅ CORRECT: When reading from database
var token = await _tokenRepository.GetByTokenHashAsync(tokenHash);
// The repository handles deserialization from MetadataJson to Metadata

// ✅ CORRECT: When creating a new token
var token = new SecurityToken
{
    Metadata = new Dictionary<string, object> 
    { 
        { "key", "value" } 
    }
};
// The repository handles serialization from Metadata to MetadataJson

// ❌ WRONG: Never assign MetadataJson to Metadata directly
token.Metadata = token.MetadataJson; // This will cause the error!

// ✅ CORRECT: If you need to deserialize manually
if (!string.IsNullOrEmpty(token.MetadataJson))
{
    token.Metadata = JsonConvert.DeserializeObject<Dictionary<string, object>>(token.MetadataJson);
}
```

## Repository Pattern

The `SecurityTokenRepository` properly handles the conversion:

```csharp
// On READ: Database returns MetadataJson (string)
var token = await ExecuteAsync<SecurityToken>(sql, new { TokenHash = tokenHash });
if (token != null && !string.IsNullOrEmpty(token.MetadataJson))
{
    token.Metadata = JsonConvert.DeserializeObject<Dictionary<string, object>>(token.MetadataJson);
}

// On WRITE: Metadata (Dictionary) is serialized to JSON string
var parameters = new
{
    token.UserId,
    Metadata = JsonConvert.SerializeObject(token.Metadata ?? new Dictionary<string, object>())
};
```

## Database Schema Requirements

Ensure your `auth.security_tokens` table has the `is_active` column:

```sql
ALTER TABLE auth.security_tokens 
ADD COLUMN IF NOT EXISTS is_active BOOLEAN DEFAULT true;

-- Add index for performance
CREATE INDEX IF NOT EXISTS idx_security_tokens_active 
ON auth.security_tokens(is_active) WHERE is_active = true;
```

## Usage Examples

### Validating Active Tokens
```csharp
var token = await _tokenRepository.GetByTokenHashAsync(tokenHash);
if (token == null || !token.IsActive)
{
    return false; // Token is invalid or deactivated
}
```

### Deactivating a Token
```csharp
token.IsActive = false;
token.VerificationStatus = VerificationStatus.Revoked;
await _tokenRepository.UpdateAsync(token);
```

## Build Status
✅ All changes compile successfully with 0 errors
⚠️ Only nullable reference warnings remain (pre-existing)
