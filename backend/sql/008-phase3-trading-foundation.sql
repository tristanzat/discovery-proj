-- Phase 3 trading foundation
-- Trade offers move inventory into escrow at creation and settle on response.

CREATE TABLE IF NOT EXISTS trade_offers (
    trade_offer_id BIGSERIAL PRIMARY KEY,
    from_account_id INT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
    to_account_id INT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
    item_code VARCHAR(64) NOT NULL,
    item_name VARCHAR(100) NOT NULL,
    rarity VARCHAR(24) NOT NULL,
    quantity INT NOT NULL,
    note VARCHAR(180),
    status VARCHAR(24) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    responded_at TIMESTAMPTZ NULL
);

CREATE INDEX IF NOT EXISTS ix_trade_offers_to_status_created
    ON trade_offers(to_account_id, status, created_at DESC);

CREATE INDEX IF NOT EXISTS ix_trade_offers_from_status_created
    ON trade_offers(from_account_id, status, created_at DESC);
