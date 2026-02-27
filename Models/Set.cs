namespace DiscordBot.Models
{
    /// <summary>
    ///     Represents a Pokémon card set.
    /// </summary>
    public class Set
    {
        /// <summary>
        ///     Gets or sets the name of the set.
        /// </summary>
        public string Name { get; set; } = default!;

        /// <summary>
        ///     Gets or sets the unique identifier for the set.
        /// </summary>
        public string Id { get; set; } = default!;

        /// <summary>
        ///     Gets or sets the URL for the set's image.
        /// </summary>
        public string ImageUrl { get; set; } = default!;
    }
}