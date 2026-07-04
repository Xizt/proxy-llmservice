using LlmShadow.DataLayer.Models;
using Microsoft.EntityFrameworkCore;

namespace LlmShadow.DataLayer;

/// <summary>EF Core database context for the shadow LLM proxy system.</summary>
public sealed class ShadowDbContext : DbContext
{
    /// <inheritdoc />
    public ShadowDbContext(DbContextOptions<ShadowDbContext> options) : base(options) { }

    /// <summary>Gets the set of proxied request records.</summary>
    public DbSet<RequestRecord> Requests => Set<RequestRecord>();

    /// <summary>Gets the set of primary LLM responses.</summary>
    public DbSet<PrimaryLlmResponse> PrimaryLlmResponses => Set<PrimaryLlmResponse>();

    /// <summary>Gets the set of secondary (shadow) LLM responses.</summary>
    public DbSet<SecondaryLlmResponse> SecondaryLlmResponses => Set<SecondaryLlmResponse>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Global soft-delete filters ────────────────────────────────────────
        modelBuilder.Entity<RequestRecord>().HasQueryFilter(r => !r.IsDeleted);
        modelBuilder.Entity<PrimaryLlmResponse>().HasQueryFilter(r => !r.IsDeleted);
        modelBuilder.Entity<SecondaryLlmResponse>().HasQueryFilter(r => !r.IsDeleted);

        // ── RequestRecord ─────────────────────────────────────────────────────
        modelBuilder.Entity<RequestRecord>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.RequestId).IsUnique();
            e.HasIndex(r => new { r.Status, r.CreatedAtUtc });
            e.Property(r => r.Model).HasMaxLength(256).IsRequired();
            e.Property(r => r.CandidateModel).HasMaxLength(256).IsRequired();
            e.Property(r => r.RequestPayloadJson).IsRequired();
            e.Property(r => r.Status).HasConversion<int>();

            e.HasOne(r => r.PrimaryResponse)
                .WithOne(p => p.Request)
                .HasForeignKey<PrimaryLlmResponse>(p => p.RequestId)
                .HasPrincipalKey<RequestRecord>(r => r.RequestId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(r => r.SecondaryResponse)
                .WithOne(s => s.Request)
                .HasForeignKey<SecondaryLlmResponse>(s => s.RequestId)
                .HasPrincipalKey<RequestRecord>(r => r.RequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── PrimaryLlmResponse ────────────────────────────────────────────────
        modelBuilder.Entity<PrimaryLlmResponse>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.RequestId);
        });

        // ── SecondaryLlmResponse ──────────────────────────────────────────────
        modelBuilder.Entity<SecondaryLlmResponse>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.RequestId);
        });
    }

    /// <inheritdoc />
    public override int SaveChanges()
    {
        SetAuditFields();
        return base.SaveChanges();
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetAuditFields()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.ModifiedAtUtc = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.ModifiedAtUtc = now;
            }
        }
    }
}
