using Game.Data;
using GameFramework.Core;
using GameFramework.Events;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    public class GameLobbyManager : Singleton<GameLobbyManager>
    {
        private const string RelayJoinCodeKey = "RelayJoinCode";

        private List<LobbyPlayerData> _lobbyPlayerDatas = new List<LobbyPlayerData>();
        private bool _inGame;

        private LobbyPlayerData _localLobbyPlayerData;

        public bool IsHost => _localLobbyPlayerData.Id == LobbyManager.Instance.GetHostId();

        private void OnEnable()
        {
            LobbyEvents.OnLobbyUpdated += OnLobbyUpdated;
        }

        private void OnDisable()
        {
            LobbyEvents.OnLobbyUpdated -= OnLobbyUpdated;
        }

        public string GetLobbyCode()
        {
            return LobbyManager.Instance.GetLobbyCode();
        }

        public async Task<bool> CreateLobby()
        {
            _localLobbyPlayerData = new LobbyPlayerData();
            _localLobbyPlayerData.Initialize(AuthenticationService.Instance.PlayerId, "HostPlayer");

            bool succeeded = await LobbyManager.Instance.CreateLobby(4, true, _localLobbyPlayerData.Serialize());
            return succeeded;
        }

        public async Task<bool> JoinLobby(string code)
        {
            _localLobbyPlayerData = new LobbyPlayerData();
            _localLobbyPlayerData.Initialize(AuthenticationService.Instance.PlayerId, "JoinPlayer");

            bool succeeded = await LobbyManager.Instance.JoinLobby(code, _localLobbyPlayerData.Serialize());
            return succeeded;
        }

        private async void OnLobbyUpdated(Lobby lobby)
        {
            List<Dictionary<string, PlayerDataObject>> playerData = LobbyManager.Instance.GetPlayersData();
            _lobbyPlayerDatas.Clear();

            int numberOfPlayerReady = 0;
            foreach (Dictionary<string, PlayerDataObject> data in playerData)
            {
                LobbyPlayerData lobbyPlayerData = new LobbyPlayerData();
                lobbyPlayerData.Initialize(data);

                if (lobbyPlayerData.IsReady)
                {
                    numberOfPlayerReady++;
                }

                if (lobbyPlayerData.Id == AuthenticationService.Instance.PlayerId)
                {
                    _localLobbyPlayerData = lobbyPlayerData;
                }

                _lobbyPlayerDatas.Add(lobbyPlayerData);
            }

            Events.LobbyEvents.OnLobbyUpdated?.Invoke();

            if (numberOfPlayerReady == lobby.Players.Count)
            {
                Events.LobbyEvents.OnLobbyReady?.Invoke();
            }

            // Clients start the game as soon as the host has put a Relay join code in the lobby.
            if (!_inGame && !IsHost)
            {
                string joinCode = LobbyManager.Instance.GetLobbyData(RelayJoinCodeKey);

                if (!string.IsNullOrEmpty(joinCode))
                {
                    _inGame = true;
                    _inGame = await JoinGame(joinCode);
                }
            }
        }

        // Host: create a Relay allocation, start hosting, then share the join code through the lobby.
        public async Task<bool> StartGame(string sceneName)
        {
            if (_inGame || !IsHost)
            {
                return false;
            }

            _inGame = true;

            int maxConnections = LobbyManager.Instance.GetMaxPlayers() - 1;
            string joinCode = await RelayManager.Instance.CreateRelay(maxConnections);

            if (string.IsNullOrEmpty(joinCode) || !NetworkManager.Singleton.StartHost())
            {
                _inGame = false;
                return false;
            }

            Dictionary<string, string> lobbyData = new Dictionary<string, string>()
            {
                { RelayJoinCodeKey, joinCode }
            };

            if (!await LobbyManager.Instance.UpdateLobbyData(lobbyData))
            {
                NetworkManager.Singleton.Shutdown();
                _inGame = false;
                return false;
            }

            // Clients follow the host into this scene automatically.
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            return true;
        }

        private async Task<bool> JoinGame(string joinCode)
        {
            if (!await RelayManager.Instance.JoinRelay(joinCode))
            {
                return false;
            }

            bool started = NetworkManager.Singleton.StartClient();

            if (!started)
            {
                Debug.LogError("Could not start the client.");
            }

            return started;
        }

        public List<LobbyPlayerData> GetPlayers()
        {
            return _lobbyPlayerDatas;
        }

        public async Task<bool> SetPlayerReady()
        {
            _localLobbyPlayerData.IsReady = true;
            return await LobbyManager.Instance.UpdatePlayerData(_localLobbyPlayerData.Id, _localLobbyPlayerData.Serialize());
        }
    }
}
