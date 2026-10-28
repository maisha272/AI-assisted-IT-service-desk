using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Services;

namespace ServiceDesk.Tests.Domain;

public class TicketWorkflowTests
{
    private static readonly DateTime Now = new(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly User Agent = new() { Id = 1, Role = UserRole.Agent, DisplayName = "Agent" };
    private static readonly User ClientUser = new() { Id = 2, Role = UserRole.ClientStaff, DisplayName = "Client" };

    private static Ticket TicketIn(TicketStatus status) => new() { Status = status, CreatedAt = Now.AddHours(-2) };

    [Theory]
    [InlineData(TicketStatus.New, TicketStatus.InProgress)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Waiting)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Resolved)]
    [InlineData(TicketStatus.Waiting, TicketStatus.InProgress)]
    [InlineData(TicketStatus.Waiting, TicketStatus.Resolved)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Closed)]
    [InlineData(TicketStatus.Resolved, TicketStatus.InProgress)]
    public void Agent_can_make_valid_transitions(TicketStatus from, TicketStatus to)
    {
        Assert.True(TicketWorkflow.CanTransition(from, to, UserRole.Agent));
    }

    [Theory]
    [InlineData(TicketStatus.New, TicketStatus.Resolved)]   // must be worked on first
    [InlineData(TicketStatus.New, TicketStatus.Closed)]
    [InlineData(TicketStatus.New, TicketStatus.Waiting)]
    [InlineData(TicketStatus.InProgress, TicketStatus.New)]  // can't go backwards to New
    [InlineData(TicketStatus.Waiting, TicketStatus.Closed)]
    [InlineData(TicketStatus.Closed, TicketStatus.InProgress)] // closed is final
    [InlineData(TicketStatus.Closed, TicketStatus.New)]
    public void Agent_cannot_make_invalid_transitions(TicketStatus from, TicketStatus to)
    {
        Assert.False(TicketWorkflow.CanTransition(from, to, UserRole.Agent));
    }

    [Fact]
    public void Client_can_only_reply_close_or_reopen()
    {
        Assert.Equal([TicketStatus.Closed, TicketStatus.InProgress],
            TicketWorkflow.AllowedNextStatuses(TicketStatus.Resolved, UserRole.ClientStaff));
        Assert.Equal([TicketStatus.InProgress],
            TicketWorkflow.AllowedNextStatuses(TicketStatus.Waiting, UserRole.ClientStaff));
        Assert.Empty(TicketWorkflow.AllowedNextStatuses(TicketStatus.New, UserRole.ClientStaff));
        Assert.Empty(TicketWorkflow.AllowedNextStatuses(TicketStatus.InProgress, UserRole.ClientStaff));
    }

    [Fact]
    public void Transition_records_audit_entry_and_updates_timestamps()
    {
        var ticket = TicketIn(TicketStatus.InProgress);

        var change = TicketWorkflow.Transition(ticket, TicketStatus.Resolved, Agent, Now);

        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.Equal(Now, ticket.ResolvedAt);
        Assert.Equal(Now, ticket.UpdatedAt);
        Assert.Equal(TicketStatus.InProgress, change.FromStatus);
        Assert.Equal(TicketStatus.Resolved, change.ToStatus);
        Assert.Equal(Agent.Id, change.ChangedById);
        Assert.Contains(change, ticket.StatusChanges);
    }

    [Fact]
    public void Closing_sets_closed_at()
    {
        var ticket = TicketIn(TicketStatus.Resolved);
        TicketWorkflow.Transition(ticket, TicketStatus.Closed, ClientUser, Now);
        Assert.Equal(Now, ticket.ClosedAt);
    }

    [Fact]
    public void Reopening_clears_resolved_at()
    {
        var ticket = TicketIn(TicketStatus.Resolved);
        ticket.ResolvedAt = Now.AddHours(-1);

        TicketWorkflow.Transition(ticket, TicketStatus.InProgress, ClientUser, Now);

        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Null(ticket.ResolvedAt);
    }

    [Fact]
    public void Invalid_transition_throws_and_leaves_ticket_unchanged()
    {
        var ticket = TicketIn(TicketStatus.New);

        Assert.Throws<InvalidTransitionException>(() =>
            TicketWorkflow.Transition(ticket, TicketStatus.Closed, Agent, Now));

        Assert.Equal(TicketStatus.New, ticket.Status);
        Assert.Empty(ticket.StatusChanges);
    }

    [Fact]
    public void Client_cannot_resolve_their_own_ticket()
    {
        var ticket = TicketIn(TicketStatus.InProgress);
        Assert.Throws<InvalidTransitionException>(() =>
            TicketWorkflow.Transition(ticket, TicketStatus.Resolved, ClientUser, Now));
    }
}
