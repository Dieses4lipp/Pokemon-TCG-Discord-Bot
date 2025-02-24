using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiscordBot.Models;
using static DiscordBot.Commands.CommandsModule;

namespace DiscordBot.Services
{
    /// <summary>
    /// Represents a Pokémon card with its name, rarity, and images.
    /// </summary>
    public class Card
    {
        /// <summary>
        /// Gets or sets the name of the card.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the rarity of the card.
        /// </summary>
        public string Rarity { get; set; }

        /// <summary>
        /// Gets or sets the images associated with the card.
        /// </summary>
        public Images Images { get; set; }
    }
}
