using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DiscordBot.CommandsModule;

namespace DiscordBot
{

    public class Card
    {
        public string Name { get; set; }
        public string Rarity { get; set; }
        public Images Images { get; set; }
    }
}
