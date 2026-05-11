SELECT a.username, p.level, p.max_hp
FROM accounts a
JOIN player_progress p ON a.account_id = p.account_id