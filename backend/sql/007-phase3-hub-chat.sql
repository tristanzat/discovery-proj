-- Phase 3 foundation: hub overworld chat persistence
-- Stores chat messages so players can load recent conversation history.

CREATE TABLE IF NOT EXISTS hub_chat_messages (
    hub_chat_message_id BIGSERIAL PRIMARY KEY,
    account_id INT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
    message VARCHAR(280) NOT NULL,
    sent_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_hub_chat_messages_sent_at
    ON hub_chat_messages(sent_at DESC);
