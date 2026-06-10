-- Phase 3 completion: hub overworld active presence tracking

CREATE TABLE IF NOT EXISTS hub_presence (
    account_id INT PRIMARY KEY REFERENCES accounts(account_id) ON DELETE CASCADE,
    last_seen_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_hub_presence_last_seen
    ON hub_presence(last_seen_at DESC);
