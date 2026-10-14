using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DiscordBot.Core.Storage;

/// <summary>
///     Hands Npgsql UTC times only: it refuses to write a local or unspecified
///     <see cref="DateTime"/> to a <c>timestamp with time zone</c> column.
/// </summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value.ToUniversalTime(),
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
