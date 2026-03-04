using Discord;
using Discord.WebSocket;
using DiscordBot.Core;
using DiscordBot.Models;
using Newtonsoft.Json;

namespace DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.SetsCommand;

/// <summary>
///     A class that contains the Handler for the /sets command
/// </summary>
public static class TestSetsCommandHandler
{
    /// <summary>
    ///     Handles the /sets command
    /// </summary>
    /// <param name="command">
    ///     The slash command that triggered the handler, containing user data and options.
    /// </param>
    public static async Task Handle(SocketSlashCommand command)
    {
        await command.DeferAsync();

        if (!CommandHandler.BotActive)
        {
            await command.FollowupAsync("💤 Bot is currently inactive. Use `/turnon` to activate.");
            return;
        }

        try
        {
            var loadingMessage = await command.FollowupAsync("⏳ Fetching latest Pokémon sets...");

            string response = await CommandHandler._httpClient.GetStringAsync(CommandHandler.SetsApiUrl + "?pagination:page=1&pagination:itemsPerPage=25");
            var setsList = JsonConvert.DeserializeObject<List<Set>>(response);

            if (setsList == null || setsList.Count == 0)
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
            Console.WriteLine($"Error building response embed: {ex.Message}");
            await command.FollowupAsync("⚠️ An error occured while building the response message.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Set Fetch Error: {ex.Message}");
            await command.FollowupAsync("⚠️ An error occurred while contacting the TCG API.");
        }
    }
}