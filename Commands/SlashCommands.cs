using Discord;

namespace DiscordBot.Commands;

/// <summary>
///     Models of the slash commands used in the bot, providing builders for each command and their options.
/// </summary>
public static class SlashCommands
{
    public static SlashCommandBuilder PullCommand() =>
        new SlashCommandBuilder()
                .WithName("pull")
                .WithDescription("Pulls a pack with given set ID.")
                .AddOption(
                    "set-id",
                    ApplicationCommandOptionType.String,
                    "The ID of the set to pull from.",
                    isRequired: true
                );

    public static SlashCommandBuilder HelpCommand() =>
            new SlashCommandBuilder()
                .WithName("help")
                .WithDescription("Lists all available commands.");

    public static SlashCommandBuilder InventoryCommand() =>
        new SlashCommandBuilder()
            .WithName("inventory")
            .WithDescription("Displays a your card collection.");
}