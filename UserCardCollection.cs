using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{
    using System.Collections.Generic;
    using static global::DiscordBot.CommandsModule;

    public class UserCardCollection
    {
        public ulong UserId { get; set; }
        public List<Card> Cards { get; set; }
    }

}
