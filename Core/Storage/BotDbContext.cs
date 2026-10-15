using DiscordBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace DiscordBot.Core.Storage;

/// <summary>
///     The tables shared by both providers. Each provider has its own subclass, so each gets its
///     own migrations; table and column names are the ones the hand-written SQLite schema used.
/// </summary>
public abstract class BotDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<UserRow> Users => Set<UserRow>();

    public DbSet<CardRow> Cards => Set<CardRow>();

    public DbSet<ExpeditionRow> Expeditions => Set<ExpeditionRow>();

    public DbSet<SettingRow> Settings => Set<SettingRow>();

    public DbSet<LockedSetRow> LockedSets => Set<LockedSetRow>();

    public DbSet<ExpeditionLocation> ExpeditionLocations => Set<ExpeditionLocation>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // The old schema has no index on users.favorite_card_id; cards.user_id gets its index by name below
        configurationBuilder.Conventions.Remove<ForeignKeyIndexConvention>();

        // Discord IDs are below 2^63; Postgres has no unsigned 64-bit type
        configurationBuilder.Properties<ulong>().HaveConversion<long>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserRow>(user =>
        {
            user.ToTable("users");
            user.HasKey(u => u.UserId);
            user.Property(u => u.UserId).HasColumnName("user_id").ValueGeneratedNever();
            user.Property(u => u.BalanceCents).HasColumnName("balance_cents").HasDefaultValue(0L);
            user.Property(u => u.PacksPulled).HasColumnName("packs_pulled").HasDefaultValue(0);
            user.Property(u => u.CardsTraded).HasColumnName("cards_traded").HasDefaultValue(0);
            user.Property(u => u.FavoriteCardId).HasColumnName("favorite_card_id");
            user.Property(u => u.LeftAtUtc).HasColumnName("left_at_utc");
            user.HasOne<CardRow>().WithMany().HasForeignKey(u => u.FavoriteCardId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CardRow>(card =>
        {
            card.ToTable("cards");
            card.HasKey(c => c.Id);
            card.Property(c => c.Id).HasColumnName("id");
            card.Property(c => c.UserId).HasColumnName("user_id");
            card.Property(c => c.SetId).HasColumnName("set_id");
            card.Property(c => c.LocalId).HasColumnName("local_id");
            card.Property(c => c.Name).HasColumnName("name");
            card.Property(c => c.Rarity).HasColumnName("rarity");
            card.Property(c => c.Image).HasColumnName("image");
            card.Property(c => c.CardmarketAvgCents).HasColumnName("cardmarket_avg_cents");
            card.Property(c => c.TcgplayerMarketCents).HasColumnName("tcgplayer_market_cents");
            card.Property(c => c.TcgplayerLowCents).HasColumnName("tcgplayer_low_cents");
            card.Property(c => c.IsLocked).HasColumnName("is_locked").HasDefaultValue(false);
            card.HasOne<UserRow>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
            card.HasIndex(c => c.UserId).HasDatabaseName("ix_cards_user_id");
        });

        modelBuilder.Entity<ExpeditionRow>(expedition =>
        {
            expedition.ToTable("expeditions");
            expedition.HasKey(e => e.UserId);
            expedition.Property(e => e.UserId).HasColumnName("user_id").ValueGeneratedNever();
            expedition.Property(e => e.LocationId).HasColumnName("location_id");
            expedition.Property(e => e.StartTimeUtc).HasColumnName("start_time_utc");
            expedition.Property(e => e.EndTimeUtc).HasColumnName("end_time_utc");
            expedition.HasOne<UserRow>().WithOne().HasForeignKey<ExpeditionRow>(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SettingRow>(setting =>
        {
            setting.ToTable("settings");
            setting.HasKey(s => s.Key);
            setting.Property(s => s.Key).HasColumnName("key");
            setting.Property(s => s.Value).HasColumnName("value");
        });

        modelBuilder.Entity<LockedSetRow>(lockedSet =>
        {
            lockedSet.ToTable("locked_sets");
            lockedSet.HasKey(l => l.SetId);
            lockedSet.Property(l => l.SetId).HasColumnName("set_id");
        });

        modelBuilder.Entity<ExpeditionLocation>(location =>
        {
            location.ToTable("expedition_locations");
            location.HasKey(l => l.Id);
            location.Property(l => l.Id).HasColumnName("id");
            location.Property(l => l.Name).HasColumnName("name");
            location.Property(l => l.DurationMinutes).HasColumnName("duration_minutes");
            location.Property(l => l.MinReward).HasColumnName("min_reward");
            location.Property(l => l.MaxReward).HasColumnName("max_reward");
            location.Property(l => l.CardRewardChance).HasColumnName("card_reward_chance");
            location.Property(l => l.CardRewardCount).HasColumnName("card_reward_count");
            location.Property(l => l.CardRewardSetId).HasColumnName("card_reward_set_id");
        });
    }
}
