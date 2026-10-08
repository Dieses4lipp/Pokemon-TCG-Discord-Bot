namespace DiscordBot.Core;

/// <summary>
///     Settings read from the environment at startup.
/// </summary>
/// <param name="Token">
///     The bot token (TOKEN).
/// </param>
/// <param name="OwnerId">
///     The Discord user allowed to run owner commands (OWNER_ID). When unset, the owner of the
///     Discord application is used.
/// </param>
public sealed record BotOptions(string Token, ulong? OwnerId);
