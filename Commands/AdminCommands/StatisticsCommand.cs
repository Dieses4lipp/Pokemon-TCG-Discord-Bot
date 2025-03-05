using Discord;
using Discord.Commands;
using DiscordBot.Core;

namespace DiscordBot.Commands
{
    /// <summary>
    ///     Provides a command to display bot statistics, including uptime and API
    ///     latency.
    /// </summary>
    public class StatisticsCommand : ModuleBase<SocketCommandContext>
    {
        /// <summary>
        ///     Displays the bot's statistics such as uptime, number of pulls, and API
        ///     latency.
        /// </summary>
        [Command("stats")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task StatsAsync()
        {
            if (!CommandHandler.BotActive)
            {
                await ReplyAsync("The bot is currently inactive and not responding to commands.");
                return;
            }

            // Calculate uptime
            TimeSpan uptime = DateTime.UtcNow - Program.StartTime;

            // Create an embed with the statistics
            EmbedBuilder embed = new EmbedBuilder()
                .WithTitle("Bot Statistics")
                .AddField("Uptime", $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s")
                .AddField("Number of Pulls since Start", CommandHandler.PullCount)
                .AddField("Last Api Latency in ms", CommandHandler.LastApiLatency)
                .WithColor(Color.Green);

            await ReplyAsync(embed: embed.Build());
        }
    }
}