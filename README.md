[api-url]: https://tcgdex.dev/
[discord-developer-portal]: https://discord.com/developers/applications
[discord-dotnet]: https://github.com/discord-net/Discord.Net
[github]: https://github.com/Dieses4lipp
[linkedin-profile]: https://www.linkedin.com/in/philipp-spiekermann-a01975352/
[x-profile]: https://x.com/DiesesPhilipp
[dotnet-download]: https://dotnet.microsoft.com/download

# Pokémon TCG Discord Bot
[![GitHub](https://img.shields.io/badge/GitHub-Dieses4lipp-blue?logo=github)][github]
[![LinkedIn](https://img.shields.io/badge/LinkedIn-Philipp%20Spiekermann-blue?style=flat&logo=linkedin)][linkedin-profile] 
[![Twitter](https://img.shields.io/badge/Twitter-@DiesesPhilipp-1DA1F2?style=flat&logo=x)][x-profile]


A feature-rich Discord bot that brings the Pokémon Trading Card Game experience to your server. Pull packs with random Pokémon cards, manage your personal collection, trade with friends, and more - all powered by the [Pokémon TCG API][api-url].

---

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [Configuration](#configuration)
- [Usage](#usage)
- [Commands](#commands)
- [Bot Architecture](#bot-architecture)
- [Contributing](#contributing)
- [Acknowledgements](#acknowledgements)

---

## Features

- **Pack Pulling:**  
  Pull a pack containing 9 random Pokémon cards using `/pull [set-id]`.
  
- **Collection Management:**  
  View your saved cards with `/inventory` and manage your collection directly in Discord.

- **Set Browsing: (Currently n development)**  
  Use `/sets` to see a list of available Pokémon card sets.

- **Trading System: (Currently n development)**  
  Initiate trades with other users using `/trade [user] [your-card] [their-card]` along with commands to confirm (`/confirmtrade`) or cancel (`/canceltrade`) a trade.

- **User Profiles:**  
  Display user profiles with `/profile [user]` to see their collection or trading history.

- **Administrative Commands:**  
  Admins can lock or unlock specific card sets (`/lock [set-id]` and `/unlock [set-id]`), restart the bot (`/restart`), or control its active status using `/turnon`/`/turnoff`. Additionally, view bot statistics with `/stats`.

- **Robust Command Handling:**  
  Commands are processed using a dedicated command handler with logging and detailed error reporting.

---

## Installation

### Prerequisites

- Download [.NET 6.0 SDK or later][dotnet-download]
- A Discord account and a bot token (obtain one via the [Discord Developer Portal][discord-developer-portal])
- An API key for the [Pokémon TCG API][api-url] (optional but recommended for higher rate limits)

### Setup Steps

1. **Clone the Repository:**

   ```bash
   git clone https://github.com/Dieses4lipp/Pokemon-TCG-Discord-Bot
   cd yourrepo
   ```

2. **Configure Environment Variables:**

   Create a `.env` file or set environment variables in your preferred way. **Important:** Store your bot token only in this file and never share it publicly. For example, your `.env` file should look like:

   ```env
   TOKEN=your_new_discord_bot_token_here
   ```

3. **Restore Dependencies and Build:**

   ```bash
   dotnet restore
   dotnet build
   ```

4. **Run the Bot:**

   ```bash
   dotnet run
   ```

---

## Configuration

- **API Endpoints:**  
  The bot uses endpoints from the [Pokémon TCG API][api-url] to fetch card and set data. Update these in the `CommandHandler` class if necessary.

- **Logging:**  
  Logging is routed to the console to help you monitor bot activity and debug errors.

- **Security Note:**  
  **Never share your bot token publicly!** Always store it securely in your environment variables or configuration files (e.g., the `.env` file). If your token is ever exposed, reset it immediately through the [Discord Developer Portal][discord-developer-portal].

---

## Usage

Once the bot is running and added to your Discord server, interact with it using the commands listed below. It automatically logs in, connects, and starts listening to slash commands with the `/` prefix. The commands are server specific and are automatically registered when the bot joins a server. 

**Important:** to automatically add the commands the bot has to be running and online, else you have to register the commands manually. Use the SyncCommands() method in the Bot.cs and enter the name of your server in the event subscription in StartAsync().

---

## Commands

### General Commands

- **`/pull [set-id]`**  
  Pulls a pack containing 9 random Pokémon cards. The cards are randomly chosen from the pool of cards belonging to the set with the given set-id.

- **`/inventory`**  
  Displays your saved Pokémon cards.

- **`/sets`**  
  Shows a list of available Pokémon card sets.

- **`/profile [user]`**  
  Displays the profile and favorite card of the specified user.

### Trading Commands

- **`/trade [user] [your-card] [their-card]`**  
  Initiates a trade session with another user by specifying which card you want to trade and which you want to receive. The cards are referenced by their names. (e.g."Pikachu")

- **`/confirmtrade`**  
  Confirms an ongoing trade.

- **`/canceltrade`**  
  Cancels an active trade session.

### Administrative Commands

- **`/lock [set-id]`**  
  Locks a specific Pokémon card set to prevent cards from being pulled. *(Admin only)*

- **`/unlock [set-id]`**  
  Unlocks a specific set to allow pulls again. *(Admin only)*

- **`/restart`**  
  Restarts the bot. *(Admin only)*

- **`/turnon` / `/turnoff`**  
  Turns the bot on or off, enabling or disabling command processing. *(Admin only)*

- **`/stats`**  
  Displays various statistics about the bot’s usage and performance. *(Admin only)*

For a full list of commands, type `/help` in Discord.

### Help Command

The `HelpCommandHandler` module sends a rich embed listing all available commands and their usage. This module is automatically registered along with other commands during the bot`s startup.

---

## Bot Architecture

### Bot Initialization

The bot is initialized in the `Bot` class, which sets up logging, connects to Discord, and listens for registered slash commands. Commands are executed using the Discord.NET command framework.

### Command Handling

Commands are processed in the `HandleSlashCommandAsync` method, which ensures:
- Commands are handled by their dedicated handlers.
- Only Handles valid commands.
- Errors are logged and reported back in the Discord channel.


---

## Contributing

Contributions are welcome! If you have ideas or improvements:

1. Fork the repository.
2. Create a feature branch: `git checkout -b feature/my-new-feature`
3. Commit your changes: `git commit -am 'Add new feature'`
4. Push your branch: `git push origin feature/my-new-feature`
5. Submit a pull request.

---

## Acknowledgements

- [Pokémon TCG API][api-url] for providing the card data.
- [Discord.NET][discord-dotnet] for the Discord API library.
- The open-source community for support and contributions.

---

Happy collecting!
