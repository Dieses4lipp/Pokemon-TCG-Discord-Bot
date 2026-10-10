namespace DiscordBot.Core;

/// <summary>
///     Converts the floating point amounts coming from the card API, the config files and slash
///     command options into the <see cref="decimal"/> amounts balances are kept in.
/// </summary>
public static class Money
{
    /// <summary>
    ///     Rounds an amount to whole cents.
    /// </summary>
    public static decimal FromDouble(double amount) =>
        Math.Round((decimal)amount, 2, MidpointRounding.AwayFromZero);
}
