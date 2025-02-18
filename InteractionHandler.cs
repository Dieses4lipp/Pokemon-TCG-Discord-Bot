using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord;

namespace DiscordBot
{
    /// <summary>
    /// Handles interactions like select menu events from the Discord client.
    /// </summary>
    public class InteractionHandler
    {
        private readonly DiscordSocketClient _client;

        /// <summary>
        /// Initializes a new instance of the <see cref="InteractionHandler"/> class.
        /// </summary>
        /// <param name="client">The Discord client instance used for event handling.</param>
        public InteractionHandler(DiscordSocketClient client)
        {
            _client = client;

            // Unsubscribe from event before subscribing to avoid multiple registrations
            _client.SelectMenuExecuted -= HandleSelectMenu;
            _client.SelectMenuExecuted += HandleSelectMenu;
        }

        /// <summary>
        /// Handles the Select Menu executed event.
        /// </summary>
        /// <param name="component">The <see cref="SocketMessageComponent"/> associated with the select menu event.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task HandleSelectMenu(SocketMessageComponent component)
        {
            // Handle select menu interaction here
            return;
        }
    }
}
