using Discord;

namespace DiscordBot.Commands;

/// <summary>
///     Models of the slash commands used in the bot, providing builders for each command and their options.
/// </summary>
public static class SlashCommandBuilders
{
    public static SlashCommandBuilder PullCommand() =>
        new SlashCommandBuilder()
                .WithName("pull")
                .WithDescription("Pulls a pack with given set ID.")
                .AddOption(
                    "set-id",
                    ApplicationCommandOptionType.String,
                    "The ID of the set to pull from.",
                    isRequired: true,
                    isAutocomplete: true
                );

    public static SlashCommandBuilder HelpCommand() =>
            new SlashCommandBuilder()
                .WithName("help")
                .WithDescription("Lists all available commands.");

    public static SlashCommandBuilder InventoryCommand() =>
        new SlashCommandBuilder()
            .WithName("inventory")
            .WithDescription("Displays a your card collection.");

    public static SlashCommandBuilder ProfileCommand() =>
        new SlashCommandBuilder()
            .WithName("profile")
            .WithDescription("Displays a User's profile.")
            .AddOption(
                "user",
                ApplicationCommandOptionType.User,
                "The User which you want to show the Profile of.",
                isRequired: true
            );

    public static SlashCommandBuilder StatsCommand() =>
        new SlashCommandBuilder()
            .WithName("stats")
            .WithDescription("Displays bot statistics.");

    public static SlashCommandBuilder RestartCommand() =>
        new SlashCommandBuilder()
            .WithName("restart")
            .WithDescription("Restarts the bot.");

    public static SlashCommandBuilder TurnOffCommand() =>
        new SlashCommandBuilder()
            .WithName("turnoff")
            .WithDescription("Turns off the bot to prevent it from responding to commands. (Admin only)");

    public static SlashCommandBuilder TurnOnCommand() =>
        new SlashCommandBuilder()
            .WithName("turnon")
            .WithDescription("Turns on the bot to allow it to respond to commands. (Admin only)");

    public static SlashCommandBuilder LockSetCommand() =>
        new SlashCommandBuilder()
            .WithName("lockset")
            .WithDescription("Locks a specific set to prevent it from being pulled. (Admin only)")
            .AddOption(
            "set-id",
            ApplicationCommandOptionType.String,
            "The Set-ID of the Set you want to lock.",
            isRequired: true);

    public static SlashCommandBuilder UnlockSetCommand() =>
        new SlashCommandBuilder()
            .WithName("unlockset")
            .WithDescription("Unlocks a specific set to allow it to be pulled again. (Admin only)")
            .AddOption(
            "set-id",
            ApplicationCommandOptionType.String,
            "The Set-ID of the Set you want to unlock.",
            isRequired: true);

    public static SlashCommandBuilder TradeCommand() =>
        new SlashCommandBuilder()
            .WithName("trade")
            .WithDescription("Propose a trade")
            .AddOption(
                "user",
                ApplicationCommandOptionType.User,
                "The User you want to trade with.",
                isRequired: true
            )
            .AddOption(
                "give-card",
                ApplicationCommandOptionType.String,
                "The name of the card you want to give.",
                isRequired: true
                )
            .AddOption(
                "receive-card",
                ApplicationCommandOptionType.String,
                "The name of the card you want to receive.",
                isRequired: true
                );

    public static SlashCommandBuilder ConfirmTradeCommand() =>
        new SlashCommandBuilder()
            .WithName("confirmtrade")
            .WithDescription("Accept the current trade");

    public static SlashCommandBuilder CancelTradeCommand() =>
        new SlashCommandBuilder()
            .WithName("canceltrade")
            .WithDescription("End the trade session");

    public static SlashCommandBuilder SetsCommand() =>
        new SlashCommandBuilder()
            .WithName("sets")
            .WithDescription("Displays a list of available Pokémon card sets.");
}