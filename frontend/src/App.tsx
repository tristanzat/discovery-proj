import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import './App.css'

type ApiStatus = 'checking' | 'online' | 'offline'

type AuthForm = {
  username: string
  password: string
}

type AccountSummary = {
  accountId: number
  username: string
  createdAt?: string
}

type Combatant = {
  hp: number
  maxHp: number
  name?: string
}

type DungeonSession = {
  sessionId: string
  accountId: number
  username: string
  player: Combatant
  enemy: Combatant
  turn: number
  status: string
  isCompleted: boolean
}

type ProgressSummary = {
  accountId: number
  level: number
  xp: number
  gold: number
  maxHp: number
  levelProgress: {
    xpIntoCurrentLevel: number
    xpToNextLevel: number
    atMaxLevel: boolean
  }
}

type QuestReward = {
  xp: number
  gold: number
  itemCode: string | null
}

type QuestBoardItem = {
  questId: string
  name: string
  description: string
  requiredEnemyDefeats: number
  reward: QuestReward
  status: string
  progressCount: number
}

type AvailableQuestsResponse = {
  accountId: number
  quests: QuestBoardItem[]
}

type QuestLogItem = {
  questId: string
  name: string
  description: string
  requiredEnemyDefeats: number
  progressCount: number
  status: string
  acceptedAt: string
  completedAt: string | null
}

type QuestLogResponse = {
  accountId: number
  questCount: number
  quests: QuestLogItem[]
}

type InventoryItem = {
  itemCode: string
  itemName: string
  rarity: string
  quantity: number
  acquiredAt: string
}

type InventoryResponse = {
  accountId: number
  itemCount: number
  items: InventoryItem[]
}

type HubChatMessage = {
  messageId: number
  accountId: number
  username: string
  message: string
  sentAt: string
}

type HubOverviewResponse = {
  accountId: number
  username: string
  activeAdventurerCount: number
  messages: HubChatMessage[]
}

type CombatQuestProgressUpdate = {
  questId: string
  progressCount: number
  status: string
}

type CombatAttackResponse = {
  outcome: string
  sessionId: string
  questProgress?: CombatQuestProgressUpdate[]
}

type CompleteQuestResponse = {
  message: string
  rewards: {
    xp: number
    gold: number
    loot: {
      code: string
      name: string
      rarity: string
      quantity: number
    }
  }
  progression: {
    level: number
    xp: number
    gold: number
    maxHp: number
    levelsGained: number
    leveledUp: boolean
  }
}

type ApiError = {
  error?: string
  title?: string
}

async function requestJson<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {}),
    },
    ...init,
  })

  const text = await response.text()
  const data = text ? (JSON.parse(text) as T | ApiError) : null

  if (!response.ok) {
    const message =
      (data as ApiError | null)?.error ??
      (data as ApiError | null)?.title ??
      `Request failed with status ${response.status}`

    throw new Error(message)
  }

  return data as T
}

function toPercent(current: number, max: number) {
  if (max <= 0) {
    return 0
  }

  return Math.max(0, Math.min(100, (current / max) * 100))
}

function formatQuestStatus(status: string) {
  return status.replaceAll('-', ' ')
}

function canAcceptQuest(status: string) {
  return status === 'not-started'
}

function canCompleteQuest(status: string) {
  return status === 'ready'
}

function canAbandonQuest(status: string) {
  return status === 'accepted' || status === 'ready'
}

function isCombatConsumable(itemCode: string) {
  return itemCode === 'minor-healing-potion'
}

function App() {
  const [apiStatus, setApiStatus] = useState<ApiStatus>('checking')
  const [registerForm, setRegisterForm] = useState<AuthForm>({ username: '', password: '' })
  const [loginForm, setLoginForm] = useState<AuthForm>({ username: '', password: '' })
  const [account, setAccount] = useState<AccountSummary | null>(null)
  const [progress, setProgress] = useState<ProgressSummary | null>(null)
  const [session, setSession] = useState<DungeonSession | null>(null)
  const [availableQuests, setAvailableQuests] = useState<QuestBoardItem[]>([])
  const [questLog, setQuestLog] = useState<QuestLogItem[]>([])
  const [inventoryItems, setInventoryItems] = useState<InventoryItem[]>([])
  const [hubChatMessages, setHubChatMessages] = useState<HubChatMessage[]>([])
  const [activeAdventurerCount, setActiveAdventurerCount] = useState(0)
  const [hubMessageDraft, setHubMessageDraft] = useState('')
  const [activity, setActivity] = useState<string[]>([
    'Welcome to Phase 3. Log in, enter the hub, and chat before diving into combat.',
  ])
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  useEffect(() => {
    let isMounted = true

    async function checkBackend() {
      try {
        await requestJson<{ status: string }>('/api/health')
        if (isMounted) {
          setApiStatus('online')
        }
      } catch {
        if (isMounted) {
          setApiStatus('offline')
        }
      }
    }

    void checkBackend()

    return () => {
      isMounted = false
    }
  }, [])

  function pushActivity(entry: string) {
    setActivity((current) => [entry, ...current].slice(0, 8))
  }

  function updateRegisterField(field: keyof AuthForm, value: string) {
    setRegisterForm((current) => ({ ...current, [field]: value }))
  }

  function updateLoginField(field: keyof AuthForm, value: string) {
    setLoginForm((current) => ({ ...current, [field]: value }))
  }

  async function loadSession(sessionId: string) {
    const latestSession = await requestJson<DungeonSession>(`/api/dungeon/session/${sessionId}`)
    setSession(latestSession)
    return latestSession
  }

  async function loadProgress(accountId: number) {
    const latestProgress = await requestJson<ProgressSummary>(`/api/player/progress/${accountId}`)
    setProgress(latestProgress)
    return latestProgress
  }

  async function loadAvailableQuests(accountId: number) {
    const response = await requestJson<AvailableQuestsResponse>(`/api/quests/available/${accountId}`)
    setAvailableQuests(response.quests)
    return response.quests
  }

  async function loadQuestLog(accountId: number) {
    const response = await requestJson<QuestLogResponse>(`/api/quests/log/${accountId}`)
    setQuestLog(response.quests)
    return response.quests
  }

  async function loadInventory(accountId: number) {
    const response = await requestJson<InventoryResponse>(`/api/inventory/${accountId}`)
    setInventoryItems(response.items)
    return response.items
  }

  async function loadHubOverview(accountId: number) {
    const response = await requestJson<HubOverviewResponse>(`/api/hub/overview/${accountId}`)
    setActiveAdventurerCount(response.activeAdventurerCount)
    setHubChatMessages(response.messages)
    return response
  }

  async function refreshPhaseTwoState(accountId: number) {
    await Promise.all([
      loadProgress(accountId),
      loadAvailableQuests(accountId),
      loadQuestLog(accountId),
      loadInventory(accountId),
      loadHubOverview(accountId),
    ])
  }

  async function withSubmission(action: () => Promise<void>) {
    setErrorMessage(null)
    setIsSubmitting(true)

    try {
      await action()
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unexpected error'
      setErrorMessage(message)
      pushActivity(`Error: ${message}`)
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleRegister(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    await withSubmission(async () => {
      const createdAccount = await requestJson<AccountSummary>('/api/auth/register', {
        method: 'POST',
        body: JSON.stringify(registerForm),
      })

      setAccount(createdAccount)
      setSession(null)
      await refreshPhaseTwoState(createdAccount.accountId)
      pushActivity(`Registered adventurer ${createdAccount.username}.`)
      pushActivity('Entered hub overworld chat. Say hello to nearby adventurers.')
      setRegisterForm((current) => ({ ...current, password: '' }))
      setLoginForm({ username: createdAccount.username, password: '' })
    })
  }

  async function handleLogin(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    await withSubmission(async () => {
      const loggedInAccount = await requestJson<
        AccountSummary & {
          level?: number
          xp?: number
          gold?: number
        }
      >('/api/auth/login', {
        method: 'POST',
        body: JSON.stringify(loginForm),
      })

      setAccount({ accountId: loggedInAccount.accountId, username: loggedInAccount.username })
      setSession(null)
      await refreshPhaseTwoState(loggedInAccount.accountId)
      pushActivity(
        `${loggedInAccount.username} logged in at level ${loggedInAccount.level ?? 1}.`,
      )
      pushActivity('Hub state loaded: chat and active adventurer count refreshed.')
      setLoginForm((current) => ({ ...current, password: '' }))
    })
  }

  async function handleRefreshHub() {
    if (!account) {
      return
    }

    await withSubmission(async () => {
      await loadHubOverview(account.accountId)
      pushActivity('Refreshed hub overview and chat feed.')
    })
  }

  async function handleSendHubMessage() {
    if (!account) {
      return
    }

    const message = hubMessageDraft.trim()
    if (!message) {
      return
    }

    await withSubmission(async () => {
      await requestJson<{ message: string }>('/api/hub/chat/send', {
        method: 'POST',
        body: JSON.stringify({ accountId: account.accountId, message }),
      })

      setHubMessageDraft('')
      await loadHubOverview(account.accountId)
      pushActivity(`Hub chat: ${account.username} says "${message}"`)
    })
  }

  async function handleEnterDungeon() {
    if (!account) {
      return
    }

    await withSubmission(async () => {
      const createdSession = await requestJson<{ sessionId: string; message: string }>(
        '/api/dungeon/enter',
        {
          method: 'POST',
          body: JSON.stringify({ accountId: account.accountId }),
        },
      )

      // Re-read the session after mutations so the UI always uses the canonical shape.
      const latestSession = await loadSession(createdSession.sessionId)
      pushActivity(
        `Entered the dungeon. ${latestSession.enemy.name} is waiting on turn ${latestSession.turn}.`,
      )
    })
  }

  async function handleAttack() {
    if (!session || !account) {
      return
    }

    await withSubmission(async () => {
      const result = await requestJson<CombatAttackResponse>('/api/combat/attack', {
        method: 'POST',
        body: JSON.stringify({ sessionId: session.sessionId }),
      })

      const latestSession = await loadSession(result.sessionId)
      await Promise.all([loadAvailableQuests(account.accountId), loadQuestLog(account.accountId)])
      pushActivity(
        `Attack resolved: ${result.outcome}. ${latestSession.enemy.name} is at ${latestSession.enemy.hp}/${latestSession.enemy.maxHp} HP.`,
      )

      if (result.questProgress && result.questProgress.length > 0) {
        for (const update of result.questProgress) {
          pushActivity(
            `Quest ${update.questId} is now ${formatQuestStatus(update.status)} (${update.progressCount}).`,
          )
        }
      }
    })
  }

  async function handleRetreat() {
    if (!session) {
      return
    }

    await withSubmission(async () => {
      const result = await requestJson<{ message: string; sessionId: string }>('/api/combat/retreat', {
        method: 'POST',
        body: JSON.stringify({ sessionId: session.sessionId }),
      })

      const latestSession = await loadSession(result.sessionId)
      pushActivity(`Combat ended: ${result.message}. Current state is ${latestSession.status}.`)
    })
  }

  async function handleRefreshProgress() {
    if (!account) {
      return
    }

    await withSubmission(async () => {
      await refreshPhaseTwoState(account.accountId)
      pushActivity('Refreshed progression, quests, and inventory.')
    })
  }

  async function handleAcceptQuest(questId: string) {
    if (!account) {
      return
    }

    await withSubmission(async () => {
      await requestJson<{ message: string }>('/api/quests/accept', {
        method: 'POST',
        body: JSON.stringify({ accountId: account.accountId, questId }),
      })

      await Promise.all([
        loadAvailableQuests(account.accountId),
        loadQuestLog(account.accountId),
      ])

      pushActivity(`Accepted quest ${questId}.`)
    })
  }

  async function handleCompleteQuest(questId: string) {
    if (!account) {
      return
    }

    await withSubmission(async () => {
      const result = await requestJson<CompleteQuestResponse>('/api/quests/complete', {
        method: 'POST',
        body: JSON.stringify({ accountId: account.accountId, questId }),
      })

      await refreshPhaseTwoState(account.accountId)
      pushActivity(
        `Completed ${questId}. +${result.rewards.xp} XP, +${result.rewards.gold} gold, loot: ${result.rewards.loot.name}.`,
      )

      if (result.progression.leveledUp) {
        pushActivity(
          `Level up! You gained ${result.progression.levelsGained} level(s) and reached level ${result.progression.level}.`,
        )
      }
    })
  }

  async function handleAbandonQuest(questId: string) {
    if (!account) {
      return
    }

    await withSubmission(async () => {
      await requestJson<{ message: string }>('/api/quests/abandon', {
        method: 'POST',
        body: JSON.stringify({ accountId: account.accountId, questId }),
      })

      await Promise.all([
        loadAvailableQuests(account.accountId),
        loadQuestLog(account.accountId),
      ])

      pushActivity(`Abandoned quest ${questId}.`)
    })
  }

  async function handleUseItemInCombat(itemCode: string) {
    if (!account || !session) {
      return
    }

    await withSubmission(async () => {
      const result = await requestJson<{
        message: string
        outcome: string
        amount: number
        remainingQuantity: number
      }>('/api/inventory/use-combat', {
        method: 'POST',
        body: JSON.stringify({
          accountId: account.accountId,
          sessionId: session.sessionId,
          itemCode,
        }),
      })

      await Promise.all([
        loadSession(session.sessionId),
        loadInventory(account.accountId),
      ])

      pushActivity(
        `Used ${itemCode}. Outcome: ${result.outcome}, amount: ${result.amount}, remaining: ${result.remainingQuantity}.`,
      )
    })
  }

  const playerHpPercent = session ? toPercent(session.player.hp, session.player.maxHp) : 0
  const enemyHpPercent = session ? toPercent(session.enemy.hp, session.enemy.maxHp) : 0
  const hasAccount = account !== null
  const usableCombatItems = useMemo(
    () => inventoryItems.filter((item) => isCombatConsumable(item.itemCode)),
    [inventoryItems],
  )

  return (
    <main className="app-shell">
      <section className="hero-panel panel">
        <div className="eyebrow-row">
          <span className="eyebrow">Phase 3 foundation</span>
          <span className={`status-pill status-${apiStatus}`}>Backend {apiStatus}</span>
        </div>

        <div className="hero-copy">
          <div>
            <h1>Dungeon crawler control room</h1>
            <p>
              Phase 3 starts here: gather in the hub overworld, chat with other adventurers, then
              run quests, combat, and inventory actions.
            </p>
          </div>

          <div className="hero-stats">
            <div>
              <span className="stat-label">Account</span>
              <strong>{account ? account.username : 'No adventurer selected'}</strong>
            </div>
            <div>
              <span className="stat-label">Level</span>
              <strong>{progress?.level ?? 1}</strong>
            </div>
            <div>
              <span className="stat-label">XP / Gold</span>
              <strong>{`${progress?.xp ?? 0} / ${progress?.gold ?? 0}`}</strong>
            </div>
            <div>
              <span className="stat-label">Hub adventurers</span>
              <strong>{activeAdventurerCount}</strong>
            </div>
          </div>
        </div>

        {errorMessage ? <p className="error-banner">{errorMessage}</p> : null}
      </section>

      <section className="workspace-grid">
        <div className="panel stack-panel auth-panel">
          <div>
            <h2>Account access</h2>
            <p>Create an account or sign into an existing adventurer.</p>
          </div>

          <form className="auth-form" onSubmit={handleRegister}>
            <label>
              <span>Register username</span>
              <input
                value={registerForm.username}
                onChange={(event) => updateRegisterField('username', event.target.value)}
                placeholder="RogueCartographer"
              />
            </label>
            <label>
              <span>Register password</span>
              <input
                type="password"
                value={registerForm.password}
                onChange={(event) => updateRegisterField('password', event.target.value)}
                placeholder="At least 8 characters"
              />
            </label>
            <button type="submit" disabled={isSubmitting || apiStatus !== 'online'}>
              Create account
            </button>
          </form>

          <form className="auth-form" onSubmit={handleLogin}>
            <label>
              <span>Login username</span>
              <input
                value={loginForm.username}
                onChange={(event) => updateLoginField('username', event.target.value)}
                placeholder="RogueCartographer"
              />
            </label>
            <label>
              <span>Login password</span>
              <input
                type="password"
                value={loginForm.password}
                onChange={(event) => updateLoginField('password', event.target.value)}
                placeholder="Your password"
              />
            </label>
            <button type="submit" disabled={isSubmitting || apiStatus !== 'online'}>
              Log in
            </button>
          </form>

          <button
            type="button"
            className="secondary"
            onClick={() => void handleRefreshProgress()}
            disabled={!hasAccount || isSubmitting || apiStatus !== 'online'}
          >
            Refresh phase 2 data
          </button>
        </div>

        <div className="panel stack-panel command-panel">
          <div>
            <h2>Combat and consumables</h2>
            <p>
              Enter the room, attack to progress kill quests, and use minor healing potions when
              your HP drops.
            </p>
          </div>

          <div className="command-row">
            <button
              type="button"
              className="secondary"
              onClick={() => void handleEnterDungeon()}
              disabled={!hasAccount || isSubmitting || apiStatus !== 'online'}
            >
              Enter dungeon
            </button>
            <button
              type="button"
              className="secondary"
              onClick={() => session && void loadSession(session.sessionId)}
              disabled={!session || isSubmitting || apiStatus !== 'online'}
            >
              Refresh session
            </button>
          </div>

          <div className="adventurer-card">
            <div>
              <span className="stat-label">Logged-in adventurer</span>
              <strong>{account?.username ?? 'Waiting for login'}</strong>
            </div>
            <div className="account-metrics">
              <div>
                <span className="stat-label">Level</span>
                <strong>{progress?.level ?? 1}</strong>
              </div>
              <div>
                <span className="stat-label">XP</span>
                <strong>{progress?.xp ?? 0}</strong>
              </div>
              <div>
                <span className="stat-label">Gold</span>
                <strong>{progress?.gold ?? 0}</strong>
              </div>
            </div>
          </div>

          <div className="panel-inset">
            <span className="stat-label">Combat consumables</span>
            {usableCombatItems.length === 0 ? (
              <p>No combat-usable items in inventory.</p>
            ) : (
              <div className="item-list">
                {usableCombatItems.map((item) => (
                  <button
                    type="button"
                    key={item.itemCode}
                    className="secondary"
                    onClick={() => void handleUseItemInCombat(item.itemCode)}
                    disabled={
                      !session ||
                      session.isCompleted ||
                      isSubmitting ||
                      apiStatus !== 'online' ||
                      item.quantity <= 0
                    }
                  >
                    {item.itemName} x{item.quantity}
                  </button>
                ))}
              </div>
            )}
          </div>

          <div className="combat-grid">
            <article className="combat-card player-card">
              <div className="combat-card-header">
                <div>
                  <span className="eyebrow">Player</span>
                  <h3>{session?.username ?? account?.username ?? 'Adventurer'}</h3>
                </div>
                <strong>
                  {session ? `${session.player.hp}/${session.player.maxHp}` : '--/--'}
                </strong>
              </div>
              <div className="hp-bar">
                <span style={{ width: `${playerHpPercent}%` }} />
              </div>
            </article>

            <article className="combat-card enemy-card">
              <div className="combat-card-header">
                <div>
                  <span className="eyebrow">Enemy</span>
                  <h3>{session?.enemy.name ?? 'Unknown foe'}</h3>
                </div>
                <strong>{session ? `${session.enemy.hp}/${session.enemy.maxHp}` : '--/--'}</strong>
              </div>
              <div className="hp-bar enemy-bar">
                <span style={{ width: `${enemyHpPercent}%` }} />
              </div>
            </article>
          </div>

          <div className="command-row">
            <button
              type="button"
              onClick={() => void handleAttack()}
              disabled={!session || session.isCompleted || isSubmitting || apiStatus !== 'online'}
            >
              Attack
            </button>
            <button
              type="button"
              className="danger"
              onClick={() => void handleRetreat()}
              disabled={!session || session.isCompleted || isSubmitting || apiStatus !== 'online'}
            >
              Retreat
            </button>
          </div>

          <dl className="session-meta">
            <div>
              <dt>Session id</dt>
              <dd>{session?.sessionId ?? 'No active session'}</dd>
            </div>
            <div>
              <dt>Status</dt>
              <dd>{session?.status ?? 'idle'}</dd>
            </div>
            <div>
              <dt>Turn</dt>
              <dd>{session?.turn ?? '-'}</dd>
            </div>
          </dl>
        </div>

        <div className="panel stack-panel quest-panel">
          <div>
            <h2>Hub chat, quests, and inventory</h2>
            <p>Talk in the hub, manage quest lifecycle, and keep your item stacks organized.</p>
          </div>

          <div className="panel-inset">
            <div className="eyebrow-row">
              <h3>Hub chat</h3>
              <button
                type="button"
                className="secondary"
                onClick={() => void handleRefreshHub()}
                disabled={!hasAccount || isSubmitting || apiStatus !== 'online'}
              >
                Refresh hub
              </button>
            </div>

            <div className="chat-compose-row">
              <input
                value={hubMessageDraft}
                onChange={(event) => setHubMessageDraft(event.target.value)}
                placeholder="Message the hub overworld..."
                maxLength={280}
                disabled={!hasAccount || isSubmitting || apiStatus !== 'online'}
              />
              <button
                type="button"
                onClick={() => void handleSendHubMessage()}
                disabled={
                  !hasAccount ||
                  isSubmitting ||
                  apiStatus !== 'online' ||
                  hubMessageDraft.trim().length === 0
                }
              >
                Send
              </button>
            </div>

            {hubChatMessages.length === 0 ? (
              <p>No hub messages yet. Be the first to post.</p>
            ) : (
              <ul className="chat-feed">
                {hubChatMessages.map((message) => (
                  <li key={message.messageId}>
                    <strong>{message.username}</strong>: {message.message}
                  </li>
                ))}
              </ul>
            )}
          </div>

          <div className="quest-list">
            {availableQuests.length === 0 ? (
              <p>No quests loaded yet. Log in and refresh data.</p>
            ) : (
              availableQuests.map((quest) => (
                <article className="quest-card" key={quest.questId}>
                  <div className="eyebrow-row">
                    <span className="eyebrow">{quest.questId}</span>
                    <span className={`status-pill quest-status-${quest.status}`}>
                      {formatQuestStatus(quest.status)}
                    </span>
                  </div>
                  <h3>{quest.name}</h3>
                  <p>{quest.description}</p>
                  <p className="quest-progress">
                    Progress {quest.progressCount}/{quest.requiredEnemyDefeats} | Reward +
                    {quest.reward.xp} XP, +{quest.reward.gold} gold, item:{' '}
                    {quest.reward.itemCode ?? 'none'}
                  </p>
                  <div className="command-row">
                    <button
                      type="button"
                      onClick={() => void handleAcceptQuest(quest.questId)}
                      disabled={
                        !hasAccount ||
                        !canAcceptQuest(quest.status) ||
                        isSubmitting ||
                        apiStatus !== 'online'
                      }
                    >
                      Accept
                    </button>
                    <button
                      type="button"
                      className="secondary"
                      onClick={() => void handleCompleteQuest(quest.questId)}
                      disabled={
                        !hasAccount ||
                        !canCompleteQuest(quest.status) ||
                        isSubmitting ||
                        apiStatus !== 'online'
                      }
                    >
                      Complete
                    </button>
                    <button
                      type="button"
                      className="danger"
                      onClick={() => void handleAbandonQuest(quest.questId)}
                      disabled={
                        !hasAccount ||
                        !canAbandonQuest(quest.status) ||
                        isSubmitting ||
                        apiStatus !== 'online'
                      }
                    >
                      Abandon
                    </button>
                  </div>
                </article>
              ))
            )}
          </div>

          <div className="panel-inset">
            <h3>Quest log</h3>
            {questLog.length === 0 ? (
              <p>No accepted quests yet.</p>
            ) : (
              <ul className="compact-list">
                {questLog.map((quest) => (
                  <li key={`${quest.questId}-${quest.acceptedAt}`}>
                    {quest.name}: {formatQuestStatus(quest.status)} ({quest.progressCount}/
                    {quest.requiredEnemyDefeats})
                  </li>
                ))}
              </ul>
            )}
          </div>

          <div className="panel-inset">
            <h3>Inventory</h3>
            {inventoryItems.length === 0 ? (
              <p>No inventory items.</p>
            ) : (
              <ul className="compact-list">
                {inventoryItems.map((item) => (
                  <li key={item.itemCode}>
                    {item.itemName} ({item.rarity}) x{item.quantity}
                  </li>
                ))}
              </ul>
            )}
          </div>

          <div className="panel-inset">
            <h3>Activity trace</h3>
            <ol className="activity-feed">
              {activity.map((entry) => (
                <li key={entry}>{entry}</li>
              ))}
            </ol>
          </div>

          {progress ? (
            <p className="level-hint">
              XP to next level: {progress.levelProgress.xpToNextLevel}.
            </p>
          ) : null}
        </div>
      </section>
    </main>
  )
}

export default App
