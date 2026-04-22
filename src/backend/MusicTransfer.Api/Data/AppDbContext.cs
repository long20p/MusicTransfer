using Microsoft.EntityFrameworkCore;

namespace MusicTransfer.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<OAuthLinkEntity> OAuthLinks => Set<OAuthLinkEntity>();
    public DbSet<MigrationJobEntity> MigrationJobs => Set<MigrationJobEntity>();
    public DbSet<MigrationJobPlaylistEntity> MigrationJobPlaylists => Set<MigrationJobPlaylistEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(b =>
        {
            b.ToTable("users");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.ExternalId).IsUnique();
            b.Property(x => x.ExternalId).IsRequired();
        });

        modelBuilder.Entity<OAuthLinkEntity>(b =>
        {
            b.ToTable("oauth_links");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.UserId, x.Provider }).IsUnique();
            b.Property(x => x.Provider).IsRequired();
        });

        modelBuilder.Entity<MigrationJobEntity>(b =>
        {
            b.ToTable("migration_jobs");
            b.HasKey(x => x.Id);
            b.Property(x => x.SourceProvider).IsRequired();
            b.Property(x => x.TargetProvider).IsRequired();
            b.Property(x => x.Status).IsRequired();
        });

        modelBuilder.Entity<MigrationJobPlaylistEntity>(b =>
        {
            b.ToTable("migration_job_playlists");
            b.HasKey(x => x.Id);
            b.Property(x => x.SourcePlaylistId).IsRequired();
            b.Property(x => x.Status).IsRequired();
        });
    }
}

public class UserEntity
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public class OAuthLinkEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? Scope { get; set; }
    public DateTime? TokenExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class MigrationJobEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string SourceProvider { get; set; } = string.Empty;
    public string TargetProvider { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class MigrationJobPlaylistEntity
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public string SourcePlaylistId { get; set; } = string.Empty;
    public string? TargetPlaylistId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
