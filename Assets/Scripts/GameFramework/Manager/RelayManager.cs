using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace GameFramework.Core
{
    public class RelayManager : Singleton<RelayManager>
    {
        private string _joinCode;

        public string JoinCode => _joinCode;

        // Host side: reserve a Relay slot for the other players and get a join code to share.
        public async Task<string> CreateRelay(int maxConnections)
        {
            Allocation allocation;

            try
            {
                allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
                _joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            }
            catch (RelayServiceException e)
            {
                Debug.LogError($"Relay allocation failed: {e.Message}");
                return null;
            }

            GetTransport().SetRelayServerData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData);

            Debug.Log($"Relay allocation created, join code {_joinCode}");
            return _joinCode;
        }

        // Client side: join the host's allocation with the code from the lobby.
        public async Task<bool> JoinRelay(string joinCode)
        {
            JoinAllocation allocation;

            try
            {
                allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            }
            catch (RelayServiceException e)
            {
                Debug.LogError($"Joining Relay failed: {e.Message}");
                return false;
            }

            _joinCode = joinCode;

            GetTransport().SetRelayServerData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData,
                allocation.HostConnectionData);

            return true;
        }

        // Uses the NetworkManager in the scene if there is one, otherwise creates one at runtime.
        private UnityTransport GetTransport()
        {
            NetworkManager networkManager = NetworkManager.Singleton;

            if (networkManager == null)
            {
                GameObject go = new GameObject("NetworkManager");
                go.SetActive(false);

                UnityTransport transport = go.AddComponent<UnityTransport>();
                networkManager = go.AddComponent<NetworkManager>();
                networkManager.NetworkConfig = new NetworkConfig
                {
                    NetworkTransport = transport
                };

                go.SetActive(true);
            }

            return (UnityTransport)networkManager.NetworkConfig.NetworkTransport;
        }
    }
}
