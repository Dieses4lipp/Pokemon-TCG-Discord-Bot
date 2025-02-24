using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Models
{
    /// <summary>
    ///     Represents a session for navigating through a list of Pokémon card sets.
    /// </summary>
    public class SetSession
    {
        /// <summary>
        ///     Gets the message ID associated with the session.
        /// </summary>
        public ulong MessageId { get; }

        /// <summary>
        ///     Gets the user ID of the participant in the session.
        /// </summary>
        public ulong UserId { get; }

        /// <summary>
        ///     Gets the list of sets in the session.
        /// </summary>
        public List<Set> Sets { get; }

        /// <summary>
        ///     Gets or sets the current index of the displayed set.
        /// </summary>
        public int CurrentIndex { get; set; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="SetSession" /> class.
        /// </summary>
        /// <param name="messageId">The message ID associated with the session.</param>
        /// <param name="userId">The user ID of the session participant.</param>
        /// <param name="sets">The list of sets to navigate.</param>
        public SetSession(ulong messageId, ulong userId, List<Set> sets)
        {
            MessageId = messageId;
            UserId = userId;
            Sets = sets;
            CurrentIndex = 0;
        }
    }
}
