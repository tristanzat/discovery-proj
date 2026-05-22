-- Phase 2: starter consumables for inventory usage flow
-- Grants 3 minor healing potions to accounts that do not already have the item.

INSERT INTO inventory_items (
    account_id,
    item_code,
    item_name,
    rarity,
    quantity,
    acquired_at
)
SELECT
    a.account_id,
    'minor-healing-potion',
    'Minor Healing Potion',
    'common',
    3,
    NOW()
FROM accounts a
WHERE NOT EXISTS (
    SELECT 1
    FROM inventory_items ii
    WHERE ii.account_id = a.account_id
      AND ii.item_code = 'minor-healing-potion'
);
