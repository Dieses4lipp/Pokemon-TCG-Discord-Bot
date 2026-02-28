using System.Collections.Concurrent;
using Discord.Commands;

namespace DiscordBot.Core;

/// <summary>
///     A custom attribute to enforce a daily usage limit for commands.
/// </summary>
public class DailyUsageLimitAttribute : PreconditionAttribute
{
    // Tracks each user's usage: key is user ID, value is a tuple of (date, count)
    private static readonly ConcurrentDictionary<ulong, (DateTime Date, int Count)> _userUsage = new();

    private readonly int _limit;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DailyUsageLimitAttribute"/> class.
    /// </summary>
    /// <param name="limit">
    ///     The maximum number of times the command can be used per day.
    /// </param>
    public DailyUsageLimitAttribute(int limit)
    {
        _limit = limit;
    }

    /// <summary>
    ///     Checks if the user has exceeded their daily usage limit for a command.
    /// </summary>
    /// <param name="context">
    ///     The command context.
    /// </param>
    /// <param name="command">
    ///     The command information.
    /// </param>
    /// <param name="services">
    ///     The service provider.
    /// </param>
    /// <returns>
    ///     A task representing the asynchronous operation, with a result indicating the
    ///     precondition check result.
    /// </returns>
    public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command,
        IServiceProvider services)
    {
        ulong userId = context.User.Id;
        DateTime today = DateTime.UtcNow.Date;

        // Get or add the usage record for the user
        (DateTime Date, int Count) usage = _userUsage.GetOrAdd(userId,
            (today, 0));

        // If the stored record is from a previous day, reset it
        if (usage.Date < today)
        {
            usage = (today, 0);
            _userUsage[userId] = usage;
        }

        // If the user has exceeded the limit, return an error
        if (usage.Count >= _limit)
        {
            return Task.FromResult(
                PreconditionResult.FromError(
                    $"You have reached your daily limit of {_limit} uses for this command."));
        }

        // Increment the count and update the record
        _userUsage[userId] = (usage.Date, usage.Count + 1);

        return Task.FromResult(PreconditionResult.FromSuccess());
    }
}