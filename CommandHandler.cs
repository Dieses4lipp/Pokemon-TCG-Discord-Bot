using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace DiscordBot
{
    public static class CommandHandler
    {
        public static async Task RegisterCommandsAsync(IServiceProvider services)
        {
            var commandService = services.GetRequiredService<CommandService>();
            await commandService.AddModulesAsync(Assembly.GetEntryAssembly(), services);
        }
    }
}
