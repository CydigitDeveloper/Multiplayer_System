# Multiplayer Lobby System

A lobby-to-game flow built on Unity Gaming Services: sign in, host a private lobby, share the code, let friends join, and start a Netcode for GameObjects session over Relay once everyone is ready.

I use it as the starting point for my multiplayer projects.

<!-- Add a short GIF of the lobby flow here: ![Lobby flow](docs/lobby.gif) -->

## Flow

`Init` → `MainMenu` → `Lobby`

1. **Init** starts Unity Services, signs the player in anonymously and loads the main menu.
2. **MainMenu**: *Host* creates a private lobby (up to 4 players). *Join* asks for the lobby code.
3. **Lobby** shows the players who have joined. When every player has marked themselves ready, `OnLobbyReady` fires and the host gets a *Start* button.
4. **Start**: the host creates a Relay allocation, starts hosting with Netcode for GameObjects and writes the Relay join code into the lobby data. Clients see the code on their next lobby refresh, join the Relay allocation and connect as clients. The host then loads the game scene and everyone follows.

## Structure

The code is split into two layers so the lobby logic can be reused in other games.

```
Assets/Scripts/
├── GameFramework/          reusable, knows nothing about this game
│   ├── Core/Singleton.cs
│   ├── Manager/LobbyManager.cs     talks to the Lobby service
│   ├── Manager/RelayManager.cs     Relay allocation + UnityTransport setup
│   └── Events/LobbyEvents.cs       OnLobbyUpdated(Lobby)
└── Game/                   this game's side
    ├── GameLobbyManager.cs         players, ready state, starting/joining the game
    ├── Data/LobbyPlayerData.cs     player data <-> PlayerDataObject
    ├── Events/LobbyEvents.cs       OnLobbyUpdated, OnLobbyReady
    ├── LobbySpawner.cs, LobbyPlayer.cs, LobbyUI.cs
    ├── MainMenuController.cs
    └── Init.cs
```

The game layer never calls the Lobby service directly. It goes through `LobbyManager` and listens to events.

## Details

- The host sends a **heartbeat every 6 seconds** so the lobby stays alive.
- Clients **poll every second** but only raise `OnLobbyUpdated` when `LastUpdated` is newer than the copy they already have, so the UI does not redraw for nothing.
- Player data (id, name, ready flag) is serialized into the lobby's `PlayerDataObject` dictionary and read back into `LobbyPlayerData`.
- The Relay join code travels through the lobby's own data (`RelayJoinCode`), so players never type a second code.
- The host starts hosting before it publishes the join code, so clients never try to connect to a host that isn't listening yet.
- If there is no `NetworkManager` in the scene, `RelayManager` creates one with a `UnityTransport` at runtime.

## Running it

1. Open the project with **Unity 2022.3.50f1**.
2. Link it to a Unity Cloud project (Edit > Project Settings > Services) and enable **Lobby**, **Relay** and **Authentication** in the dashboard.
3. Set the scene to load after *Start* on `LobbyUI` (`Game Scene Name`, default `Loading`). The scene must be in Build Settings.
4. Open `Assets/Scenes/Init.unity` and press Play.
5. To test joining, run a build next to the editor. Host in one, join with the code in the other, ready up and press Start.

## Tech

Unity 2022.3 · Unity Lobby 1.2 · Unity Authentication · Netcode for GameObjects 1.11 · Unity Relay 1.0 · URP 14
