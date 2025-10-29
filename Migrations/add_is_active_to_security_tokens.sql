-- Migration: Add is_active column to security_tokens table
-- Date: 2025-10-29
-- Description: Adds IsActive tracking to security tokens for better token lifecycle management

-- Add is_active column with default value
ALTER TABLE auth.security_tokens 
ADD COLUMN IF NOT EXISTS is_active BOOLEAN DEFAULT true NOT NULL;

-- Set existing tokens to active (if any exist)
UPDATE auth.security_tokens 
SET is_active = true 
WHERE is_active IS NULL;

-- Add index for performance on active token lookups
CREATE INDEX IF NOT EXISTS idx_security_tokens_active 
ON auth.security_tokens(is_active) 
WHERE is_active = true;

-- Add composite index for common query pattern (token lookup with active status)
CREATE INDEX IF NOT EXISTS idx_security_tokens_hash_active 
ON auth.security_tokens(token_hash, is_active, verification_status) 
WHERE is_active = true;

-- Add comment for documentation
COMMENT ON COLUMN auth.security_tokens.is_active IS 
'Indicates whether the token is active. Inactive tokens cannot be used even if not expired.';

-- Verification query
SELECT 
    COUNT(*) as total_tokens,
    COUNT(CASE WHEN is_active = true THEN 1 END) as active_tokens,
    COUNT(CASE WHEN is_active = false THEN 1 END) as inactive_tokens
FROM auth.security_tokens;
