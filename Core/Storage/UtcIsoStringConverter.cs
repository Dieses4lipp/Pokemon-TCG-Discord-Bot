using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DiscordBot.Core.Storage;

/// <summary>
///     Stores a time as round-trip ISO 8601 text in UTC (<c>2026-10-08T12:00:00.0000000Z</c>), the
///     format the hand-written SQLite schema used. Fixed width, so the text sorts in time order.
/// </summary>
public sealed class UtcIsoStringConverter() : ValueConverter<DateTime, string>(
    value => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
    text => DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
