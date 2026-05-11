-- Phase 1 Schema Initialization
-- Durable tables for accounts and player progression

CREATE TABLE IF NOT EXISTS accounts (
    account_id SERIAL PRIMARY KEY,
    username VARCHAR(32) NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS player_progress (
    account_id INT PRIMARY KEY REFERENCES accounts(account_id) ON DELETE CASCADE,
    level INT NOT NULL DEFAULT 1,
    xp INT NOT NULL DEFAULT 0,
    gold INT NOT NULL DEFAULT 0,
    max_hp INT NOT NULL DEFAULT 100,
    last_saved_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Smoke test: insert test player
INSERT INTO accounts (username, password_hash)
VALUES ('test_player', 'placeholder_hash')
ON CONFLICT (username) DO NOTHING;

INSERT INTO player_progress (account_id)
SELECT account_id FROM accounts WHERE username = 'test_player'
ON CONFLICT (account_id) DO NOTHING;
