using Discord;
using DiscordBot.Models;

namespace DiscordBot.Core;

/// <summary>
///     Builds the embed shown for a single card in pack and inventory views.
/// </summary>
public static class CardEmbeds
{
    /// <summary>
    ///     Image shown for cards the API delivers without an image.
    /// </summary>
    private static readonly string DefaultCardImagePath =
        Path.Combine(AppContext.BaseDirectory, "Assets", "sets_covers", "default.jpg");

    /// <summary>
    ///     Builds the attachments a card embed from <see cref="BuildCardEmbed"/> needs.
    ///     Must be passed on every send/modify, so a previous card's fallback image gets removed.
    /// </summary>
    /// <param name="card">
    ///     The card to display.
    /// </param>
    /// <returns>
    ///     The fallback image if the card has no image URL, otherwise no attachments.
    ///     The caller disposes them after sending.
    /// </returns>
    public static FileAttachment[] BuildCardAttachments(Card card)
    {
        return string.IsNullOrWhiteSpace(card.Image)
            ? [new FileAttachment(DefaultCardImagePath)]
            : [];
    }

    /// <summary>
    ///     Builds an embed to display a Pokémon card.
    /// </summary>
    /// <param name="card">
    ///     The card to display.
    /// </param>
    /// <param name="current">
    ///     The current index of the card within a session.
    /// </param>
    /// <param name="total">
    ///     The total number of cards in the session.
    /// </param>
    /// <returns>
    ///     An <see cref="Embed"/> representing the card's details.
    /// </returns>
    public static Embed BuildCardEmbed(Card card, int current, int total)
    {
        double marketPrice =
            card.Pricing?.TcgPlayer?.Market ??
            card.Pricing?.TcgPlayer?.Low ??
            card.Pricing?.Cardmarket?.Avg ??
            0.50;
        // Discord only accepts URLs here; the local fallback is sent as an attachment (see BuildCardAttachments)
        string cardImageUrl = string.IsNullOrWhiteSpace(card.Image)
            ? $"attachment://{Path.GetFileName(DefaultCardImagePath)}"
            : $"{card.Image}/low.png";

        return new EmbedBuilder()
            .WithTitle($"{card.Name} ({current}/{total})")
            .WithDescription($"Rarity: {card.Rarity ?? "unknown"}\nEstimated Value: **${marketPrice:F2}**")
            .WithImageUrl(cardImageUrl)
            .WithColor(Color.Blue)
            .Build();
    }
}
