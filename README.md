# Pokémon TCG Discord Bot

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)  
[![LinkedIn](https://img.shields.io/badge/LinkedIn-Philipp%20Spiekermann-blue?style=flat&logo=linkedin)](https://www.linkedin.com/in/philipp-spiekermann-a01975352/)  
[![Twitter](https://img.shields.io/badge/Twitter-@DiesesPhilipp-1DA1F2?style=flat&logo=twitter)](https://x.com/DiesesPhilipp)


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
  Pull a pack of 9 random Pokémon cards using `!pullpack [set ID]`.
  
- **Collection Management:**  
  View your saved cards with `!mycards` and manage your collection directly in Discord.

- **Set Browsing:**  
  Use `!sets` to see a list of available Pokémon card sets.

- **Trading System:**  
  Initiate trades with other users using `!trade [user] [card index]` along with commands to confirm (`!confirmtrade`) or cancel (`!canceltrade`) a trade.

- **User Profiles:**  
  Display user profiles with `!profile [user]` to see their collection or trading history.

- **Administrative Commands:**  
  Admins can lock or unlock specific card sets (`!lock [set ID]` and `!unlock [set ID]`), restart the bot (`!restart`), or control its active status using `!turnon`/`!turnoff`. Additionally, view bot statistics with `!stats`.

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

- **Command Prefix:**  
  The bot listens for commands starting with `!`. You can modify this in the command handling logic if needed.

- **API Endpoints:**  
  The bot uses endpoints from the Pokémon TCG API to fetch card and set data. Update these in the `CommandHandler` class if necessary.

- **Logging:**  
  Logging is routed to the console to help you monitor bot activity and debug errors.

- **Security Note:**  
  **Never share your bot token publicly!** Always store it securely in your environment variables or configuration files (e.g., the `.env` file). If your token is ever exposed, reset it immediately through the [Discord Developer Portal](https://discord.com/developers/applications).

---

## Usage

Once the bot is running and added to your Discord server, interact with it using the commands listed below. It automatically logs in, connects, and starts listening for commands with the `!` prefix.

---

## Commands

### General Commands

- **`!pullpack [set ID]`**  
  Pulls a pack of 9 random Pokémon cards. If a set ID is provided, only cards from that set are used.

- **`!mycards`**  
  Displays your saved Pokémon cards.

- **`!sets`**  
  Shows a list of available Pokémon card sets.

- **`!profile [user]`**  
  Displays the profile and collection of the specified user.

### Trading Commands

- **`!trade [user] [card index]`**  
  Initiates a trade session with another user by specifying which card you want to trade.

- **`!confirmtrade`**  
  Confirms an ongoing trade.

- **`!canceltrade`**  
  Cancels an active trade session.

### Administrative Commands

- **`!unlock [set ID]`**  
  Unlocks a specific Pokémon card set to allow pulls again. *(Admin only)*

- **`!lock [set ID]`**  
  Locks a specific set to prevent cards from being pulled. *(Admin only)*

- **`!restart`**  
  Restarts the bot. *(Admin only)*

- **`!turnon` / `!turnoff`**  
  Turns the bot on or off, enabling or disabling command processing. *(Admin only)*

- **`!stats`**  
  Displays various statistics about the bot’s usage and performance. *(Admin only)*

For a full list of commands, type `!help` in Discord.

### Help Command

The `HelpCommand` module sends a rich embed listing all available commands and their usage. This module is automatically registered along with other commands during the bot`s startup.

---

## Bot Architecture

### Bot Initialization

The bot is initialized in the `Bot` class, which sets up logging, connects to Discord, and listens for messages with the `!` prefix. Commands are executed using the Discord.NET command framework.

### Command Handling

Commands are processed in the `HandleCommandAsync` method, which ensures:
- Messages from other bots are ignored.
- Only messages with the `!` prefix are processed.
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

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

---

## Acknowledgements

- [Pokémon TCG API](https://pokemontcg.io/) for providing the card data.
- [Discord.NET](https://github.com/discord-net/Discord.Net) for the Discord API library.
- The open-source community for support and contributions.

---

Happy collecting!
