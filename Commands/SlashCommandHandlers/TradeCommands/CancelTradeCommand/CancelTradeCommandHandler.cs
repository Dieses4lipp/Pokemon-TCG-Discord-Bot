using Discord;
using Discord.WebSocket;
using DiscordBot.Core;

namespace DiscordBot.Commands.SlashCommandHandlers.TradeCommands.CancelTradeCommand;

/// <summary>
///     A class that contains the Handler for /canceltrade command
/// </summary>
public static class CancelTradeCommandHandler
{
    /// <summary>
    ///     Handles the /canceltrade command, cancelling the trade
    /// </summary>
    /// <param name="command">
    ///     The <see cref="SocketSlashCommand"/> representing the slash command that was executed by
    /// </param>
    public static async Task Handle(SocketSlashCommand command)
    {
        // Defer the response to give more time to process
        await command.DeferAsync(ephemeral: false);

        if (!CommandHandler.BotActive)
        {
            await command.FollowupAsync("💤 Bot is inactive.", ephemeral: true);
            return;
        }

        // Checks if the user is actually in a trade
        if (!CommandHandler.ActiveTrades.TryGetValue(command.User.Id, out Models.TradeSession? session))
        {
            await command.FollowupAsync("❌ You don't have any pending trades to cancel.", ephemeral: true);
            return;
        }

        // Identifies the partner to notify them
        ulong partnerId = (command.User.Id == session.SenderId) ? session.ReceiverId : session.SenderId;

        CommandHandler.ActiveTrades.Remove(session.SenderId);
        CommandHandler.ActiveTrades.Remove(session.ReceiverId);

        var embed = new EmbedBuilder()
            .WithTitle("🚫 Trade Cancelled")
            .WithDescription($"{command.User.Mention} has cancelled the trade with {MentionUtils.MentionUser(partnerId)}.")
            .WithColor(Color.Red)
            .WithCurrentTimestamp()
            .Build();

        await command.FollowupAsync(embed: embed);
    }
}