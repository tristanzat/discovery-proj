import { useEffect, useState } from 'react'
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
  level?: number
  xp?: number
  gold?: number
  maxHp?: number
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

function App() {
  const [apiStatus, setApiStatus] = useState<ApiStatus>('checking')
  const [registerForm, setRegisterForm] = useState<AuthForm>({ username: '', password: '' })
  const [loginForm, setLoginForm] = useState<AuthForm>({ username: '', password: '' })
  const [account, setAccount] = useState<AccountSummary | null>(null)
  const [session, setSession] = useState<DungeonSession | null>(null)
  const [activity, setActivity] = useState<string[]>([
    'Welcome. Register or log in, then enter the first dungeon room.',
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
      pushActivity(`Registered adventurer ${createdAccount.username}.`)
      setRegisterForm((current) => ({ ...current, password: '' }))
      setLoginForm({ username: createdAccount.username, password: '' })
    })
  }

  async function handleLogin(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    await withSubmission(async () => {
      const loggedInAccount = await requestJson<AccountSummary>('/api/auth/login', {
        method: 'POST',
        body: JSON.stringify(loginForm),
      })

      setAccount(loggedInAccount)
      setSession(null)
      pushActivity(
        `${loggedInAccount.username} logged in at level ${loggedInAccount.level ?? 1}.`,
      )
      setLoginForm((current) => ({ ...current, password: '' }))
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
    if (!session) {
      return
    }

    await withSubmission(async () => {
      const result = await requestJson<{ outcome: string; sessionId: string }>('/api/combat/attack', {
        method: 'POST',
        body: JSON.stringify({ sessionId: session.sessionId }),
      })

      const latestSession = await loadSession(result.sessionId)
      pushActivity(
        `Attack resolved: ${result.outcome}. ${latestSession.enemy.name} is at ${latestSession.enemy.hp}/${latestSession.enemy.maxHp} HP.`,
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

  const playerHpPercent = session ? toPercent(session.player.hp, session.player.maxHp) : 0
  const enemyHpPercent = session ? toPercent(session.enemy.hp, session.enemy.maxHp) : 0

  return (
    <main className="app-shell">
      <section className="hero-panel panel">
        <div className="eyebrow-row">
          <span className="eyebrow">Phase 1 frontend</span>
          <span className={`status-pill status-${apiStatus}`}>Backend {apiStatus}</span>
        </div>

        <div className="hero-copy">
          <div>
            <h1>Dungeon crawler control room</h1>
            <p>
              This UI exercises the live .NET backend: account creation, login, room entry,
              and the first deterministic combat loop.
            </p>
          </div>

          <div className="hero-stats">
            <div>
              <span className="stat-label">Account</span>
              <strong>{account ? account.username : 'No adventurer selected'}</strong>
            </div>
            <div>
              <span className="stat-label">Room state</span>
              <strong>{session ? session.status : 'Not in dungeon'}</strong>
            </div>
            <div>
              <span className="stat-label">Turn</span>
              <strong>{session ? session.turn : '-'}</strong>
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
        </div>

        <div className="panel stack-panel command-panel">
          <div>
            <h2>Run the room loop</h2>
            <p>Once logged in, enter the dungeon and play through the API-backed combat flow.</p>
          </div>

          <div className="command-row">
            <button
              type="button"
              className="secondary"
              onClick={() => void handleEnterDungeon()}
              disabled={!account || isSubmitting || apiStatus !== 'online'}
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
                <strong>{account?.level ?? 1}</strong>
              </div>
              <div>
                <span className="stat-label">XP</span>
                <strong>{account?.xp ?? 0}</strong>
              </div>
              <div>
                <span className="stat-label">Gold</span>
                <strong>{account?.gold ?? 0}</strong>
              </div>
            </div>
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
        </div>

        <div className="panel stack-panel log-panel">
          <div>
            <h2>Session trace</h2>
            <p>Use this to observe how frontend state follows backend state transitions.</p>
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
              <dt>Complete</dt>
              <dd>{session ? String(session.isCompleted) : 'false'}</dd>
            </div>
          </dl>

          <ol className="activity-feed">
            {activity.map((entry) => (
              <li key={entry}>{entry}</li>
            ))}
          </ol>
        </div>
      </section>
    </main>
  )
}

export default App
