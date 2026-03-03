using Discord;
using Discord.WebSocket;
using DiscordBot.Core;
using System.Diagnostics;

namespace DiscordBot.Commands.SlashCommandHandlers.AdminCommands.StatsCommand;

/// <summary>
///     A class containing the Handler for the /stats command
/// </summary>
public static class StatsCommandHandler
{
    /// <summary>
    ///     Handles the /stats command, showing the bot stats
    /// </summary>
    /// <param name="command">
    ///     The SocketSlashCommand object representing the command interaction.
    /// </param>
    /// <param name="client">
    ///     The current DiscordSocketClient
    /// </param>
    public static async Task Handle(SocketSlashCommand command, DiscordSocketClient client)
    {
        // Defer the response to give more time to process
        await command.DeferAsync(ephemeral: true);

        if (!CommandHandler.BotActive)
        {
            await command.FollowupAsync("💤 Bot is currently inactive. Use `/turnon` to activate.");
            return;
        }

        // Calculate uptime
        TimeSpan uptime = DateTime.UtcNow - Program.StartTime;

        // Get hardware/system metrics
        double cpuUsage = Process.GetCurrentProcess().WorkingSet64 / 1024.0 / 1024.0; // RAM in MB
        int guildCount = client.Guilds.Count;

        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle("📊 System Diagnostics")
            .WithColor(Color.Green)
            .WithThumbnailUrl(client.CurrentUser.GetAvatarUrl())
            // First Row: Time & Health
            .AddField("⏳ Uptime", $"`{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m`", true)
            .AddField("📡 Latency", $"`{client.Latency}ms`", true)
            .AddField("🖥️ Memory", $"`{cpuUsage:F2} MB`", true)
            // Second Row: Activity
            .AddField("🃏 Global Pulls", $"`{CommandHandler.PullCount}`", true)
            .AddField("🏰 Servers", $"`{guildCount}`", true)
            .AddField("⚙️ Status", "`Operational`", true)
            .WithFooter(footer => footer.Text = $"Requested by {command.User.Username}")
            .WithCurrentTimestamp();

        await command.FollowupAsync(embed: embed.Build());
    }
}