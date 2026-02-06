using CrabDevKit.Intermediary;
using HarmonyLib;
using Il2CppSystem.Runtime.InteropServices;
using SteamworksNative;
using System;
using UnhollowerBaseLib;
using UnityEngine;

namespace AntiCrasher
{
    internal static class HandlePacketPatches
    {
        [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.Awake))]
        [HarmonyPostfix]
        internal static void PostSteamManagerAwake()
        {
            TMPro.TMP_Settings.instance.m_warningsDisabled = true;
        }



        private const int MIN_PACKET_SIZE = 8;

        // Check for invalid packets
        [HarmonyPatch(typeof(SteamPacketManager), nameof(SteamPacketManager.Method_Private_Static_Void_SteamNetworkingMessage_t_Int32_0))]
        [HarmonyPrefix]
        [HarmonyPriority(int.MinValue)]
        internal static bool PreSteamPacketManagerHandlePacket(SteamNetworkingMessage_t param_0, int param_1)
        {
            ulong clientId = param_0.m_identityPeer.GetSteamID64();
            if (!SessionVerifier.IsValid(clientId)) // Discard packet and stop P2P with sender, receiving a packet from someone not in the same lobby
            {
                SteamManager.Instance.StopP2P(new(clientId));
                return false;
            }

            int size = param_0.m_cbSize;
            if (size < MIN_PACKET_SIZE) // Discard short packet, will always throw an exception when the game tries to handle it
            {
                AntiCrasher.Instance.Flag(clientId, AntiCrashReason.InvalidPacketLength);
                return false;
            }

            Il2CppStructArray<byte> data = new(size);
            Marshal.Copy(param_0.m_pData, data, 0, size);

            Packet packet = new();
            packet.SetBytes(data);

            packet.ReadInt(true); // Packet length, discard
            int type = packet.ReadInt(true);


            // Flag invalid packet types
            if ((SteamPacketManager_NetworkChannel)param_1 == SteamPacketManager_NetworkChannel.ToServer)
            {
                if (!Enum.IsDefined(typeof(ClientPackets), type))
                {
                    AntiCrasher.Instance.Flag(clientId, AntiCrashReason.InvalidClientPacketType);
                    return false;
                }

                switch ((ClientPackets)type)
                {
                    case ClientPackets.pingPong: AntiCrasher.Instance.Flag(clientId, AntiCrashReason.UnusedPingPongPacket); return false;
                    case ClientPackets.lobbyVisualsChangeColor: AntiCrasher.Instance.Flag(clientId, AntiCrashReason.UnusedColorChangeRequestPacket); return false;
                    case ClientPackets.gameStartedCooldown: AntiCrasher.Instance.Flag(clientId, AntiCrashReason.UnusedRequestGameStartedCooldownPacket); return false;
                    case ClientPackets.buyItem: AntiCrasher.Instance.Flag(clientId, AntiCrashReason.UnusedTryBuyItemPacket); return false;
                }
            }
            else if (!Enum.IsDefined(typeof(ServerPackets), type))
            {
                AntiCrasher.Instance.Flag(clientId, AntiCrashReason.InvalidServerPacketType);
                return false;
            }

            return true;
        }

        // Exceptions thrown in HandlePacket are not caught by the base game, and leads to packets that would have been handled later in the same frame being lost
        // Here we catch the exception, but continue handling packets as though no error occured
        [HarmonyPatch(typeof(SteamPacketManager), nameof(SteamPacketManager.Method_Private_Static_Void_SteamNetworkingMessage_t_Int32_0))]
        [HarmonyFinalizer]
        internal static void FinalSteamPacketManagerHandlePacket(SteamNetworkingMessage_t param_0, int param_1, Exception __exception)
        {
            if (__exception != null)
                AntiCrasher.Instance.Log.LogError($"An exception occurred handling a {(SteamPacketManager_NetworkChannel)param_1} packet from {SteamFriends.GetFriendPersonaName(param_0.m_identityPeer.GetSteamID())} ({param_0.m_identityPeer.GetSteamID64()}):\n{__exception}");
        }
    }


    
    internal static class InvalidHelpers
    {
        internal static bool IsInvalid(this float value)
            => float.IsNaN(value) || float.IsInfinity(value) || value == float.MaxValue || value == float.MinValue;

        internal static bool IsInvalid(this Vector3 value)
            => value.x.IsInvalid() || value.y.IsInvalid() || value.z.IsInvalid();
    }


    internal static class ServerHandlePatches
    {
        // Don't allow players at invalid coordinates
        [HarmonyPatch(typeof(ServerHandle), nameof(ServerHandle.PlayerPosition))]
        [HarmonyPrefix]
        [HarmonyPriority(int.MaxValue)]
        internal static bool PreServerHandlePlayerPosition(ulong param_0, Packet param_1)
        {
            int initialReadPos = param_1.get_readPos();

            Vector3 playerPosition = param_1.ReadVector3(true);

            if (playerPosition.IsInvalid())
            {
                AntiCrasher.Instance.Flag(param_0, AntiCrashReason.InvalidPlayerPositionPacket, banOffender: false);
                GameServer.Instance.QueueRespawn(param_0, 0f);
                param_1.set_readPos(initialReadPos);
                return false;
            }

            param_1.set_readPos(initialReadPos);
            return true;
        }

        // Don't allow players with invalid rotations
        [HarmonyPatch(typeof(ServerHandle), nameof(ServerHandle.PlayerRotation))]
        [HarmonyPrefix]
        [HarmonyPriority(int.MaxValue)]
        internal static bool PreServerHandlePlayerRotation(ulong param_0, Packet param_1)
        {
            int initialReadPos = param_1.get_readPos();

            float playerRotationY = param_1.ReadFloat(true);
            float playerRotationX = param_1.ReadFloat(true);

            if (playerRotationX.IsInvalid() || playerRotationY.IsInvalid())
            {
                AntiCrasher.Instance.Flag(param_0, AntiCrashReason.InvalidPlayerRotationPacket);
                param_1.set_readPos(initialReadPos);
                return false;
            }

            param_1.set_readPos(initialReadPos);
            return true;
        }

        // Don't allow damaging Tantan illegally
        [HarmonyPatch(typeof(ServerHandle), nameof(ServerHandle.CrabDamage))]
        [HarmonyPrefix]
        [HarmonyPriority(int.MaxValue)]
        internal static bool PreServerHandleCrabDamage(ulong param_0, Packet param_1)
        {
            int initialReadPos = param_1.get_readPos();

            int itemId = param_1.ReadInt(true);
            int uniqueId = param_1.ReadInt(true);
            
            if (
                ItemManager.GetItemById(itemId) == null ||
                ItemManager.GetItemById(itemId).itemName != "Snowball" ||
                !SharedObjectManager.Instance.Contains(uniqueId)
            )
            {
                AntiCrasher.Instance.Flag(param_0, AntiCrashReason.InvalidCrabDamagePacket);
                param_1.set_readPos(initialReadPos);
                return false;
            }

            SharedObject shared = SharedObjectManager.Instance.GetSharedObject(uniqueId);
            if (shared != null)
            {
                ItemPrefab item = shared.GetComponent<ItemPrefab>();
                if (item != null && item.itemData.itemID != itemId)
                {
                    AntiCrasher.Instance.Flag(param_0, AntiCrashReason.InvalidCrabDamagePacket);
                    param_1.set_readPos(initialReadPos);
                    return false;
                }
            }

            param_1.set_readPos(initialReadPos);
            return true;
        }

        // Don't allow invalid direction vector3
        [HarmonyPatch(typeof(ServerHandle), nameof(ServerHandle.PlayerDamage))]
        [HarmonyPrefix]
        [HarmonyPriority(int.MaxValue)]
        internal static bool PreServerHandlePlayerDamage(ulong param_0, Packet param_1)
        {
            int initialReadPos = param_1.get_readPos();

            param_1.ReadUlong(true); // Other client, discard
            param_1.ReadInt(true); // Damage, discard
            Vector3 direction = param_1.ReadVector3(true);

            if (direction.IsInvalid() || (direction != Vector3.zero && (direction.magnitude > 1.0001f || direction.magnitude < 0.9999f)))
            {
                AntiCrasher.Instance.Flag(param_0, AntiCrashReason.InvalidPlayerDamagePacket);
                param_1.set_readPos(initialReadPos);
                return false;
            }

            param_1.set_readPos(initialReadPos);
            return true;
        }
    }


    internal static class ClientHandlePatches
    {
        // Don't allow players at invalid coordinates
        [HarmonyPatch(typeof(ClientHandle), nameof(ClientHandle.PlayerPosition))]
        [HarmonyPrefix]
        [HarmonyPriority(int.MaxValue)]
        internal static bool PreClientHandlePlayerPosition(Packet param_0)
        {
            int initialReadPos = param_0.get_readPos();

            ulong clientId = param_0.ReadUlong(true);
            Vector3 playerPosition = param_0.ReadVector3(true);

            if (playerPosition.IsInvalid())
            {
                AntiCrasher.Instance.Flag(clientId, AntiCrashReason.InvalidPlayerPositionPacketFromHost, banOffender: false);
                param_0.set_readPos(initialReadPos);
                return false;
            }

            param_0.set_readPos(initialReadPos);
            return true;
        }

        // Don't allow players with invalid rotations
        [HarmonyPatch(typeof(ClientHandle), nameof(ClientHandle.PlayerRotation))]
        [HarmonyPrefix]
        [HarmonyPriority(int.MaxValue)]
        internal static bool PreClientHandlePlayerRotation(Packet param_0)
        {
            int initialReadPos = param_0.get_readPos();

            ulong clientId = param_0.ReadUlong(true);
            float playerRotationY = param_0.ReadFloat(true);
            float playerRotationX = param_0.ReadFloat(true);

            if (playerRotationX.IsInvalid() || playerRotationY.IsInvalid())
            {
                AntiCrasher.Instance.Flag(clientId, AntiCrashReason.InvalidPlayerRotationPacketFromHost, banOffender: false);
                param_0.set_readPos(initialReadPos);
                return false;
            }

            param_0.set_readPos(initialReadPos);
            return true;
        }

        // Don't allow invalid direction vector3
        [HarmonyPatch(typeof(ClientHandle), nameof(ClientHandle.PlayerDamage))]
        [HarmonyPrefix]
        [HarmonyPriority(int.MaxValue)]
        internal static bool PreClientHandlePlayerDamage(Packet param_0)
        {
            int initialReadPos = param_0.get_readPos();

            ulong attackerClientId = param_0.ReadUlong(true);
            param_0.ReadUlong(true); // hurtClientId, discard
            param_0.ReadInt(true); // itemId, discard
            Vector3 direction = param_0.ReadVector3(true);

            if (direction.IsInvalid() || (direction != Vector3.zero && (direction.magnitude > 1.0001f || direction.magnitude < 0.9999f)))
            {
                AntiCrasher.Instance.Flag(attackerClientId, AntiCrashReason.InvalidPlayerDamagePacketFromHost, banOffender: false);
                param_0.set_readPos(initialReadPos);
                return !direction.IsInvalid();
            }

            param_0.set_readPos(initialReadPos);
            return true;
        }
    }
}