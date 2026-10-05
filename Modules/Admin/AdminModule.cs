using Discord;
using Discord.Interactions;
using DiscordBot.Core;
using DiscordBot.Preconditions;

namespace DiscordBot.Modules.Admin;

/// <summary>
///     Owner-only commands. Hidden from members without Administrator in the command picker;
///     <see cref="RequireBotOwnerAttribute"/> is what actually guards them.
/// </summary>
[DefaultMemberPermissions(GuildPermission.Administrator)]
[RequireBotOwner]
public sealed class AdminModule(BotState botState, BotStateStore stateStore) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("lockset", "Locks a specific set to prevent it from being pulled. (Admin only)")]
    public async Task LockSetAsync(
        [Summary("set-id", "The Set-ID of the Set you want to lock.")] string setId)
    {
        await DeferAsync(ephemeral: true);

        if (string.IsNullOrWhiteSpace(setId))
        {
            await FollowupAsync("❌ Invalid Set ID.");
            return;
        }

        if (botState.LockSet(setId))
        {
            await stateStore.SaveAsync();
            await FollowupAsync($"🔒 **Set Locked:** Users can no longer pull from `{setId}`.");
        }
        else
        {
            await FollowupAsync($"⚠️ Set `{setId}` is already in the locked list.");
        }
    }

    [SlashCommand("unlockset", "Unlocks a specific set to allow it to be pulled again. (Admin only)")]
    public async Task UnlockSetAsync(
        [Summary("set-id", "The Set-ID of the Set you want to unlock.")] string setId)
    {
        await DeferAsync(ephemeral: true);

        if (string.IsNullOrWhiteSpace(setId))
        {
            await FollowupAsync("❌ Invalid Set ID.");
            return;
        }

        if (botState.UnlockSet(setId))
        {
            await stateStore.SaveAsync();
            await FollowupAsync($"🔓 **Set Unlocked:** Users can now pull from `{setId}` again.");
        }
        else
        {
            await FollowupAsync($"ℹ️ Set `{setId}` wasn't locked to begin with.");
        }
    }

    [SlashCommand("turnoff", "Turns off the bot to prevent it from responding to commands. (Admin only)")]
    public async Task TurnOffAsync()
    {
        await DeferAsync(ephemeral: true);

        botState.IsActive = false;
        await stateStore.SaveAsync();

        await FollowupAsync("💤 Bot is now inactive.");
    }

    [SlashCommand("turnon", "Turns on the bot to allow it to respond to commands. (Admin only)")]
    public async Task TurnOnAsync()
    {
        await DeferAsync(ephemeral: true);

        botState.IsActive = true;
        await stateStore.SaveAsync();

        await FollowupAsync("🔌 Bot is now active.");
    }

    [SlashCommand("restart", "Restarts the bot.")]
    public async Task RestartAsync()
    {
        await DeferAsync(ephemeral: true);
        await FollowupAsync("🔄 Restarting...");

        await Task.Delay(1000);
        Program.RestartBot();
    }
}
