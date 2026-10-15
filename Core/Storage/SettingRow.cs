namespace DiscordBot.Core.Storage;

/// <summary>
///     A row of the <c>settings</c> table: one key/value pair of the runtime state.
/// </summary>
public sealed class SettingRow
{
    public string Key { get; set; } = default!;

    public string Value { get; set; } = default!;
}
