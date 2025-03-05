using Microsoft.EntityFrameworkCore;
using OpenShock.Common.Constants;
using OpenShock.Common.Extensions;
using OpenShock.Common.Models;

namespace OpenShock.Common.OpenShockDb;

/// <summary>
/// This is meant for use in migrations only.
/// </summary>
public class MigrationOpenShockContext : OpenShockContext
{
    private readonly string? _connectionString = null;
    private readonly bool _debug;
    private readonly bool _migrationTool;
    private readonly ILoggerFactory? _loggerFactory = null;
    
    public MigrationOpenShockContext()
    {
        _migrationTool = true;
    }
    
    public MigrationOpenShockContext(string connectionString, bool debug, ILoggerFactory loggerFactory)
    {
        _connectionString = connectionString;
        _debug = debug;
        _loggerFactory = loggerFactory;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (_migrationTool)
        {
            ConfigureOptionsBuilder(optionsBuilder, "Host=localhost;Database=openshock;Username=openshock;Password=openshock", true);
            return;
        }
        if(string.IsNullOrWhiteSpace(_connectionString))
            throw new InvalidOperationException("Connection string is not set.");
        ConfigureOptionsBuilder(optionsBuilder, _connectionString, _debug);
        
        if (_loggerFactory != null)
            optionsBuilder.UseLoggerFactory(_loggerFactory);
    }
}

/// <summary>
/// Main OpenShock DB Context
/// </summary>
public partial class OpenShockContext : DbContext
{
    public OpenShockContext()
    {
    }

    public OpenShockContext(DbContextOptions<OpenShockContext> options)
        : base(options)
    {
    }
    
    public static void ConfigureOptionsBuilder(DbContextOptionsBuilder optionsBuilder, string connectionString,
        bool debug)
    {
        optionsBuilder.UseNpgsql(connectionString, npgsqlBuilder =>
        {
            npgsqlBuilder.MapEnum<RoleType>();
            npgsqlBuilder.MapEnum<ControlType>();
            npgsqlBuilder.MapEnum<PermissionType>();
            npgsqlBuilder.MapEnum<ShockerModelType>();
            npgsqlBuilder.MapEnum<OtaUpdateStatus>();
        });

        if (debug)
        {
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
        }
    }

    public virtual DbSet<ApiToken> ApiTokens { get; set; }

    public virtual DbSet<ApiTokenReport> ApiTokensReports { get; set; }

    public virtual DbSet<Device> Devices { get; set; }

    public virtual DbSet<DeviceOtaUpdate> DeviceOtaUpdates { get; set; }

    public virtual DbSet<PasswordReset> PasswordResets { get; set; }

    public virtual DbSet<ShareRequest> ShareRequests { get; set; }

    public virtual DbSet<ShareRequestsShocker> ShareRequestsShockers { get; set; }

    public virtual DbSet<Shocker> Shockers { get; set; }

    public virtual DbSet<ShockerControlLog> ShockerControlLogs { get; set; }

    public virtual DbSet<ShockerShare> ShockerShares { get; set; }

    public virtual DbSet<ShockerShareCode> ShockerShareCodes { get; set; }

    public virtual DbSet<ShockerSharesLink> ShockerSharesLinks { get; set; }

    public virtual DbSet<ShockerSharesLinksShocker> ShockerSharesLinksShockers { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UsersActivation> UsersActivations { get; set; }

    public virtual DbSet<UsersEmailChange> UsersEmailChanges { get; set; }

    public virtual DbSet<UsersNameChange> UsersNameChanges { get; set; }

    public virtual DbSet<AdminUsersView> AdminUsersViews { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseNpgsql("Host=localhost;Database=openshock;Username=openshock;Password=openshock");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum("control_type", new[] { "sound", "vibrate", "shock", "stop" })
            .HasPostgresEnum("ota_update_status", new[] { "started", "running", "finished", "error", "timeout" })
            .HasPostgresEnum("password_encryption_type", new[] { "pbkdf2", "bcrypt_enhanced" })
            .HasPostgresEnum("permission_type",
                new[] { "shockers.use", "shockers.edit", "shockers.pause", "devices.edit", "devices.auth" })
            .HasPostgresEnum("role_type", new[] { "support", "staff", "admin", "system" })
            .HasPostgresEnum("shocker_model_type", new[] { "caiXianlin", "petTrainer", "petrainer998DR" })
            .HasAnnotation("Npgsql:CollationDefinition:public.ndcoll", "und-u-ks-level2,und-u-ks-level2,icu,False");

        modelBuilder.Entity<ApiToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("api_tokens_pkey");

            entity.ToTable("api_tokens");

            entity.HasIndex(e => e.UserId).HasAnnotation("Npgsql:StorageParameter:deduplicate_items", "true");
            entity.HasIndex(e => e.ValidUntil).HasAnnotation("Npgsql:StorageParameter:deduplicate_items", "true");
            entity.HasIndex(e => e.TokenHash).IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedByIp)
                .HasColumnName("created_by_ip");
            entity.Property(e => e.CreatedOn)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_on");
            entity.Property(e => e.LastUsed)
                .HasDefaultValueSql("'-infinity'::timestamp without time zone")
                .HasColumnName("last_used");
            entity.Property(e => e.Name)
                .HasMaxLength(HardLimits.ApiKeyNameMaxLength)
                .HasColumnName("name");
            entity.Property(e => e.TokenHash)
                .HasMaxLength(HardLimits.Sha256HashHexLength)
                .HasColumnName("token_hash");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.ValidUntil).HasColumnName("valid_until");

            entity.Property(e => e.Permissions).HasColumnType("permission_type[]").HasColumnName("permissions");

            entity.HasOne(d => d.User).WithMany(p => p.ApiTokens)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_user_id");
        });

        modelBuilder.Entity<ApiTokenReport>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("api_token_reports_pkey");

            entity.ToTable("api_token_reports");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.ReportedByUserId)
                .HasColumnName("reported_by_user_id");
            entity.Property(e => e.ReportedByIp)
                .HasColumnName("reported_by_ip");
            entity.Property(e => e.ReportedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("reported_at");
        });

        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("devices_pkey");

            entity.ToTable("devices");

            entity.HasIndex(e => e.Owner).HasAnnotation("Npgsql:StorageParameter:deduplicate_items", "true");
            entity.HasIndex(e => e.Token).IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedOn)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_on");
            entity.Property(e => e.Name)
                .VarCharWithLength(HardLimits.HubNameMaxLength)
                .HasColumnName("name");
            entity.Property(e => e.Owner).HasColumnName("owner");
            entity.Property(e => e.Token)
                .HasMaxLength(HardLimits.HubTokenMaxLength)
                .HasColumnName("token");

            entity.HasOne(d => d.OwnerNavigation).WithMany(p => p.Devices)
                .HasForeignKey(d => d.Owner)
                .HasConstraintName("owner_user_id");
        });

        modelBuilder.Entity<DeviceOtaUpdate>(entity =>
        {
            entity.HasKey(e => new { e.Device, e.UpdateId }).HasName("device_ota_updates_pkey");

            entity.ToTable("device_ota_updates");

            entity.HasIndex(e => e.CreatedOn, "device_ota_updates_created_on_idx")
                .HasAnnotation("Npgsql:StorageParameter:deduplicate_items", "true");

            entity.Property(e => e.Device).HasColumnName("device");
            entity.Property(e => e.UpdateId).HasColumnName("update_id");
            entity.Property(e => e.CreatedOn)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_on");
            entity.Property(e => e.Message)
                .VarCharWithLength(HardLimits.OtaUpdateMessageMaxLength)
                .HasColumnName("message");
            entity.Property(e => e.Version)
                .VarCharWithLength(HardLimits.SemVerMaxLength)
                .HasColumnName("version");

            entity.Property(e => e.Status).HasColumnType("ota_update_status").HasColumnName("status");


            entity.HasOne(d => d.DeviceNavigation).WithMany(p => p.DeviceOtaUpdates)
                .HasForeignKey(d => d.Device)
                .HasConstraintName("device_ota_updates_device");
        });

        modelBuilder.Entity<PasswordReset>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("password_resets_pkey");

            entity.ToTable("password_resets");

            entity.HasIndex(e => e.UserId).HasAnnotation("Npgsql:StorageParameter:deduplicate_items", "true");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedOn)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_on");
            entity.Property(e => e.Secret)
                .VarCharWithLength(HardLimits.PasswordResetSecretMaxLength)
                .HasColumnName("secret");
            entity.Property(e => e.UsedOn)
                .HasColumnName("used_on");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.PasswordResets)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("user_id");
        });

        modelBuilder.Entity<ShareRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("shares_codes_pkey");

            entity.ToTable("share_requests");

            entity.HasIndex(e => e.Owner).HasAnnotation("Npgsql:StorageParameter:deduplicate_items", "true");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedOn)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_on");
            entity.Property(e => e.Owner).HasColumnName("owner");
            entity.Property(e => e.User).HasColumnName("user");

            entity.HasOne(d => d.OwnerNavigation).WithMany(p => p.ShareRequestOwnerNavigations)
                .HasForeignKey(d => d.Owner)
                .HasConstraintName("fk_share_requests_owner");

            entity.HasOne(d => d.UserNavigation).WithMany(p => p.ShareRequestUserNavigations)
                .HasForeignKey(d => d.User)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_share_requests_user");
        });

        modelBuilder.Entity<ShareRequestsShocker>(entity =>
        {
            entity.HasKey(e => new { e.ShareRequest, e.Shocker }).HasName("share_requests_shockers_pkey");

            entity.ToTable("share_requests_shockers");

            entity.Property(e => e.ShareRequest).HasColumnName("share_request");
            entity.Property(e => e.Shocker).HasColumnName("shocker");
            entity.Property(e => e.LimitDuration).HasColumnName("limit_duration");
            entity.Property(e => e.LimitIntensity).HasColumnName("limit_intensity");
            entity.Property(e => e.PermLive)
                .HasDefaultValue(true)
                .HasColumnName("perm_live");
            entity.Property(e => e.PermShock)
                .HasDefaultValue(true)
                .HasColumnName("perm_shock");
            entity.Property(e => e.PermSound)
                .HasDefaultValue(true)
                .HasColumnName("perm_sound");
            entity.Property(e => e.PermVibrate)
                .HasDefaultValue(true)
                .HasColumnName("perm_vibrate");

            entity.HasOne(d => d.ShareRequestNavigation).WithMany(p => p.ShareRequestsShockers)
                .HasForeignKey(d => d.ShareRequest)
                .HasConstraintName("fk_share_requests_shockers_share_request");

            entity.HasOne(d => d.ShockerNavigation).WithMany(p => p.ShareRequestsShockers)
                .HasForeignKey(d => d.Shocker)
                .HasConstraintName("fk_share_requests_shockers_shocker");
        });

        modelBuilder.Entity<Shocker>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("shockers_pkey");

            entity.ToTable("shockers");

            entity.HasIndex(e => e.Device).HasAnnotation("Npgsql:StorageParameter:deduplicate_items", "true");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedOn)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_on");
            entity.Property(e => e.Device).HasColumnName("device");
            entity.Property(e => e.Name)
                .HasMaxLength(HardLimits.ShockerNameMaxLength)
                .HasColumnName("name");
            entity.Property(e => e.Paused)
                .HasDefaultValue(false)
                .HasColumnName("paused");
            entity.Property(e => e.Model).HasColumnType("shocker_model_type").HasColumnName("model");
            entity.Property(e => e.RfId).HasColumnName("rf_id");

            entity.HasOne(d => d.DeviceNavigation).WithMany(p => p.Shockers)
                .HasForeignKey(d => d.Device)
                .HasConstraintName("device_id");
        });

        modelBuilder.Entity<ShockerControlLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("shocker_control_logs_pkey");

            entity.ToTable("shocker_control_logs");

            entity.HasIndex(e => e.ShockerId).HasAnnotation("Npgsql:StorageParameter:deduplicate_items", "true");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.ControlledBy).HasColumnName("controlled_by");
            entity.Property(e => e.CreatedOn)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_on");
            entity.Property(e => e.CustomName)
                .VarCharWithLength(HardLimits.ShockerControlLogCustomNameMaxLength)
                .HasColumnName("custom_name");
            entity.Property(e => e.Duration).HasColumnName("duration");
            entity.Property(e => e.Intensity).HasColumnName("intensity");
            entity.Property(e => e.LiveControl)
                .HasDefaultValue(false)
                .HasColumnName("live_control");
            entity.Property(e => e.ShockerId).HasColumnName("shocker_id");
            entity.Property(e => e.Type).HasColumnType("control_type").HasColumnName("type");

            entity.HasOne(d => d.ControlledByNavigation).WithMany(p => p.ShockerControlLogs)
                .HasForeignKey(d => d.ControlledBy)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_controlled_by");

            entity.HasOne(d => d.Shocker).WithMany(p => p.ShockerControlLogs)
                .HasForeignKey(d => d.ShockerId)
                .HasConstraintName("fk_shocker_id");
        });

        modelBuilder.Entity<ShockerShare>(entity =>
        {
            entity.HasKey(e => new { e.ShockerId, e.SharedWith }).HasName("shocker_shares_pkey");

            entity.ToTable("shocker_shares");

            entity.HasIndex(e => e.SharedWith).HasAnnotation("Npgsql:StorageParameter:deduplicate_items", "true");

            entity.Property(e => e.ShockerId).HasColumnName("shocker_id");
            entity.Property(e => e.SharedWith).HasColumnName("shared_with");
            entity.Property(e => e.CreatedOn)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_on");
            entity.Property(e => e.LimitDuration).HasColumnName("limit_duration");
            entity.Property(e => e.LimitIntensity).HasColumnName("limit_intensity");
            entity.Property(e => e.Paused)
                .HasDefaultValue(false)
                .HasColumnName("paused");
            entity.Property(e => e.PermLive)
                .HasDefaultValue(true)
                .HasColumnName("perm_live");
            entity.Property(e => e.PermShock)
                .HasDefaultValue(true)
                .HasColumnName("perm_shock");
            entity.Property(e => e.PermSound)
                .HasDefaultValue(true)
                .HasColumnName("perm_sound");
            entity.Property(e => e.PermVibrate)
                .HasDefaultValue(true)
                .HasColumnName("perm_vibrate");

            entity.HasOne(d => d.SharedWithNavigation).WithMany(p => p.ShockerShares)
                .HasForeignKey(d => d.SharedWith)
                .HasConstraintName("shared_with_user_id");

            entity.HasOne(d => d.Shocker).WithMany(p => p.ShockerShares)
                .HasForeignKey(d => d.ShockerId)
                .HasConstraintName("ref_shocker_id");
        });

        modelBuilder.Entity<ShockerShareCode>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("shocker_share_codes_pkey");

            entity.ToTable("shocker_share_codes");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedOn)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_on");
            entity.Property(e => e.LimitDuration).HasColumnName("limit_duration");
            entity.Property(e => e.LimitIntensity).HasColumnName("limit_intensity");
            entity.Property(e => e.PermShock)
                .HasDefaultValue(true)
                .HasColumnName("perm_shock");
            entity.Property(e => e.PermSound)
                .HasDefaultValue(true)
                .HasColumnName("perm_sound");
            entity.Property(e => e.PermVibrate)
                .HasDefaultValue(true)
                .HasColumnName("perm_vibrate");
            entity.Property(e => e.ShockerId).HasColumnName("shocker_id");

            entity.HasOne(d => d.Shocker).WithMany(p => p.ShockerShareCodes)
                .HasForeignKey(d => d.ShockerId)
                .HasConstraintName("fk_shocker_id");
        });

        modelBuilder.Entity<ShockerSharesLink>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("shocker_shares_links_pkey");

            entity.ToTable("shocker_shares_links");

            entity.HasIndex(e => e.OwnerId).HasAnnotation("Npgsql:StorageParameter:deduplicate_items", "true");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedOn)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_on");
            entity.Property(e => e.ExpiresOn).HasColumnName("expires_on");
            entity.Property(e => e.Name)
                .VarCharWithLength(HardLimits.ShockerShareLinkNameMaxLength)
                .HasColumnName("name");
            entity.Property(e => e.OwnerId).HasColumnName("owner_id");

            entity.HasOne(d => d.Owner).WithMany(p => p.ShockerSharesLinks)
                .HasForeignKey(d => d.OwnerId)
                .HasConstraintName("owner_id");
        });

        modelBuilder.Entity<ShockerSharesLinksShocker>(entity =>
        {
            entity.HasKey(e => new { e.ShareLinkId, e.ShockerId }).HasName("shocker_shares_links_shockers_pkey");

            entity.ToTable("shocker_shares_links_shockers");

            entity.Property(e => e.ShareLinkId).HasColumnName("share_link_id");
            entity.Property(e => e.ShockerId).HasColumnName("shocker_id");
            entity.Property(e => e.Cooldown).HasColumnName("cooldown");
            entity.Property(e => e.LimitDuration).HasColumnName("limit_duration");
            entity.Property(e => e.LimitIntensity).HasColumnName("limit_intensity");
            entity.Property(e => e.Paused)
                .HasDefaultValue(false)
                .HasColumnName("paused");
            entity.Property(e => e.PermLive)
                .HasDefaultValue(true)
                .HasColumnName("perm_live");
            entity.Property(e => e.PermShock).HasColumnName("perm_shock");
            entity.Property(e => e.PermSound).HasColumnName("perm_sound");
            entity.Property(e => e.PermVibrate).HasColumnName("perm_vibrate");

            entity.HasOne(d => d.ShareLink).WithMany(p => p.ShockerSharesLinksShockers)
                .HasForeignKey(d => d.ShareLinkId)
                .HasConstraintName("share_link_id");

            entity.HasOne(d => d.Shocker).WithMany(p => p.ShockerSharesLinksShockers)
                .HasForeignKey(d => d.ShockerId)
                .HasConstraintName("shocker_id");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Name).UseCollation(new[] { "ndcoll" }).IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .VarCharWithLength(HardLimits.EmailAddressMaxLength)
                .HasColumnName("email");
            entity.Property(e => e.EmailActivated)
                .HasDefaultValue(false)
                .HasColumnName("email_activated");
            entity.Property(e => e.Name)
                .UseCollation("ndcoll")
                .VarCharWithLength(HardLimits.UsernameMaxLength)
                .HasColumnName("name");
            entity.Property(e => e.PasswordHash)
                .VarCharWithLength(HardLimits.PasswordHashMaxLength)
                .HasColumnName("password_hash");
            entity.Property(e => e.Roles)
                .HasColumnType("role_type[]")
                .HasColumnName("roles");
        });

        modelBuilder.Entity<UsersActivation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_activation_pkey");

            entity.ToTable("users_activation");

            entity.HasIndex(e => e.UserId);

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedOn)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_on");
            entity.Property(e => e.Secret)
                .VarCharWithLength(HardLimits.UserActivationSecretMaxLength)
                .HasColumnName("secret");
            entity.Property(e => e.UsedOn).HasColumnName("used_on");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.UsersActivations)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("user_id");
        });

        modelBuilder.Entity<UsersEmailChange>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_email_change_pkey");

            entity.ToTable("users_email_changes");

            entity.HasIndex(e => e.UserId);

            entity.HasIndex(e => e.CreatedOn).HasAnnotation("Npgsql:StorageParameter:deduplicate_items", "true");

            entity.HasIndex(e => e.UsedOn).HasAnnotation("Npgsql:StorageParameter:deduplicate_items", "true");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CreatedOn)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_on");
            entity.Property(e => e.Email)
                .VarCharWithLength(HardLimits.EmailAddressMaxLength)
                .HasColumnName("email");
            entity.Property(e => e.Secret)
                .VarCharWithLength(HardLimits.UserEmailChangeSecretMaxLength)
                .HasColumnName("secret");
            entity.Property(e => e.UsedOn).HasColumnName("used_on");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.UsersEmailChanges)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_user_id");
        });

        modelBuilder.Entity<UsersNameChange>(entity =>
        {
            entity.HasKey(e => new { e.Id, e.UserId }).HasName("users_name_changes_pkey");

            entity.ToTable("users_name_changes");

            entity.HasIndex(e => e.CreatedOn).HasAnnotation("Npgsql:StorageParameter:deduplicate_items", "true");

            entity.HasIndex(e => e.OldName).HasAnnotation("Npgsql:StorageParameter:deduplicate_items", "true");

            entity.HasIndex(e => e.UserId).HasAnnotation("Npgsql:StorageParameter:deduplicate_items", "true");

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .UseIdentityAlwaysColumn()
                .HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.CreatedOn)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_on");
            entity.Property(e => e.OldName)
                .VarCharWithLength(HardLimits.UsernameMaxLength)
                .HasColumnName("old_name");

            entity.HasOne(d => d.User).WithMany(p => p.UsersNameChanges)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_user_id");
        });

        modelBuilder.Entity<AdminUsersView>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("admin_users_view");

            entity.Property(e => e.Id)
                .HasColumnName("id");
            entity.Property(e => e.Name)
                .HasColumnType("character varying")
                .HasColumnName("name");
            entity.Property(e => e.Email)
                .HasColumnType("character varying")
                .HasColumnName("email");
            entity.Property(e => e.PasswordHashType)
                .HasColumnType("character varying")
                .HasColumnName("password_hash_type");
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at");
            entity.Property(e => e.EmailActivated)
                .HasColumnName("email_activated");
            entity.Property(e => e.Roles)
                .HasColumnType("role_type[]")
                .HasColumnName("roles")
                .HasConversion(x => x.ToArray(), x => x.ToList());
            entity.Property(e => e.ApiTokenCount)
                .HasColumnName("api_token_count");
            entity.Property(e => e.PasswordResetCount)
                .HasColumnName("password_reset_count");
            entity.Property(e => e.ShockerShareCount)
                .HasColumnName("shocker_share_count");
            entity.Property(e => e.ShockerShareLinkCount)
                .HasColumnName("shocker_share_link_count");
            entity.Property(e => e.EmailChangeRequestCount)
                .HasColumnName("email_change_request_count");
            entity.Property(e => e.NameChangeRequestCount)
                .HasColumnName("name_change_request_count");
            entity.Property(e => e.UserActivationCount)
                .HasColumnName("user_activation_count");
            entity.Property(e => e.DeviceCount)
                .HasColumnName("device_count");
            entity.Property(e => e.ShockerCount)
                .HasColumnName("shocker_count");
            entity.Property(e => e.ShockerControlLogCount)
                .HasColumnName("shocker_control_log_count");
        });
    }
}
