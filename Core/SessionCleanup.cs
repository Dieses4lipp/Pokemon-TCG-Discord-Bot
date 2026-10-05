namespace DiscordBot.Core;

/// <summary>
///     Periodically drops expired sessions, so abandoned pack views, inventory views and
///     unanswered trades do not pile up in memory and in the state file.
/// </summary>
public sealed class SessionCleanup(SessionStore sessions, BotStateStore stateStore)
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);

    /// <summary>
    ///     Removes expired sessions every <see cref="CheckInterval"/>. Never throws.
    /// </summary>
    /// <returns>
    ///     A task that runs for the lifetime of the process.
    /// </returns>
    public async Task RunAsync()
    {
        while (true)
        {
            await Task.Delay(CheckInterval);

            try
            {
                if (sessions.RemoveExpired(DateTime.UtcNow))
                    await stateStore.SaveAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to clean up expired sessions: {ex}");
            }
        }
    }
}
