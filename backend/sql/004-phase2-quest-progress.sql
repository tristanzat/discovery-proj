-- Phase 2 Stage 2: enemy type tag for targeted kill tracking

ALTER TABLE quest_definitions
    ADD COLUMN IF NOT EXISTS enemy_type_tag VARCHAR(64) NULL;

-- Tag the existing starter quest so only goblin kills count.
UPDATE quest_definitions
SET enemy_type_tag = 'Goblin'
WHERE quest_id = 'clear-goblin-room'
  AND enemy_type_tag IS NULL;
