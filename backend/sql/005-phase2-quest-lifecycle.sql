-- Phase 2: quest lifecycle expansion
-- Adds a follow-up goblin quest to demonstrate multi-step progression.

INSERT INTO quest_definitions (
    quest_id,
    name,
    description,
    required_enemy_defeats,
    reward_xp,
    reward_gold,
    reward_item_code,
    enemy_type_tag
)
SELECT
    'goblin-culling-contract',
    'Goblin Culling Contract',
    'Defeat 3 goblins to thin out the cave population.',
    3,
    60,
    35,
    'unknown-relic-shard',
    'Goblin'
WHERE NOT EXISTS (
    SELECT 1
    FROM quest_definitions
    WHERE quest_id = 'goblin-culling-contract'
);
