namespace ServiceDesk.Api.Domain;

/// <summary>Lifecycle of a ticket. Allowed moves are defined in <see cref="Services.TicketWorkflow"/>.</summary>
public enum TicketStatus
{
    New,
    InProgress,
    Waiting,
    Resolved,
    Closed
}

public enum TicketPriority
{
    Critical,
    High,
    Medium,
    Low
}

public enum TicketCategory
{
    Hardware,
    Software,
    Network,
    AccountAccess,
    Email,
    Printing,
    Security,
    Other
}

public enum UserRole
{
    /// <summary>Staff member of a client company: can create and view their own tickets.</summary>
    ClientStaff,

    /// <summary>Service desk agent: can view and manage all tickets.</summary>
    Agent
}

public enum TicketSource
{
    Portal,
    Email
}
