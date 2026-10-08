using System.Diagnostics;
using Discord;
using Discord.Interactions;
using DiscordBot.Core;
using DiscordBot.Preconditions;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Modules.Trainer;

/// <summary>
///     The read-only trainer commands: /stats, /profile and /sets.
/// </summary>
[RequireBotActive]
public sealed class TrainerModule(
    BotState botState,
    UserRepository users,
    CardApiClient api,
    ILogger<TrainerModule> logger)
    : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("stats", "Displays bot statistics.")]
    public async Task StatsAsync()
    {
        await DeferAsync(ephemeral: true);

        // Calculate uptime
        TimeSpan uptime = DateTime.UtcNow - botState.StartedAtUtc;

        // Get hardware/system metrics
        double cpuUsage = Process.GetCurrentProcess().WorkingSet64 / 1024.0 / 1024.0; // RAM in MB
        int guildCount = Context.Client.Guilds.Count;

        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle("📊 System Diagnostics")
            .WithColor(Color.Green)
            .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl())
            // First Row: Time & Health
            .AddField("⏳ Uptime", $"`{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m`", true)
            .AddField("📡 Latency", $"`{Context.Client.Latency}ms`", true)
            .AddField("🖥️ Memory", $"`{cpuUsage:F2} MB`", true)
            // Second Row: Activity
            .AddField("🃏 Global Pulls", $"`{botState.PullCount}`", true)
            .AddField("🏰 Servers", $"`{guildCount}`", true)
            .AddField("⚙️ Status", "`Operational`", true)
            .WithFooter(footer => footer.Text = $"Requested by {Context.User.Username}")
            .WithCurrentTimestamp();

        await FollowupAsync(embed: embed.Build());
    }

    [SlashCommand("profile", "Displays a User's profile.")]
    public async Task ProfileAsync(
        [Summary("user", "The User which you want to show the Profile of.")] IUser user)
    {
        await DeferAsync(ephemeral: true);

        var collection = await users.LoadUserCardsAsync(user.Id);

        var embed = new EmbedBuilder()
            .WithTitle($"📊 {user.Username}'s Trainer Profile")
            .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
            .AddField("📦 Packs Pulled", collection.PacksPulled, true)
            .AddField("📇 Total Saved", collection.Cards?.Count ?? 0, true)
            .AddField("✨ Unique Cards", collection.DifferentCardsSaved, true)
            .AddField("🤝 Cards Traded", collection.CardsTraded, true)
            .AddField("💰 Balance", $"${collection.Balance:F2}", true)
            .WithColor(Color.Blue)
            .WithCurrentTimestamp();

        if (collection.FavoriteCard != null)
        {
            embed.AddField("⭐Favorite Card⭐", collection.FavoriteCard.Name)
                .WithImageUrl($"{collection.FavoriteCard.Image}/low.png");
        }
        else
        {
            embed.AddField("⭐ Favorite Card", "No favorite set yet! Use ⭐ in your inventory.");
        }

        await FollowupAsync(embed: embed.Build());
    }

    [SlashCommand("sets", "Displays a list of available Pokémon card sets.")]
    public async Task SetsAsync()
    {
        await DeferAsync(ephemeral: true);

        try
        {
            var loadingMessage = await FollowupAsync("⏳ Fetching latest Pokémon sets...");

            var setsList = await api.GetFirstSetsAsync(25);

            if (setsList.Count == 0)
            {
                await loadingMessage.ModifyAsync(m => m.Content = "❌ No sets found!");
                return;
            }

            var sortedSets = setsList
                .OrderBy(s => s.Name)
                .Take(24)
                .ToList();

            var embedBuilder = new EmbedBuilder()
                .WithTitle("📂 Pokémon TCG Sets")
                .WithDescription("Use these IDs with the `/pull` command!")
                .WithColor(Color.Green)
                .WithFooter("Showing the 25 most recent sets.")
                .WithCurrentTimestamp();

            foreach (var set in sortedSets)
            {
                embedBuilder.AddField(set.Name, $"`{set.Id}` \n", inline: true);
            }

            await loadingMessage.ModifyAsync(msg =>
            {
                msg.Content = "";
                msg.Embed = embedBuilder.Build();
                msg.Components = null;
            });
        }
        catch (ArgumentException ex)
        {
            logger.LogError(ex, "Error building /sets response embed");
            await FollowupAsync("⚠️ An error occured while building the response message.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch sets for /sets");
            await FollowupAsync("⚠️ An error occurred while contacting the TCG API.");
        }
    }
}
