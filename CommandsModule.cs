using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;

namespace DiscordBot
{
    public class CommandsModule : ModuleBase<SocketCommandContext>
    {
        [Command("ping")]
        public async Task PingAsync()
        {
            Console.WriteLine("Ping command triggered!"); 
            await ReplyAsync("Pong!");
        }
    }
}
