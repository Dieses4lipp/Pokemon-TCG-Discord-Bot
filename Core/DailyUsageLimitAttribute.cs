using System.Collections.Concurrent;
using Discord.Commands;

namespace DiscordBot.Core
{
    public class DailyUsageLimitAttribute : PreconditionAttribute
    {
        private readonly int _limit;

        // Tracks each user's usage: key is user ID, value is a tuple of (date, count)
        private static readonly ConcurrentDictionary<ulong, (DateTime Date, int Count)> _userUsage = new();

        public DailyUsageLimitAttribute(int limit)
        {
            _limit = limit;
        }

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
}
