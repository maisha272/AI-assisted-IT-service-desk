using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Services.Integrations;
using ServiceDesk.Api.Services.Triage;

namespace ServiceDesk.Tests.Integrations;

public class InboundEmailServiceTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);
    private readonly TestDb _t = new();
    private readonly InboundEmailService _service;

    public InboundEmailServiceTests() =>
        _service = new InboundEmailService(_t.Db, new TriageQueue(), new FakeTimeProvider(new DateTimeOffset(Now)),
            NullLogger<InboundEmailService>.Instance);

    [Fact]
    public async Task Email_from_known_user_creates_an_email_ticket_for_their_client()
    {
        var result = await _service.ProcessAsync(new InboundEmail("User U <USER@acme.test>", "VPN not connecting", "VPN fails with error 809."));

        Assert.Equal(InboundEmailOutcome.TicketCreated, result.Outcome);
        var ticket = await _t.Db.Tickets.SingleAsync(t => t.Id == result.TicketId);
        Assert.Equal(TicketSource.Email, ticket.Source);
        Assert.Equal(_t.Client.Id, ticket.ClientId);
        Assert.Equal(_t.ClientUser.Id, ticket.CreatedById);
        Assert.Equal("VPN not connecting", ticket.Title);
        Assert.Equal(Now.AddHours(72), ticket.ResolutionDueAt); // Medium until triaged
    }

    [Fact]
    public async Task New_sender_at_a_known_client_domain_becomes_a_contact()
    {
        var result = await _service.ProcessAsync(new InboundEmail("newperson@acme.test", "Hello", "My monitor flickers."));

        Assert.Equal(InboundEmailOutcome.TicketCreated, result.Outcome);
        var contact = await _t.Db.Users.SingleAsync(u => u.Email == "newperson@acme.test");
        Assert.Equal(UserRole.ClientStaff, contact.Role);
        Assert.Equal(_t.Client.Id, contact.ClientId);
        Assert.Equal("", contact.PasswordHash); // cannot sign in
    }

    [Theory]
    [InlineData("stranger@unknown.test")] // not a client
    [InlineData("agent@desk.test")]       // agents' emails aren't tickets
    [InlineData("not an email address")]
    public async Task Unknown_or_invalid_senders_are_rejected(string from)
    {
        var result = await _service.ProcessAsync(new InboundEmail(from, "Help", "Something broke."));

        Assert.Equal(InboundEmailOutcome.Rejected, result.Outcome);
        Assert.Equal(0, await _t.Db.Tickets.CountAsync());
    }

    [Fact]
    public async Task Reply_with_ticket_reference_adds_a_comment_and_resumes_a_waiting_ticket()
    {
        var created = await _service.ProcessAsync(new InboundEmail("user@acme.test", "Printer jam", "Printer keeps jamming."));
        var ticket = await _t.Db.Tickets.SingleAsync();
        ticket.Status = TicketStatus.Waiting;
        await _t.Db.SaveChangesAsync();

        var reply = await _service.ProcessAsync(new InboundEmail("user@acme.test", $"RE: [#{created.TicketId}] Printer jam",
            "It's the HP on level 2.\n\nOn Tue, 10 Mar 2026 Agent wrote:\n> Which printer is it?"));

        Assert.Equal(InboundEmailOutcome.CommentAdded, reply.Outcome);
        Assert.Equal(1, await _t.Db.Tickets.CountAsync());
        var comment = await _t.Db.Comments.SingleAsync();
        Assert.Equal("It's the HP on level 2.", comment.Body); // quoted history stripped
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
    }

    [Fact]
    public async Task Reference_to_someone_elses_ticket_creates_a_new_ticket_instead()
    {
        var first = await _service.ProcessAsync(new InboundEmail("user@acme.test", "Printer jam", "Printer keeps jamming."));

        var other = await _service.ProcessAsync(new InboundEmail("colleague@acme.test", $"RE: [#{first.TicketId}] Printer jam", "Me too."));

        Assert.Equal(InboundEmailOutcome.TicketCreated, other.Outcome);
        Assert.NotEqual(first.TicketId, other.TicketId);
    }

    [Theory]
    [InlineData("Olivia Brown <Olivia@Northwind.test>", "olivia@northwind.test")]
    [InlineData("olivia@northwind.test", "olivia@northwind.test")]
    [InlineData("no address here", null)]
    public void Extracts_sender_address(string from, string? expected)
    {
        Assert.Equal(expected, InboundEmailService.ExtractAddress(from));
    }

    public void Dispose() => _t.Dispose();
}
