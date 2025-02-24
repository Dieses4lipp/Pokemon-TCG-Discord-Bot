﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiscordBot.Services;

namespace DiscordBot.Models
{
    /// <summary>
    ///     Represents the API response containing a list of Pokémon cards.
    /// </summary>
    public class ApiResponse
    {
        /// <summary>
        ///     Gets or sets the list of cards returned from the API.
        /// </summary>
        public List<Card> Data { get; set; }
    }
}
