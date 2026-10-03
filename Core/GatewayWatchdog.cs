using Discord;
using Discord.WebSocket;

namespace DiscordBot.Core;

/// <summary>
///     Exits the process when the gateway has not been connected for too long, so Docker
///     (restart: unless-stopped) starts a fresh container.
/// </summary>
/// <remarks>
///     Discord.Net can hang in <see cref="ConnectionState.Connecting"/> forever after a dropped
///     gateway without raising any event, so the connection state is polled instead.
/// </remarks>
public static class GatewayWatchdog
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxNotConnected = TimeSpan.FromMinutes(3);

    /// <summary>
    ///     Polls the client's connection state forever and exits with code 1 once it has not been
    ///     connected for longer than the allowed time.
    /// </summary>
    /// <param name="client">
    ///     The Discord client to watch.
    /// </param>
    /// <returns>
    ///     A task that never completes unless the process exits.
    /// </returns>
    public static async Task RunAsync(DiscordSocketClient client)
    {
        DateTime? notConnectedSince = null;

        while (true)
        {
            await Task.Delay(CheckInterval);

            if (client.ConnectionState == ConnectionState.Connected)
            {
                notConnectedSince = null;
                continue;
            }

            notConnectedSince ??= DateTime.UtcNow;

            if (DateTime.UtcNow - notConnectedSince > MaxNotConnected)
            {
                Console.WriteLine($"Watchdog: gateway state {client.ConnectionState} since {notConnectedSince:O}, exiting");
                Environment.Exit(1);
            }
        }
    }
}
