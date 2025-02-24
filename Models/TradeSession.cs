using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiscordBot.Services;

namespace DiscordBot.Models
{
    public class TradeSession
    {
        public ulong SenderId { get; }
        public ulong ReceiverId { get; }
        public Card CardToTrade { get; }

        public TradeSession(ulong senderId, ulong receiverId, Card cardToTrade)
        {
            SenderId = senderId;
            ReceiverId = receiverId;
            CardToTrade = cardToTrade;
        }
    }
}
