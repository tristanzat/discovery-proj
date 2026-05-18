-- Phase 2 Schema: quests and inventory

CREATE TABLE IF NOT EXISTS quest_definitions (
    quest_id VARCHAR(64) PRIMARY KEY,
    name VARCHAR(80) NOT NULL,
    description VARCHAR(300) NOT NULL,
    required_enemy_defeats INT NOT NULL DEFAULT 1,
    reward_xp INT NOT NULL DEFAULT 0,
    reward_gold INT NOT NULL DEFAULT 0,
    reward_item_code VARCHAR(64)
);

CREATE TABLE IF NOT EXISTS player_quests (
    player_quest_id BIGSERIAL PRIMARY KEY,
    account_id INT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
    quest_id VARCHAR(64) NOT NULL REFERENCES quest_definitions(quest_id) ON DELETE RESTRICT,
    status VARCHAR(24) NOT NULL,
    progress_count INT NOT NULL DEFAULT 0,
    accepted_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMPTZ NULL,
    CONSTRAINT uq_player_quest UNIQUE (account_id, quest_id)
);

CREATE TABLE IF NOT EXISTS inventory_items (
    inventory_item_id BIGSERIAL PRIMARY KEY,
    account_id INT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
    item_code VARCHAR(64) NOT NULL,
    item_name VARCHAR(100) NOT NULL,
    rarity VARCHAR(24) NOT NULL,
    quantity INT NOT NULL DEFAULT 1,
    acquired_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_inventory_item UNIQUE (account_id, item_code)
);

INSERT INTO quest_definitions (
    quest_id,
    name,
    description,
    required_enemy_defeats,
    reward_xp,
    reward_gold,
    reward_item_code)
VALUES (
    'clear-goblin-room',
    'Goblin Extermination I',
    'Defeat a goblin in the beginner dungeon room.',
    1,
    25,
    15,
    'goblin-ear-trophy')
ON CONFLICT (quest_id) DO NOTHING;