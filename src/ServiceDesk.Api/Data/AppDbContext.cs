using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ServiceDesk.Api.Domain;

namespace ServiceDesk.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<TicketStatusChange> TicketStatusChanges => Set<TicketStatusChange>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every DateTime is stored and read back as UTC. SQLite and SQL Server's datetime2 don't keep
        // DateTimeKind, so without this the API would return times without a "Z" suffix and the
        // browser would wrongly treat them as local time.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(200);
            e.Property(c => c.EmailDomain).HasMaxLength(200);
            e.HasIndex(c => c.EmailDomain).IsUnique();
        });

        modelBuilder.Entity<User>(e =>
        {
            e.Property(u => u.Email).HasMaxLength(256);
            e.Property(u => u.DisplayName).HasMaxLength(200);
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(32);
            e.HasIndex(u => u.Email).IsUnique();
            e.HasOne(u => u.Client).WithMany(c => c.Users)
                .HasForeignKey(u => u.ClientId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Ticket>(e =>
        {
            e.Property(t => t.Title).HasMaxLength(200);
            e.Property(t => t.Description).HasMaxLength(8000);
            // Enums stored as readable strings ("InProgress") instead of ints, so the data survives
            // enum reordering and is easy to read when querying the database directly.
            e.Property(t => t.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(t => t.Priority).HasConversion<string>().HasMaxLength(32);
            e.Property(t => t.Category).HasConversion<string>().HasMaxLength(32);
            e.Property(t => t.Source).HasConversion<string>().HasMaxLength(32);
            e.HasOne(t => t.Client).WithMany(c => c.Tickets)
                .HasForeignKey(t => t.ClientId).OnDelete(DeleteBehavior.Restrict);
            // Restrict (not cascade) on user FKs: SQL Server rejects multiple cascade paths to one table.
            e.HasOne(t => t.CreatedBy).WithMany()
                .HasForeignKey(t => t.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.AssignedTo).WithMany()
                .HasForeignKey(t => t.AssignedToId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(t => t.Status);
            e.HasIndex(t => t.ClientId);
            e.HasIndex(t => t.ResolutionDueAt);
        });

        modelBuilder.Entity<Comment>(e =>
        {
            e.Property(c => c.Body).HasMaxLength(8000);
            e.HasOne(c => c.Ticket).WithMany(t => t.Comments)
                .HasForeignKey(c => c.TicketId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Author).WithMany()
                .HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TicketStatusChange>(e =>
        {
            e.Property(s => s.FromStatus).HasConversion<string>().HasMaxLength(32);
            e.Property(s => s.ToStatus).HasConversion<string>().HasMaxLength(32);
            e.HasOne(s => s.Ticket).WithMany(t => t.StatusChanges)
                .HasForeignKey(s => s.TicketId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.ChangedBy).WithMany()
                .HasForeignKey(s => s.ChangedById).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
