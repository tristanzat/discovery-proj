import type { HubConnection } from '@microsoft/signalr'
import { useEffect, useMemo, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import './App.css'
import { createGameConnection, isConnectionReady } from './realtime/gameRealtime'

type ApiStatus = 'checking' | 'online' | 'offline'
type RealtimeStatus = 'disconnected' | 'connecting' | 'connected'

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
  floor: number
  roomNumber: number
  totalRooms: number
  roomsCleared: number
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

type HubRosterEntry = {
  accountId: number
  username: string
  level: number
  gold: number
  lastSeenAt: string
  lastSavedAt: string | null
}

type HubRosterResponse = {
  accountId: number
  rosterCount: number
  roster: HubRosterEntry[]
}

type TradeOffer = {
  tradeOfferId: number
  fromAccountId: number
  fromUsername: string
  toAccountId: number
  toUsername: string
  itemCode: string
  itemName: string
  rarity: string
  quantity: number
  note: string | null
  status: string
  createdAt: string
  respondedAt: string | null
}

type TradeOffersResponse = {
  accountId: number
  offerCount: number
  offers: TradeOffer[]
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

type AdvanceDungeonRoomResponse = {
  message: string
  sessionId: string
  status: string
  isCompleted: boolean
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

type AccountChangeEvent = {
  accountId: number
  changedAt: string
}

type DungeonSessionChangeEvent = {
  accountId: number
  sessionId: string
  status: string
  isCompleted: boolean
  changedAt: string
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
  const [realtimeStatus, setRealtimeStatus] = useState<RealtimeStatus>('disconnected')
  const [registerForm, setRegisterForm] = useState<AuthForm>({ username: '', password: '' })
  const [loginForm, setLoginForm] = useState<AuthForm>({ username: '', password: '' })
  const [account, setAccount] = useState<AccountSummary | null>(null)
  const [progress, setProgress] = useState<ProgressSummary | null>(null)
  const [session, setSession] = useState<DungeonSession | null>(null)
  const [availableQuests, setAvailableQuests] = useState<QuestBoardItem[]>([])
  const [questLog, setQuestLog] = useState<QuestLogItem[]>([])
  const [inventoryItems, setInventoryItems] = useState<InventoryItem[]>([])
  const [hubChatMessages, setHubChatMessages] = useState<HubChatMessage[]>([])
  const [hubRoster, setHubRoster] = useState<HubRosterEntry[]>([])
  const [activeAdventurerCount, setActiveAdventurerCount] = useState(0)
  const [hubMessageDraft, setHubMessageDraft] = useState('')
  const [tradeOffers, setTradeOffers] = useState<TradeOffer[]>([])
  const [tradeRecipientUsername, setTradeRecipientUsername] = useState('')
  const [tradeItemCode, setTradeItemCode] = useState('')
  const [tradeQuantity, setTradeQuantity] = useState(1)
  const [tradeNote, setTradeNote] = useState('')
  const [activity, setActivity] = useState<string[]>([
    'Welcome to this multiplayer dungeon crawler.',
  ])
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const accountRef = useRef<AccountSummary | null>(null)
  const sessionRef = useRef<DungeonSession | null>(null)
  const connectionRef = useRef<HubConnection | null>(null)
  const joinedAccountIdRef = useRef<number | null>(null)
  const joinedSessionIdRef = useRef<string | null>(null)
  const joinedHubRef = useRef(false)

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

  function appendHubChatMessage(message: HubChatMessage) {
    setHubChatMessages((current) => {
      if (current.some((entry) => entry.messageId === message.messageId)) {
        return current
      }

      return [...current, message].slice(-30)
    })
  }

  function updateRegisterField(field: keyof AuthForm, value: string) {
    setRegisterForm((current) => ({ ...current, [field]: value }))
  }

  function updateLoginField(field: keyof AuthForm, value: string) {
    setLoginForm((current) => ({ ...current, [field]: value }))
  }

  async function synchronizeRealtimeGroups(nextAccountId: number | null, nextSessionId: string | null) {
    const connection = connectionRef.current
    if (!connection || !isConnectionReady(connection)) {
      return
    }

    if (joinedSessionIdRef.current && joinedSessionIdRef.current !== nextSessionId) {
      await connection.invoke('LeaveSession', joinedSessionIdRef.current)
      joinedSessionIdRef.current = null
    }

    if (joinedAccountIdRef.current && joinedAccountIdRef.current !== nextAccountId) {
      await connection.invoke('LeaveAccount', joinedAccountIdRef.current)
      joinedAccountIdRef.current = null
    }

    if (nextAccountId !== null && !joinedHubRef.current) {
      await connection.invoke('JoinHub')
      joinedHubRef.current = true
    }

    if (nextAccountId !== null && joinedAccountIdRef.current !== nextAccountId) {
      await connection.invoke('JoinAccount', nextAccountId)
      joinedAccountIdRef.current = nextAccountId
    }

    if (nextSessionId && joinedSessionIdRef.current !== nextSessionId) {
      await connection.invoke('JoinSession', nextSessionId)
      joinedSessionIdRef.current = nextSessionId
    }
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

  async function pingHubPresence(accountId: number) {
    await requestJson<{ message: string }>('/api/hub/presence/ping', {
      method: 'POST',
      body: JSON.stringify({ accountId }),
    })
  }

  async function loadHubRoster(accountId: number) {
    const response = await requestJson<HubRosterResponse>(`/api/hub/roster/${accountId}`)
    setHubRoster(response.roster)
    return response.roster
  }

  async function loadTradeOffers(accountId: number) {
    const response = await requestJson<TradeOffersResponse>(`/api/trade/offers/${accountId}`)
    setTradeOffers(response.offers)
    return response.offers
  }

  useEffect(() => {
    accountRef.current = account
  }, [account])

  useEffect(() => {
    sessionRef.current = session
  }, [session])

  useEffect(() => {
    if (apiStatus !== 'online') {
      return
    }

    const connection = createGameConnection()
    connectionRef.current = connection

    connection.onreconnecting(() => {
      setRealtimeStatus('connecting')
      return Promise.resolve()
    })

    connection.onreconnected(async () => {
      setRealtimeStatus('connected')
      joinedHubRef.current = false
      joinedAccountIdRef.current = null
      joinedSessionIdRef.current = null
      await synchronizeRealtimeGroups(
        accountRef.current?.accountId ?? null,
        sessionRef.current?.sessionId ?? null,
      )
    })

    connection.onclose(() => {
      setRealtimeStatus('disconnected')
    })

    connection.on('hubPresenceChanged', () => {
      const currentAccount = accountRef.current
      if (!currentAccount) {
        return
      }

      void Promise.all([loadHubOverview(currentAccount.accountId), loadHubRoster(currentAccount.accountId)])
    })

    connection.on('hubChatMessageReceived', (message: HubChatMessage) => {
      appendHubChatMessage(message)
    })

    connection.on('tradeOffersChanged', (payload: AccountChangeEvent) => {
      const currentAccount = accountRef.current
      if (!currentAccount || currentAccount.accountId !== payload.accountId) {
        return
      }

      void loadTradeOffers(currentAccount.accountId)
    })

    connection.on('inventoryChanged', (payload: AccountChangeEvent) => {
      const currentAccount = accountRef.current
      if (!currentAccount || currentAccount.accountId !== payload.accountId) {
        return
      }

      void loadInventory(currentAccount.accountId)
    })

    connection.on('questsChanged', (payload: AccountChangeEvent) => {
      const currentAccount = accountRef.current
      if (!currentAccount || currentAccount.accountId !== payload.accountId) {
        return
      }

      void Promise.all([
        loadAvailableQuests(currentAccount.accountId),
        loadQuestLog(currentAccount.accountId),
      ])
    })

    connection.on('progressChanged', (payload: AccountChangeEvent) => {
      const currentAccount = accountRef.current
      if (!currentAccount || currentAccount.accountId !== payload.accountId) {
        return
      }

      void loadProgress(currentAccount.accountId)
    })

    connection.on('dungeonSessionChanged', (payload: DungeonSessionChangeEvent) => {
      const currentAccount = accountRef.current
      const currentSession = sessionRef.current

      if (!currentAccount || currentAccount.accountId !== payload.accountId) {
        return
      }

      if (!currentSession || currentSession.sessionId !== payload.sessionId) {
        return
      }

      void loadSession(payload.sessionId)
    })

    async function startRealtime() {
      try {
        setRealtimeStatus('connecting')
        await connection.start()
        setRealtimeStatus('connected')
        await synchronizeRealtimeGroups(accountRef.current?.accountId ?? null, sessionRef.current?.sessionId ?? null)
        pushActivity('Realtime channel connected.')
      } catch (error) {
        setRealtimeStatus('disconnected')
        const message = error instanceof Error ? error.message : 'unknown realtime error'
        pushActivity(`Realtime connection failed: ${message}`)
      }
    }

    void startRealtime()

    return () => {
      connectionRef.current = null
      joinedHubRef.current = false
      joinedAccountIdRef.current = null
      joinedSessionIdRef.current = null
      setRealtimeStatus('disconnected')
      void connection.stop()
    }
  }, [apiStatus])

  async function refreshPhaseTwoState(accountId: number) {
    await pingHubPresence(accountId)

    await Promise.all([
      loadProgress(accountId),
      loadAvailableQuests(accountId),
      loadQuestLog(accountId),
      loadInventory(accountId),
      loadHubOverview(accountId),
      loadHubRoster(accountId),
      loadTradeOffers(accountId),
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
      await pingHubPresence(account.accountId)
      await Promise.all([loadHubOverview(account.accountId), loadHubRoster(account.accountId)])
      pushActivity('Refreshed hub overview, roster, and chat feed.')
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
      pushActivity(`Hub chat: ${account.username} says "${message}"`)
    })
  }

  async function handleSendTradeOffer() {
    if (!account) {
      return
    }

    const toUsername = tradeRecipientUsername.trim()
    const itemCode = tradeItemCode.trim()
    const quantity = Math.max(1, Math.floor(tradeQuantity))
    const note = tradeNote.trim()
    const selectedItem = tradeableItems.find((item) => item.itemCode === itemCode)

    if (!toUsername || !itemCode) {
      return
    }

    if (!selectedItem) {
      return
    }

    if (quantity > selectedItem.quantity) {
      setErrorMessage(`You only have ${selectedItem.quantity} of ${selectedItem.itemName}.`)
      return
    }

    await withSubmission(async () => {
      await requestJson<{ message: string }>('/api/trade/offers/send', {
        method: 'POST',
        body: JSON.stringify({
          fromAccountId: account.accountId,
          toUsername,
          itemCode,
          quantity,
          note: note.length > 0 ? note : null,
        }),
      })

      setTradeRecipientUsername('')
      setTradeNote('')
      setTradeQuantity(1)
      await Promise.all([loadInventory(account.accountId), loadTradeOffers(account.accountId)])
      pushActivity(`Trade offer sent: ${quantity}x ${itemCode} to ${toUsername}.`)
    })
  }

  async function handleRespondTradeOffer(tradeOfferId: number, action: 'accept' | 'reject') {
    if (!account) {
      return
    }

    await withSubmission(async () => {
      await requestJson<{ message: string }>('/api/trade/offers/respond', {
        method: 'POST',
        body: JSON.stringify({
          accountId: account.accountId,
          tradeOfferId,
          action,
        }),
      })

      await Promise.all([loadInventory(account.accountId), loadTradeOffers(account.accountId)])
      pushActivity(`Trade offer #${tradeOfferId} ${action}ed.`)
    })
  }

  async function handleCancelTradeOffer(tradeOfferId: number) {
    if (!account) {
      return
    }

    await withSubmission(async () => {
      await requestJson<{ message: string }>('/api/trade/offers/cancel', {
        method: 'POST',
        body: JSON.stringify({
          accountId: account.accountId,
          tradeOfferId,
        }),
      })

      await Promise.all([loadInventory(account.accountId), loadTradeOffers(account.accountId)])
      pushActivity(`Trade offer #${tradeOfferId} cancelled.`)
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
        `Entered floor ${latestSession.floor}, room ${latestSession.roomNumber}/${latestSession.totalRooms}. ${latestSession.enemy.name} awaits.`,
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
        `Attack resolved: ${result.outcome}. ${latestSession.enemy.name} is at ${latestSession.enemy.hp}/${latestSession.enemy.maxHp} HP in room ${latestSession.roomNumber}/${latestSession.totalRooms}.`,
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

  async function handleAdvanceRoom() {
    if (!session) {
      return
    }

    await withSubmission(async () => {
      const result = await requestJson<AdvanceDungeonRoomResponse>('/api/dungeon/advance', {
        method: 'POST',
        body: JSON.stringify({ sessionId: session.sessionId }),
      })

      const latestSession = await loadSession(result.sessionId)

      if (latestSession.isCompleted && latestSession.status === 'victory') {
        pushActivity(`Floor ${latestSession.floor} cleared. Dungeon run complete.`)
        return
      }

      pushActivity(
        `Advanced to room ${latestSession.roomNumber}/${latestSession.totalRooms}. ${latestSession.enemy.name} appears.`,
      )
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
  const tradeableItems = inventoryItems.filter((item) => item.quantity > 0)
  const selectedTradeItem = tradeableItems.find((item) => item.itemCode === tradeItemCode)
  const maxTradeQuantity = selectedTradeItem?.quantity ?? 1
  const incomingOffers = useMemo(
    () =>
      account
        ? tradeOffers.filter(
            (offer) => offer.toAccountId === account.accountId && offer.status === 'pending',
          )
        : [],
    [account, tradeOffers],
  )
  const outgoingOffers = useMemo(
    () =>
      account
        ? tradeOffers.filter(
            (offer) => offer.fromAccountId === account.accountId && offer.status === 'pending',
          )
        : [],
    [account, tradeOffers],
  )

  useEffect(() => {
    if (!account || apiStatus !== 'online') {
      return
    }

    const intervalId = setInterval(() => {
      void pingHubPresence(account.accountId)
    }, 60000)

    return () => {
      clearInterval(intervalId)
    }
  }, [account, apiStatus])

  useEffect(() => {
    if (realtimeStatus !== 'connected') {
      return
    }

    void synchronizeRealtimeGroups(account?.accountId ?? null, session?.sessionId ?? null)
  }, [account?.accountId, session?.sessionId, realtimeStatus])

  const realtimeStatusVariant =
    realtimeStatus === 'connected'
      ? 'online'
      : realtimeStatus === 'connecting'
        ? 'checking'
        : 'offline'

  return (
    <main className="app-shell">
      <section className="hero-panel panel">
        <div className="eyebrow-row">
          <span className="eyebrow">Main page working.</span>
          <span className={`status-pill status-${apiStatus}`}>Backend {apiStatus}</span>
          <span className={`status-pill status-${realtimeStatusVariant}`}>
            Realtime {realtimeStatus}
          </span>
        </div>

        <div className="hero-copy">
          <div>
            <h1>Realtime Dungeon Crawler</h1>
            <p>
              Try the dungeon loop and trade and chat with other players.
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
            Refresh world data
          </button>
        </div>

        <div className="panel stack-panel command-panel">
          <div>
            <h2>Combat and consumables</h2>
            <p>
              Enter a generated floor, clear each room in sequence, and use combat consumables when
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
            <button
              type="button"
              className="secondary"
              onClick={() => void handleAdvanceRoom()}
              disabled={
                !session ||
                session.isCompleted ||
                session.status !== 'room-cleared' ||
                isSubmitting ||
                apiStatus !== 'online'
              }
            >
              Advance room
            </button>
          </div>

          <dl className="session-meta">
            <div>
              <dt>Floor</dt>
              <dd>{session ? `${session.floor}` : '-'}</dd>
            </div>
            <div>
              <dt>Room</dt>
              <dd>{session ? `${session.roomNumber}/${session.totalRooms}` : '-'}</dd>
            </div>
            <div>
              <dt>Rooms cleared</dt>
              <dd>{session?.roomsCleared ?? '-'}</dd>
            </div>
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

          <div className="panel-inset">
            <div className="eyebrow-row">
              <h3>Trading post</h3>
              <button
                type="button"
                className="secondary"
                onClick={() => account && void loadTradeOffers(account.accountId)}
                disabled={!hasAccount || isSubmitting || apiStatus !== 'online'}
              >
                Refresh trades
              </button>
            </div>

            <div className="trade-compose-grid">
              <label>
                <span>Recipient username</span>
                <input
                  value={tradeRecipientUsername}
                  onChange={(event) => setTradeRecipientUsername(event.target.value)}
                  placeholder="RogueCartographer"
                  disabled={!hasAccount || isSubmitting || apiStatus !== 'online'}
                />
              </label>

              <label>
                <span>Inventory item</span>
                <select
                  value={tradeItemCode}
                  onChange={(event) => setTradeItemCode(event.target.value)}
                  disabled={!hasAccount || isSubmitting || apiStatus !== 'online'}
                >
                  <option value="">Select item</option>
                  {tradeableItems.map((item) => (
                    <option key={item.itemCode} value={item.itemCode}>
                      {item.itemName} ({item.rarity}) x{item.quantity}
                    </option>
                  ))}
                </select>
              </label>

              <label>
                <span>Quantity</span>
                <input
                  type="number"
                  min={1}
                  max={maxTradeQuantity}
                  value={tradeQuantity}
                  onChange={(event) => setTradeQuantity(Number(event.target.value) || 1)}
                  disabled={!hasAccount || isSubmitting || apiStatus !== 'online'}
                />
              </label>

              <label>
                <span>Optional note</span>
                <input
                  value={tradeNote}
                  onChange={(event) => setTradeNote(event.target.value)}
                  maxLength={180}
                  placeholder="Offer from the last goblin run"
                  disabled={!hasAccount || isSubmitting || apiStatus !== 'online'}
                />
              </label>
            </div>

            <button
              type="button"
              onClick={() => void handleSendTradeOffer()}
              disabled={
                !hasAccount ||
                isSubmitting ||
                apiStatus !== 'online' ||
                tradeRecipientUsername.trim().length === 0 ||
                tradeItemCode.trim().length === 0 ||
                tradeQuantity <= 0 ||
                tradeQuantity > maxTradeQuantity
              }
            >
              Send trade offer
            </button>

            {selectedTradeItem ? (
              <p>
                Available: {selectedTradeItem.itemName} x{selectedTradeItem.quantity}
              </p>
            ) : null}

            <h3>Hub roster</h3>
            {hubRoster.length === 0 ? (
              <p>No other adventurers visible in the hub.</p>
            ) : (
              <ul className="roster-list compact-list">
                {hubRoster.map((entry) => (
                  <li key={entry.accountId}>
                    <strong>{entry.username}</strong> (Lv {entry.level}, Gold {entry.gold})
                    <div className="trade-action-row">
                      <button
                        type="button"
                        className="secondary"
                        onClick={() => setTradeRecipientUsername(entry.username)}
                        disabled={!hasAccount || isSubmitting || apiStatus !== 'online'}
                      >
                        Target for trade
                      </button>
                    </div>
                  </li>
                ))}
              </ul>
            )}

            <h3>Incoming offers</h3>
            {incomingOffers.length === 0 ? (
              <p>No incoming trade offers.</p>
            ) : (
              <ul className="compact-list">
                {incomingOffers.map((offer) => (
                  <li key={offer.tradeOfferId}>
                    From {offer.fromUsername}: {offer.itemName} x{offer.quantity}
                    <div className="trade-action-row">
                      <button
                        type="button"
                        className="secondary"
                        onClick={() => void handleRespondTradeOffer(offer.tradeOfferId, 'accept')}
                        disabled={isSubmitting || apiStatus !== 'online'}
                      >
                        Accept
                      </button>
                      <button
                        type="button"
                        className="danger"
                        onClick={() => void handleRespondTradeOffer(offer.tradeOfferId, 'reject')}
                        disabled={isSubmitting || apiStatus !== 'online'}
                      >
                        Reject
                      </button>
                    </div>
                  </li>
                ))}
              </ul>
            )}

            <h3>Outgoing offers</h3>
            {outgoingOffers.length === 0 ? (
              <p>No outgoing trade offers.</p>
            ) : (
              <ul className="compact-list">
                {outgoingOffers.map((offer) => (
                  <li key={offer.tradeOfferId}>
                    To {offer.toUsername}: {offer.itemName} x{offer.quantity}
                    <div className="trade-action-row">
                      <button
                        type="button"
                        className="danger"
                        onClick={() => void handleCancelTradeOffer(offer.tradeOfferId)}
                        disabled={isSubmitting || apiStatus !== 'online'}
                      >
                        Cancel
                      </button>
                    </div>
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
