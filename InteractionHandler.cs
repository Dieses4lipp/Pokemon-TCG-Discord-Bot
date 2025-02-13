using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            _client.SelectMenuExecuted += HandleSelectMenu;
        }

        private async Task HandleSelectMenu(SocketMessageComponent component)
        {
            if (component.Data.CustomId == "set_selection")
            {
                string selectedSetId = component.Data.Values.First();

                var embed = new EmbedBuilder()
                    .WithTitle($"Ausgewähltes Set: {selectedSetId}")
                    .WithDescription($"Jetzt kannst du Karten aus {selectedSetId} ziehen!")
                    .WithColor(Color.Blue)
                    .Build();

                await component.UpdateAsync(msg => msg.Embed = embed);
            }
        }
    }

}
