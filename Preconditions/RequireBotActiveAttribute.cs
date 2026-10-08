using Discord;
using Discord.Interactions;
using DiscordBot.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Preconditions;

/// <summary>
///     Rejects the interaction while the bot is turned off with /turnoff.
/// </summary>
public sealed class RequireBotActiveAttribute : PreconditionAttribute
{
    public override Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
    {
        bool isActive = services.GetRequiredService<BotState>().IsActive;

        return Task.FromResult(isActive
            ? PreconditionResult.FromSuccess()
            : PreconditionResult.FromError("💤 Bot is currently inactive."));
    }
}
