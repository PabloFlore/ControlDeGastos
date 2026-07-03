-- Migration: Revoked Tokens table
-- Run this in your Supabase SQL Editor after deploying Functions

CREATE TABLE IF NOT EXISTS revoked_tokens (
    token_hash TEXT PRIMARY KEY,
    revoked_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    reason TEXT NOT NULL DEFAULT ''
);

ALTER TABLE revoked_tokens ENABLE ROW LEVEL SECURITY;

-- Only Functions (service_role) can access revoked_tokens
CREATE POLICY "service_role_all_revoked_tokens" ON revoked_tokens
    FOR ALL TO service_role
    USING (true)
    WITH CHECK (true);
