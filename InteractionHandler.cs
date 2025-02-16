using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord;

namespace DiscordBot
{
    public class InteractionHandler
    {
        private readonly DiscordSocketClient _client;

        public InteractionHandler(DiscordSocketClient client)
        {
            _client = client;
            _client.SelectMenuExecuted -= HandleSelectMenu; // Avoid double event registration
            _client.SelectMenuExecuted += HandleSelectMenu;
        }

        public async Task HandleSelectMenu(SocketMessageComponent component)
        {
                return;
        }
    }
}