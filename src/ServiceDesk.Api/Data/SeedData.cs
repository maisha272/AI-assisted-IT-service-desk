using ServiceDesk.Api.Domain;
using ServiceDesk.Api.Services;

namespace ServiceDesk.Api.Data;

/// <summary>
/// Realistic demo data. Everything is generated relative to "now" with a fixed random seed, so the
/// live demo always looks current (fresh tickets, some near breach) and is the same on every run.
/// </summary>
public static class SeedData
{
    public const string DemoAgentEmail = "sam@servicedesk.test";
    public const string DemoClientEmail = "olivia@northwind.test";

    private record TicketTemplate(string Title, string Description, TicketCategory Category, TicketPriority Priority, string Fix);

    private static readonly TicketTemplate[] Templates =
    [
        new("Laptop won't power on", "My laptop won't turn on this morning. The charging light flashes amber three times and then nothing. I have client meetings this afternoon.", TicketCategory.Hardware, TicketPriority.High, "Performed a hard reset (held power 30s with charger disconnected). Laptop booted. Battery health reported as poor, so a replacement battery has been ordered."),
        new("Second monitor not detected", "Since moving desks my second monitor just says 'no signal'. I'm using the USB-C dock.", TicketCategory.Hardware, TicketPriority.Low, "Updated the dock firmware and display drivers, then reseated the DisplayPort cable. Both monitors now detected."),
        new("Laptop battery draining very quickly", "My battery goes from 100% to empty in about an hour, even when I'm only using email.", TicketCategory.Hardware, TicketPriority.Low, "A background sync process was stuck at high CPU. Cleared the sync cache and replaced the battery under warranty."),
        new("Excel crashes when opening large spreadsheets", "Excel freezes and closes whenever I open the monthly reporting workbook (about 40MB). Other files are fine.", TicketCategory.Software, TicketPriority.Medium, "Disabled a faulty COM add-in and switched the user to 64-bit Office. Workbook now opens in a few seconds."),
        new("Request to install Adobe Acrobat Pro", "I need Acrobat Pro to edit and sign PDFs for contracts. My manager has approved it.", TicketCategory.Software, TicketPriority.Low, "Assigned an Acrobat Pro licence and deployed the app through Intune. Confirmed it activates."),
        new("Teams keeps freezing during video calls", "Teams freezes for 10-20 seconds during video calls, then catches up. It happens several times per meeting.", TicketCategory.Software, TicketPriority.Medium, "Cleared the Teams cache, disabled GPU hardware acceleration and updated the graphics driver. Calls are now stable."),
        new("Accounting software won't sync bank feed", "Our bank feed hasn't updated since Monday. Reconciliations are behind and month end is this week.", TicketCategory.Software, TicketPriority.High, "Bank connection token had expired. Re-authorised the bank feed connection and triggered a manual sync."),
        new("CAD licence error on startup", "CAD software says 'no licences available' when I open it, but I was using it yesterday.", TicketCategory.Software, TicketPriority.High, "The licence server service had stopped after a Windows update. Restarted it and set the service to start automatically."),
        new("Office internet is down", "No one in the office can access the internet. The router lights are red. Phones are also down.", TicketCategory.Network, TicketPriority.Critical, "ISP outage confirmed on their status page. Failed over to the 4G backup link, then switched back when the ISP restored service."),
        new("VPN disconnects every few minutes", "When working from home my VPN drops every 5 minutes and I lose access to the file server.", TicketCategory.Network, TicketPriority.High, "Home router was dropping idle UDP sessions. Switched the VPN client profile to TCP and enabled keep-alive."),
        new("Wi-Fi very slow in the meeting room", "Wi-Fi in the large meeting room is very slow; video calls keep buffering.", TicketCategory.Network, TicketPriority.Medium, "Access point in the meeting room was on a congested channel. Changed channel and increased its transmit power."),
        new("Can't access shared drive", "I can't open the S: drive. Windows says the network path was not found.", TicketCategory.Network, TicketPriority.High, "Mapped drive pointed at the old file server name. Updated the drive mapping group policy and ran gpupdate."),
        new("Locked out of my account", "I entered my password wrong too many times and now I'm locked out of my computer and email.", TicketCategory.AccountAccess, TicketPriority.High, "Verified identity by phone call-back, unlocked the account and reset the password. Reminded the user about self-service reset."),
        new("New starter account setup", "We have a new receptionist starting Monday. Please set up their email, computer login and access to the shared calendar.", TicketCategory.AccountAccess, TicketPriority.Medium, "Created the user from the new-starter template, assigned a Microsoft 365 licence, added to Reception groups and prepared a laptop."),
        new("MFA prompt not arriving on new phone", "I got a new phone and the authenticator app isn't getting sign-in prompts, so I can't log in.", TicketCategory.AccountAccess, TicketPriority.High, "Verified identity, cleared the old MFA registration and had the user re-register the authenticator app on the new phone."),
        new("Need access to Finance SharePoint site", "I need read access to the Finance SharePoint site for the audit. My manager approved.", TicketCategory.AccountAccess, TicketPriority.Low, "Added the user to the Finance-Readers group after confirming manager approval in writing."),
        new("Offboard departing employee", "A staff member finishes on Friday. Please disable their access and forward their email to their manager.", TicketCategory.AccountAccess, TicketPriority.Medium, "Disabled sign-in, converted the mailbox to shared with the manager's access, revoked sessions and collected the laptop."),
        new("Not receiving external emails", "I haven't received any emails from outside the company since this morning. Internal email works.", TicketCategory.Email, TicketPriority.High, "An over-aggressive transport rule was quarantining external mail. Corrected the rule and released quarantined messages."),
        new("Outlook keeps asking for my password", "Outlook pops up a password prompt every few minutes even though I type the right password.", TicketCategory.Email, TicketPriority.Medium, "Removed stale cached credentials from Windows Credential Manager and rebuilt the Outlook profile."),
        new("Shared mailbox not showing in Outlook", "I was given access to the accounts@ shared mailbox but it doesn't appear in Outlook.", TicketCategory.Email, TicketPriority.Low, "Permissions had been granted without automapping. Re-applied full access with automapping; mailbox appeared after restarting Outlook."),
        new("Mailbox full, emails bouncing", "People say their emails to me are bouncing back with 'mailbox full'.", TicketCategory.Email, TicketPriority.Medium, "Enabled the online archive and applied a retention policy to move mail older than 2 years to archive."),
        new("Printer showing offline", "The level 2 printer shows as offline for everyone. We can't print invoices.", TicketCategory.Printing, TicketPriority.Medium, "Printer had picked up a new IP address from DHCP. Set a DHCP reservation and updated the print queue port."),
        new("Scan to email not working", "Scanning to email from the copier fails with an SMTP error.", TicketCategory.Printing, TicketPriority.Medium, "The copier used basic SMTP auth, which was disabled. Configured an SMTP relay connector for the copier's IP instead."),
        new("Printer jamming constantly", "The reception printer jams every few pages.", TicketCategory.Printing, TicketPriority.Low, "Replaced the worn pickup rollers and advised staff to use the correct paper weight."),
        new("Suspicious email asking for password reset", "I got an email saying my password expires today with a link to 'keep the same password'. It looks odd.", TicketCategory.Security, TicketPriority.High, "Confirmed phishing. Purged the message from all mailboxes, blocked the sender domain and reminded staff not to click."),
        new("Clicked a link in a phishing email", "I clicked a link in an email and entered my password before realising it was fake.", TicketCategory.Security, TicketPriority.Critical, "Reset the password, revoked all sessions, checked sign-in logs (no suspicious access) and removed a malicious inbox rule."),
        new("Antivirus warning on my laptop", "Defender popped up saying it found a threat and quarantined a file from my Downloads folder.", TicketCategory.Security, TicketPriority.High, "Reviewed the alert: a bundled adware installer. Ran a full scan (clean) and confirmed the quarantined file was removed."),
        new("Files renamed with strange extension", "Some files on the shared drive now have a weird extension and won't open. There's a text file asking for payment.", TicketCategory.Security, TicketPriority.Critical, "Isolated the infected workstation, disabled the user's sessions, restored affected folders from last night's backup and reset credentials."),
        new("Request new mobile phone", "Our new sales manager needs a company mobile phone set up with email.", TicketCategory.Other, TicketPriority.Low, "Ordered the device, enrolled it in Intune and configured Outlook and Teams."),
        new("Question about IT onboarding checklist", "Do you have a checklist for what IT needs to know before a new person starts?", TicketCategory.Other, TicketPriority.Low, "Sent the new-starter request form and checklist, and explained the 3 business day lead time."),
    ];

    private static readonly string[] FirstResponses =
    [
        "Thanks for letting us know. I'm looking into this now.",
        "Hi, I've picked this up and will connect remotely shortly.",
        "Thanks for the details. I'm investigating and will update you soon.",
    ];

    private static readonly string[] ClarifyingQuestions =
    [
        "Could you confirm whether this happens on other devices as well?",
        "Can you send a screenshot of the error message?",
        "When did this last work correctly?",
    ];

    private static readonly string[] ClientReplies =
    [
        "Yes, my colleague has the same problem.",
        "Screenshot attached. It started yesterday afternoon.",
        "It was fine on Friday.",
    ];

    // Rough resolution targets used to make the history realistic (most tickets resolved in time,
    // some late). These are the same numbers as the SLA policy introduced later.
    private static readonly Dictionary<TicketPriority, double> ResolutionHours = new()
    {
        [TicketPriority.Critical] = 4,
        [TicketPriority.High] = 24,
        [TicketPriority.Medium] = 72,
        [TicketPriority.Low] = 120,
    };

    public static void Populate(AppDbContext db, DateTime now)
    {
        var rng = new Random(42);

        var agents = new List<User>
        {
            new() { Email = DemoAgentEmail, DisplayName = "Sam Taylor", Role = UserRole.Agent },
            new() { Email = "priya@servicedesk.test", DisplayName = "Priya Nair", Role = UserRole.Agent },
            new() { Email = "jordan@servicedesk.test", DisplayName = "Jordan Lee", Role = UserRole.Agent },
        };

        var clients = new List<Client>
        {
            NewClient("Northwind Dental Group", "northwind.test", ("Olivia Brown", "olivia"), ("Liam Wilson", "liam")),
            NewClient("Harbour Logistics", "harbourlogistics.test", ("Mia Thompson", "mia"), ("Noah Martin", "noah")),
            NewClient("Brightside Accounting", "brightside.test", ("Ava Walker", "ava"), ("Ethan Harris", "ethan")),
            NewClient("Kestrel Architects", "kestrel.test", ("Chloe King", "chloe"), ("Lucas Wright", "lucas")),
        };
        foreach (var c in clients) c.CreatedAt = now.AddDays(-365);

        db.Users.AddRange(agents);
        db.Clients.AddRange(clients);

        const int recentOpenTickets = 14;
        const int historicalTickets = 70;

        for (var i = 0; i < recentOpenTickets + historicalTickets; i++)
        {
            var template = Templates[rng.Next(Templates.Length)];
            var client = clients[rng.Next(clients.Count)];
            // The demo client user (Olivia) gets a decent share of tickets so her view isn't empty.
            var creator = i % 5 == 0 ? clients[0].Users[0] : client.Users[rng.Next(client.Users.Count)];
            if (creator.Client != client) client = creator.Client!;

            var isRecent = i < recentOpenTickets;
            var createdAt = isRecent
                ? now.AddHours(-rng.NextDouble() * 60)
                : now.AddDays(-(3 + rng.NextDouble() * 87));

            var ticket = new Ticket
            {
                Title = template.Title,
                Description = template.Description,
                Category = template.Category,
                Priority = template.Priority,
                Source = rng.NextDouble() < 0.25 ? TicketSource.Email : TicketSource.Portal,
                Client = client,
                CreatedBy = creator,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
            };
            client.Tickets.Add(ticket);

            if (isRecent)
                BuildOpenHistory(ticket, agents, rng, now);
            else
                BuildClosedHistory(ticket, template, agents, rng, now);
        }
    }

    private static Client NewClient(string name, string domain, params (string Name, string Local)[] staff)
    {
        var client = new Client { Name = name, EmailDomain = domain };
        foreach (var (displayName, local) in staff)
            client.Users.Add(new User
            {
                Email = $"{local}@{domain}",
                DisplayName = displayName,
                Role = UserRole.ClientStaff,
                Client = client,
            });
        return client;
    }

    /// <summary>Recent tickets: a mix of New, In progress and Waiting, some getting close to their deadline.</summary>
    private static void BuildOpenHistory(Ticket ticket, List<User> agents, Random rng, DateTime now)
    {
        var age = now - ticket.CreatedAt;
        var roll = rng.NextDouble();
        if (roll < 0.35 && age.TotalHours < 30) return; // still New

        var agent = agents[rng.Next(agents.Count)];
        ticket.AssignedTo = agent;
        var t = ticket.CreatedAt.AddMinutes(10 + rng.Next(90));
        if (t > now) t = now.AddMinutes(-1);
        AddComment(ticket, agent, FirstResponses[rng.Next(FirstResponses.Length)], t);
        TicketWorkflow.Transition(ticket, TicketStatus.InProgress, agent, t);

        if (roll > 0.7)
        {
            var t2 = t.AddMinutes(20 + rng.Next(60));
            if (t2 < now)
            {
                AddComment(ticket, agent, ClarifyingQuestions[rng.Next(ClarifyingQuestions.Length)], t2);
                TicketWorkflow.Transition(ticket, TicketStatus.Waiting, agent, t2);
            }
        }
    }

    /// <summary>Older tickets: worked, resolved (mostly within target) and usually closed.</summary>
    private static void BuildClosedHistory(Ticket ticket, TicketTemplate template, List<User> agents, Random rng, DateTime now)
    {
        var agent = agents[rng.Next(agents.Count)];
        ticket.AssignedTo = agent;

        var target = ResolutionHours[ticket.Priority];
        // ~85% resolved within target, ~15% late (so SLA reports have something to show).
        var factor = rng.NextDouble() < 0.85 ? 0.15 + rng.NextDouble() * 0.8 : 1.05 + rng.NextDouble() * 0.8;
        var resolvedAt = ticket.CreatedAt.AddHours(target * factor);

        var firstResponse = ticket.CreatedAt.AddMinutes(5 + rng.Next(Math.Max(10, (int)(target * 60 * 0.15))));
        if (firstResponse >= resolvedAt) firstResponse = ticket.CreatedAt.AddMinutes(5);

        AddComment(ticket, agent, FirstResponses[rng.Next(FirstResponses.Length)], firstResponse);
        TicketWorkflow.Transition(ticket, TicketStatus.InProgress, agent, firstResponse);

        if (rng.NextDouble() < 0.3)
        {
            var span = resolvedAt - firstResponse;
            var asked = firstResponse + span * 0.3;
            var replied = firstResponse + span * 0.5;
            AddComment(ticket, agent, ClarifyingQuestions[rng.Next(ClarifyingQuestions.Length)], asked);
            TicketWorkflow.Transition(ticket, TicketStatus.Waiting, agent, asked);
            AddComment(ticket, ticket.CreatedBy, ClientReplies[rng.Next(ClientReplies.Length)], replied);
            TicketWorkflow.Transition(ticket, TicketStatus.InProgress, ticket.CreatedBy, replied);
        }

        if (rng.NextDouble() < 0.25)
            AddComment(ticket, agent, "Internal: checked the change log, nothing scheduled for this client in the last 24h.",
                resolvedAt.AddMinutes(-15), isInternal: true);

        AddComment(ticket, agent, template.Fix, resolvedAt);
        TicketWorkflow.Transition(ticket, TicketStatus.Resolved, agent, resolvedAt);

        var closedAt = resolvedAt.AddDays(1 + rng.NextDouble() * 2);
        if (closedAt < now)
            TicketWorkflow.Transition(ticket, TicketStatus.Closed, agent, closedAt);
    }

    private static void AddComment(Ticket ticket, User author, string body, DateTime at, bool isInternal = false) =>
        ticket.Comments.Add(new Comment { Author = author, Body = body, CreatedAt = at, IsInternal = isInternal });
}
