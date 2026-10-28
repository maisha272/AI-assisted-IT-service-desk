import type {
  SlaState,
  Agent,
  Client,
  CreateTicketRequest,
  LoginResponse,
  TicketDetail,
  TicketPriority,
  TicketStatus,
  TicketSummary,
  TicketCategory,
} from './types'

/** Error thrown for non-2xx responses. `message` is taken from the API's ProblemDetails when present. */
export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

// The JWT lives in memory here (and in localStorage via AuthContext) and is sent on every request.
let authToken: string | null = null
let onUnauthorized: (() => void) | null = null

export const setAuthToken = (token: string | null) => {
  authToken = token
}

export const setUnauthorizedHandler = (handler: (() => void) | null) => {
  onUnauthorized = handler
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  if (init.body) headers.set('Content-Type', 'application/json')
  if (authToken) headers.set('Authorization', `Bearer ${authToken}`)

  const response = await fetch(`/api${path}`, { ...init, headers })

  // An expired/invalid token on a normal request means the session is over.
  if (response.status === 401 && authToken) onUnauthorized?.()

  if (!response.ok) {
    let message = `Request failed (${response.status})`
    try {
      const problem = await response.json()
      // ASP.NET Core returns RFC 9457 "problem details"; validation errors are under `errors`.
      const firstValidationError = problem.errors
        ? (Object.values(problem.errors).flat()[0] as string | undefined)
        : undefined
      message = firstValidationError ?? problem.detail ?? problem.title ?? message
    } catch {
      // Body wasn't JSON; keep the generic message.
    }
    throw new ApiError(response.status, message)
  }

  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

const post = <T>(path: string, body: unknown) =>
  request<T>(path, { method: 'POST', body: JSON.stringify(body) })

export interface TicketFilters {
  status?: TicketStatus
  priority?: TicketPriority
  sla?: SlaState
  search?: string
}

export const api = {
  login: (email: string, password: string) => post<LoginResponse>('/auth/login', { email, password }),
  listTickets(filters: TicketFilters = {}) {
    const params = new URLSearchParams()
    for (const [key, value] of Object.entries(filters)) if (value) params.set(key, value)
    const qs = params.toString()
    return request<TicketSummary[]>(`/tickets${qs ? `?${qs}` : ''}`)
  },
  getTicket: (id: number) => request<TicketDetail>(`/tickets/${id}`),
  createTicket: (body: CreateTicketRequest) => post<TicketDetail>('/tickets', body),
  addComment: (id: number, body: string, isInternal: boolean) =>
    post<TicketDetail>(`/tickets/${id}/comments`, { body, isInternal }),
  changeStatus: (id: number, status: TicketStatus) =>
    post<TicketDetail>(`/tickets/${id}/status`, { status }),
  updateTicket: (id: number, changes: { priority?: TicketPriority; category?: TicketCategory }) =>
    request<TicketDetail>(`/tickets/${id}`, { method: 'PATCH', body: JSON.stringify(changes) }),
  assignTicket: (id: number, agentId: number | null) =>
    post<TicketDetail>(`/tickets/${id}/assign`, { agentId }),
  listClients: () => request<Client[]>('/clients'),
  listAgents: () => request<Agent[]>('/agents'),
}
