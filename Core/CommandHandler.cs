using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace DiscordBot.Core
{
    /// <summary>
    /// Handles the registration of commands for the bot.
    /// </summary>
    public static class CommandHandler
    {
        /// <summary>
        /// Registers all commands within the assembly.
        /// </summary>
        /// <param name="services">The service provider used to resolve services.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task RegisterCommandsAsync(IServiceProvider services)
        {
            var commandService = services.GetRequiredService<CommandService>();
            await commandService.AddModulesAsync(Assembly.GetEntryAssembly(), services);
        }
    }
}
