using Discord;
using Discord.Commands;
using DiscordBot.Core;
using System;
using System.Threading.Tasks;

namespace DiscordBot.Commands
{
    public class StatisticsCommand : ModuleBase<SocketCommandContext>
    {
        [Command("stats")]
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