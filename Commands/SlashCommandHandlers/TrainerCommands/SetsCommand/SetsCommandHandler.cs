using Discord;
using Discord.WebSocket;
using DiscordBot.Core;
using DiscordBot.Models;
using Newtonsoft.Json;

namespace DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.SetsCommand;

/// <summary>
///     A class that contains the Handler for the /sets command
/// </summary>
public static class SetsCommandHandler
{
    /// <summary>
    ///     Handles the /sets command
    /// </summary>
    /// <param name="command">
    ///     The slash command that triggered the handler, containing user data and options.
    /// </param>
    public static async Task Handle(SocketSlashCommand command)
    {
        // Defer the response to give more time to process
        await command.DeferAsync();

        if (!CommandHandler.BotActive)
        {
            await command.FollowupAsync("💤 Bot is currently inactive. Use `/turnon` to activate.");
            return;
        }

        try
        {
            var loadingMessage = await command.FollowupAsync("⏳ Fetching latest Pokémon sets...");

            // Fetches API Data
            string response = await CommandHandler._httpClient.GetStringAsync(CommandHandler.SetsApiUrl);
            var setsList = JsonConvert.DeserializeObject<List<Set>>(response);

            if (setsList == null || setsList.Count == 0)
            {
                await loadingMessage.ModifyAsync(m => m.Content = "❌ No sets found!");
                return;
            }

            // Then use setsList directly:
            var sortedSets = setsList
                .OrderByDescending(s => s.Name)
                .Take(25)
                .ToList();

            var selectMenu = new SelectMenuBuilder()
                .WithPlaceholder("Choose a set to see its ID")
                .WithCustomId("set_selection_info")
                .WithMinValues(1)
                .WithMaxValues(1);

            foreach (var set in sortedSets)
            {
                selectMenu.AddOption(set.Name, set.Id, $"| ID: {set.Id}");
            }

            var components = new ComponentBuilder().WithSelectMenu(selectMenu).Build();

            var embed = new EmbedBuilder()
                .WithTitle("📂 Pokémon TCG Sets")
                .WithDescription("Select a set from the menu to view its unique **Set ID**. Use this ID with the `/pull` command!")
                .WithColor(Color.Green)
                .WithFooter("Showing the 25 most recent sets.")
                .WithCurrentTimestamp()
                .Build();

            await loadingMessage.ModifyAsync(msg =>
            {
                msg.Content = "";
                msg.Embed = embed;
                msg.Components = components;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Set Fetch Error: {ex.Message}");
            await command.FollowupAsync("⚠️ An error occurred while contacting the TCG API.");
        }
    }
}