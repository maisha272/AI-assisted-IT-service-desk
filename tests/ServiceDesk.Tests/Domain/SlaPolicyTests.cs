using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Services;

namespace ServiceDesk.Tests.Domain;

public class SlaPolicyTests
{
    private static readonly DateTime Created = new(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);

    private static Ticket NewTicket(TicketPriority priority)
    {
        var ticket = new Ticket { Priority = priority, CreatedAt = Created };
        SlaPolicy.ApplyDeadlines(ticket);
        return ticket;
    }

    [Theory]
    [InlineData(TicketPriority.Critical, 1, 4)]
    [InlineData(TicketPriority.High, 4, 24)]
    [InlineData(TicketPriority.Medium, 8, 72)]
    [InlineData(TicketPriority.Low, 24, 120)]
    public void Deadlines_are_measured_from_creation_by_priority(TicketPriority priority, int responseHours, int resolutionHours)
    {
        var ticket = NewTicket(priority);

        Assert.Equal(Created.AddHours(responseHours), ticket.ResponseDueAt);
        Assert.Equal(Created.AddHours(resolutionHours), ticket.ResolutionDueAt);
    }

    [Fact]
    public void Changing_priority_recalculates_deadlines_from_creation()
    {
        var ticket = NewTicket(TicketPriority.Low);

        ticket.Priority = TicketPriority.Critical;
        SlaPolicy.ApplyDeadlines(ticket);

        Assert.Equal(Created.AddHours(4), ticket.ResolutionDueAt);
    }

    [Theory]
    // 4h window: at-risk once 25% (1h) or less remains, i.e. from 3h after start.
    [InlineData(0, SlaState.OnTrack)]
    [InlineData(2.9, SlaState.OnTrack)]
    [InlineData(3.0, SlaState.AtRisk)]   // exactly 25% left counts as at risk
    [InlineData(4.0, SlaState.AtRisk)]   // the deadline itself is not yet a breach
    [InlineData(4.01, SlaState.Breached)]
    public void Running_sla_state_depends_on_time_remaining(double hoursElapsed, SlaState expected)
    {
        var due = Created.AddHours(4);
        var now = Created.AddHours(hoursElapsed);

        Assert.Equal(expected, SlaPolicy.Evaluate(Created, due, completedAt: null, now));
    }

    [Fact]
    public void Completed_on_time_is_met_even_if_now_is_past_the_deadline()
    {
        var due = Created.AddHours(4);
        Assert.Equal(SlaState.Met, SlaPolicy.Evaluate(Created, due, completedAt: due, now: due.AddDays(10)));
    }

    [Fact]
    public void Completed_late_is_breached()
    {
        var due = Created.AddHours(4);
        Assert.Equal(SlaState.Breached, SlaPolicy.Evaluate(Created, due, completedAt: due.AddMinutes(1), now: due.AddDays(1)));
    }

    [Fact]
    public void First_agent_status_change_stops_the_response_clock()
    {
        var ticket = NewTicket(TicketPriority.High);
        var agent = new User { Id = 1, Role = UserRole.Agent };

        TicketWorkflow.Transition(ticket, TicketStatus.InProgress, agent, Created.AddHours(1));
        var sla = SlaPolicy.Evaluate(ticket, now: Created.AddHours(10));

        Assert.Equal(Created.AddHours(1), ticket.FirstRespondedAt);
        Assert.Equal(SlaState.Met, sla.ResponseState);
        Assert.Equal(SlaState.OnTrack, sla.ResolutionState);
    }

    [Fact]
    public void Client_actions_do_not_count_as_a_response()
    {
        var ticket = NewTicket(TicketPriority.High);
        ticket.Status = TicketStatus.Resolved;
        ticket.ResolvedAt = Created.AddHours(1);
        var client = new User { Id = 2, Role = UserRole.ClientStaff };

        TicketWorkflow.Transition(ticket, TicketStatus.InProgress, client, Created.AddHours(2));

        Assert.Null(ticket.FirstRespondedAt);
    }

    [Fact]
    public void First_response_time_is_not_overwritten_by_later_actions()
    {
        var ticket = NewTicket(TicketPriority.High);
        SlaPolicy.RecordAgentResponse(ticket, Created.AddHours(1));
        SlaPolicy.RecordAgentResponse(ticket, Created.AddHours(5));
        Assert.Equal(Created.AddHours(1), ticket.FirstRespondedAt);
    }

    [Fact]
    public void Unanswered_critical_ticket_breaches_response_after_one_hour()
    {
        var ticket = NewTicket(TicketPriority.Critical);

        var sla = SlaPolicy.Evaluate(ticket, now: Created.AddMinutes(61));

        Assert.Equal(SlaState.Breached, sla.ResponseState);
        Assert.Equal(SlaState.Breached, sla.Overall);
    }

    [Fact]
    public void Overall_state_is_the_worse_of_response_and_resolution()
    {
        var ticket = NewTicket(TicketPriority.Medium);   // response 8h, resolution 72h
        var sla = SlaPolicy.Evaluate(ticket, now: Created.AddHours(7)); // response at risk, resolution on track

        Assert.Equal(SlaState.AtRisk, sla.ResponseState);
        Assert.Equal(SlaState.OnTrack, sla.ResolutionState);
        Assert.Equal(SlaState.AtRisk, sla.Overall);
    }

    [Fact]
    public void Reopened_ticket_resumes_the_original_resolution_clock()
    {
        var ticket = NewTicket(TicketPriority.Critical);
        var agent = new User { Id = 1, Role = UserRole.Agent };
        var client = new User { Id = 2, Role = UserRole.ClientStaff };
        TicketWorkflow.Transition(ticket, TicketStatus.InProgress, agent, Created.AddMinutes(10));
        TicketWorkflow.Transition(ticket, TicketStatus.Resolved, agent, Created.AddHours(1));

        TicketWorkflow.Transition(ticket, TicketStatus.InProgress, client, Created.AddHours(5)); // reopened

        Assert.Equal(SlaState.Breached, SlaPolicy.Evaluate(ticket, Created.AddHours(5)).ResolutionState);
    }
}
