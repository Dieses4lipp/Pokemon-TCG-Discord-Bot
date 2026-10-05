using Discord;
using Discord.Interactions;
using DiscordBot.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Preconditions;

/// <summary>
///     Only lets the bot owner (<see cref="BotOptions.OwnerId"/>, or the Discord application
///     owner when that is unset) run the command.
/// </summary>
public sealed class RequireBotOwnerAttribute : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
    {
        ulong ownerId = services.GetRequiredService<BotOptions>().OwnerId
            ?? (await context.Client.GetApplicationInfoAsync()).Owner.Id;

        return context.User.Id == ownerId
            ? PreconditionResult.FromSuccess()
            : PreconditionResult.FromError("❌ Only the bot owner can use this command.");
    }
}
