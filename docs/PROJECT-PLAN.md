# Discovery Project Plan
## Description
This project will be an online multiplayer dungeon crawler. Players will have accounts that they have to log in with in order to play. There will be quests, trading, looting, and other basic functions of a dungeon crawler. There will be a main hub overworld where quests are accepted and players interact with each other. There will also be a portal to go to a procedurally-generated dungeon. Enemies are computer-controlled; the game is PvE.

## Requirements
- Online synchronous turn-based multiplayer
- Player accounts and log in
- Computer-controlled enemies
- Player interaction through trading and chat

## Phases
- Phase 1 (MVP): Player accounts, login, single dungeon room, basic combat loop
- Phase 2: Quest system, inventory, loot drops
- Phase 3: Trading, hub overworld, player chat
- Phase 4: Procedural dungeon generation
- Phase 5: Real-time multiplayer sync, scaling infrastructure

## Learning Goals
- Understand REST API design and how a frontend consumes a backend
- Learn how relational databases model real-world entities
- Understand event-driven architecture and why messaging is needed
- Learn how to containerize and deploy services

## Tech Stack Mapping
- Player accounts & login → Postgres (users table) + .NET auth
- Dungeon/combat state → Cosmos DB (fast ephemeral state)
- Quest completion rewards → Service Bus (async processing)
- Player action telemetry → Event Hub (high-volume events)
- Leaderboards → Redis (cached rankings)
- Real-time multiplayer sync → WebSockets via .NET + SignalR
- Deployment → Docker containers on Azure, orchestrated by K8s

## Out of Scope
- PvP combat
- Voice chat
- Mobile clients
- Microtransactions / economy
