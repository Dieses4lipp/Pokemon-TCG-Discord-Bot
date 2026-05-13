# Pokémon TCG Discord Bot
[![LinkedIn](https://img.shields.io/badge/LinkedIn-Philipp%20Spiekermann-blue?style=flat&logo=linkedin)](https://www.linkedin.com/in/philipp-spiekermann-a01975352/) [![Twitter](https://img.shields.io/badge/Twitter-@DiesesPhilipp-1DA1F2?style=flat&logo=twitter)](https://x.com/DiesesPhilipp)




A feature-rich Discord bot that brings the Pokémon Trading Card Game experience to your server. Pull packs of random Pokémon cards, manage your personal collection, trade with friends, and more - all powered by the official [Pokémon TCG API](https://pokemontcg.io/).

---

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [Configuration](#configuration)
- [Usage](#usage)
- [Commands](#commands)
- [Bot Architecture](#bot-architecture)
- [Contributing](#contributing)
- [License](#license)
- [Acknowledgements](#acknowledgements)

---

## Features

- **Pack Pulling:**  
  Pull a pack of 9 random Pokémon cards using `/pull [set-id]`.
  
- **Collection Management:**  
  View your saved cards with `/inventory` and manage your collection directly in Discord.

- **Set Browsing:**  
  Use `/sets` to see a list of available Pokémon card sets.

- **Trading System:**  
  Initiate trades with other users using `/trade [user] [give-card] [receive-card]` along with commands to confirm (`/confirmtrade`) or cancel (`/canceltrade`) a trade.

- **User Profiles:**  
  Display user profiles with `/profile [user]` to see their collection or trading history.

- **Administrative Commands:**  
  Admins can lock or unlock specific card sets (`/lockset [set-id]` and `/unlockset [set-id]`), restart the bot (`/restart`), or control its active status using `/turnon`/`/turnoff`. Additionally, view bot statistics with `/stats`.

- **Robust Command Handling:**  
  Commands are processed using a dedicated command handler with logging and detailed error reporting.

---

## Installation

### Prerequisites

- [.NET 6.0 SDK or later](https://dotnet.microsoft.com/download)
- A Discord account and a bot token (obtain one via the [Discord Developer Portal](https://discord.com/developers/applications))
- An API key for the [Pokémon TCG API](https://pokemontcg.io/) (optional but recommended for higher rate limits)

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

- **Command Architecture:**  
  The bot listens strictly to Discord Application (Slash) Commands executing directly through integrations instead of the old-school text message prefix parsing mechanism.

- **API Endpoints:**  
  The bot uses endpoints from the Pokémon TCG API to fetch card and set data. Update these in the `CommandHandler` class if necessary.

- **Logging:**  
  Logging is routed to the console to help you monitor bot activity and debug errors.

- **Security Note:**  
  **Never share your bot token publicly!** Always store it securely in your environment variables or configuration files (e.g., the `.env` file). If your token is ever exposed, reset it immediately through the [Discord Developer Portal](https://discord.com/developers/applications).

---

## Usage

Once the bot is running and added to your Discord server, interact with it using the commands listed below. It automatically logs in, connects, registers slash commands globally or server specific, and starts listening for command interactions properly matching typical slash integration logic.

---

## Commands

### General Commands

- **`/pull [set-id] [language? (default: english)]`**  
  Pulls a pack containing 9 random Pokémon cards. If a set ID is provided, only cards from that set are used.

- **`/inventory`**  
  Displays your saved Pokémon cards.

- **`/sets`**  
  Shows a list of available Pokémon card sets.

- **`/profile [user]`**  
  Displays the profile and collection of the specified user.

- **`/help`**  
  Lists all available commands.

### Trading Commands

- **`/trade [user] [give-card] [receive-card]`**  
  Initiates a trade session with another user by specifying which card you want to give and which card you want to receive.

- **`/confirmtrade`**  
  Confirms an ongoing trade.

- **`/canceltrade`**  
  Cancels an active trade session (End the trade session).

### Administrative Commands

- **`/unlockset [set-id]`**  
  Unlocks a specific Pokémon card set to allow pulls again. *(Admin only)*

- **`/lockset [set-id]`**  
  Locks a specific set to prevent cards from being pulled. *(Admin only)*

- **`/restart`**  
  Restarts the bot. *(Admin only)*

- **`/turnon` / `/turnoff`**  
  Turns the bot on or off, enabling or disabling command processing. *(Admin only)*

- **`/stats`**  
  Displays various statistics about the bot’s usage and performance. *(Admin only)*

For a full list of commands, type `/help` in Discord.

### Help Command

The `HelpCommand` module sends a rich embed listing all available commands and their usage. This module is automatically registered along with other commands during the bot`s startup.

---

## Bot Architecture

### Bot Initialization

The bot is initialized in the `Bot` class, which sets up logging, connects to Discord, and listens for slash commands (`/`). Commands are executed using the Discord.NET interaction command framework.

### Command Handling

Commands are processed via slash command interaction routing, which ensures:
- Input parameters and user permissions are automatically validated before execution.
- Interactions are handled by their assigned module handlers.
- Errors are logged and reported back securely and ephemerally in the Discord channel.


---

## Contributing

Contributions are welcome! If you have ideas or improvements:

1. Fork the repository.
2. Create a feature branch: `git checkout -b feature/my-new-feature`
3. Commit your changes: `git commit -am 'Add new feature'`
4. Push your branch: `git push origin feature/my-new-feature`
5. Submit a pull request.

---

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

---

## Acknowledgements

- [Pokémon TCG API](https://pokemontcg.io/) for providing the card data.
- [Discord.NET](https://github.com/discord-net/Discord.Net) for the Discord API library.
- The open-source community for support and contributions.

---

Happy collecting!
