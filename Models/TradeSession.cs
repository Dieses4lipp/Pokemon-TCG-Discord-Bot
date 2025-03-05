using System;
using DiscordBot.Services;

namespace DiscordBot.Models
{
    /// <summary>
    /// Represents a trade session between two users.
    /// </summary>
    public class TradeSession
    {
        /// <summary>
        /// Gets the ID of the user who is sending the card.
        /// </summary>
        public ulong SenderId { get; }

        /// <summary>
        /// Gets the ID of the user who is receiving the card.
        /// </summary>
        public ulong ReceiverId { get; }

        /// <summary>
        /// Gets the card that is being traded.
        /// </summary>
        public Card CardToTrade { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TradeSession"/> class.
        /// </summary>
        /// <param name="senderId">The ID of the user who is sending the card.</param>
        /// <param name="receiverId">The ID of the user who is receiving the card.</param>
        /// <param name="cardToTrade">The card being traded.</param>
        public TradeSession(ulong senderId, ulong receiverId, Card cardToTrade)
        {