// TypeScript mirrors of the API's DTOs (see src/ServiceDesk.Api/Dtos). Keep these in sync with the C# records.

export const TICKET_STATUSES = ['New', 'InProgress', 'Waiting', 'Resolved', 'Closed'] as const
export type TicketStatus = (typeof TICKET_STATUSES)[number]

export const TICKET_PRIORITIES = ['Critical', 'High', 'Medium', 'Low'] as const
export type TicketPriority = (typeof TICKET_PRIORITIES)[number]

export const TICKET_CATEGORIES = [
  'Hardware',
  'Software',
  'Network',
  'AccountAccess',
  'Email',
  'Printing',
  'Security',
  'Other',
] as const
export type TicketCategory = (typeof TICKET_CATEGORIES)[number]

export const SLA_STATES = ['OnTrack', 'AtRisk', 'Breached', 'Met'] as const
export type SlaState = (typeof SLA_STATES)[number]

export interface Sla {
  responseDueAt: string
  resolutionDueAt: string
  firstRespondedAt: string | null
  responseState: SlaState
  resolutionState: SlaState
  overall: SlaState
}

export type UserRole = 'ClientStaff' | 'Agent'
export type TicketSource = 'Portal' | 'Email'

export interface CurrentUser {
  id: number
  email: string
  displayName: string
  role: UserRole
  clientId: number | null
  clientName: string | null
}

export interface LoginResponse {
  token: string
  expiresAt: string
  user: CurrentUser
}

export interface TicketSummary {
  id: number
  title: string
  status: TicketStatus
  priority: TicketPriority
  category: TicketCategory
  source: TicketSource
  clientId: number
  clientName: string
  createdByName: string
  assignedToName: string | null
  createdAt: string
  updatedAt: string
  sla: Sla
}

export interface Comment {
  id: number
  authorName: string
  authorRole: UserRole
  body: string
  isInternal: boolean
  createdAt: string
}

export interface StatusChange {
  fromStatus: TicketStatus
  toStatus: TicketStatus
  changedByName: string
  changedAt: string
}

export interface TicketDetail {
  id: number
  title: string
  description: string
  status: TicketStatus
  priority: TicketPriority
  category: TicketCategory
  source: TicketSource
  clientId: number
  clientName: string
  createdByName: string
  assignedToId: number | null
  assignedToName: string | null
  createdAt: string
  updatedAt: string
  resolvedAt: string | null
  closedAt: string | null
  sla: Sla
  allowedNextStatuses: TicketStatus[]
  comments: Comment[]
  statusChanges: StatusChange[]
}

export interface CreateTicketRequest {
  title: string
  description: string
  priority: TicketPriority
  category: TicketCategory
  clientId?: number
}

export interface Dashboard {
  openTickets: number
  unassignedTickets: number
  breachedTickets: number
  atRiskTickets: number
  openByPriority: { priority: TicketPriority; count: number }[]
  openByStatus: { status: TicketStatus; count: number }[]
  resolvedLast30Days: number
  averageResolutionMinutesLast30Days: number | null
  slaCompliancePercentLast30Days: number | null
  needsAttention: TicketSummary[]
}

export interface Client {
  id: number
  name: string
}

export interface Agent {
  id: number
  displayName: string
}
