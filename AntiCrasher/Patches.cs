using BepInEx.IL2CPP.Utils;
using CrabDevKit.Intermediary;
using HarmonyLib;
using Il2CppSystem.Runtime.InteropServices;
using SteamworksNative;
using System;
using System.Collections;
using System.Collections.Generic;
using UnhollowerBaseLib;
using UnityEngine;

namespace AntiCrasher
{
    internal static class HandlePacketPatches
    {
        internal static Coroutine keybindCoro;

        internal static IEnumerator CoroKeybinds()
        {
            while (true)
            {
                yield return null;

                if (!Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))
                    continue;

                if (Input.GetKeyDown(KeyCode.T))
                {
                    //TestAntiCrash.Test();

                    AntiCrasher.Instance.Log.LogInfo("Tested AntiCrasher");
                    
                    if (Chatbox.Instance)
                        Chatbox.Instance.AppendMessage(0ul, "Functional", "AntiCrasher");
                }
            }
        }

        [HarmonyPatch(typeof(MainManager), nameof(MainManager.Awake))]
        [HarmonyPostfix]
        internal static void PostMainManagerAwake()
        {
            keybindCoro ??= MainManager.Instance.StartCoroutine(CoroKeybinds());
        }


        private const int MIN_PACKET_SIZE = 8;


        // Ensure all packets are handled in the frame they're received, beyond the 70 message limit
        private static int _maxHandledPackets = 0;
        private static int _totalHandledPackets = 0;
        [HarmonyPatch(typeof(SteamPacketManager), nameof(SteamPacketManager.Update))]
        [HarmonyPostfix]
        internal static void PostSteamPacketManagerUpdate()
        {
            while (_maxHandledPackets == SteamPacketManagerExtensions.get_deobf_messagesToCheckFor())
            {
                AntiCrasher.Instance.Log.LogInfo("Handled 70 packets! Checking for more...");

                _maxHandledPackets = 0;
                SteamPacketManager.CheckForPackets();
            }
            _totalHandledPackets = 0;
        }

        [HarmonyPatch(typeof(SteamNetworkingMessages), nameof(SteamNetworkingMessages.ReceiveMessagesOnChannel))]
        [HarmonyPostfix]
        internal static void PostSteamPacketManagerUpdate(int __result, int nLocalChannel)
        {
            if ((nLocalChannel == (int)SteamPacketManager_NetworkChannel.ToClient || nLocalChannel == (int)SteamPacketManager_NetworkChannel.ToServer) && __result > _maxHandledPackets)
                _maxHandledPackets = __result;
        }

        // Check for invalid packets
        [HarmonyPatch(typeof(SteamPacketManager), nameof(SteamPacketManager.Method_Private_Static_Void_SteamNetworkingMessage_t_Int32_0))]
        [HarmonyPrefix]
        [HarmonyPriority(int.MinValue)]
        internal static bool PreSteamPacketManagerHandlePacket(SteamNetworkingMessage_t param_0, int param_1)
        {
            _totalHandledPackets++;

            ulong clientId = param_0.m_identityPeer.GetSteamID64();
            if (!SessionVerifier.IsValid(clientId)) // Discard packet and stop P2P with sender, receiving a packet from someone not in the same lobby
            {
                SteamManager.Instance.StopP2P(new(clientId));
                if (AntiCrasher.Instance.packetLogging)
                    PacketLogger.EnqueuePacket(clientId, -1, _totalHandledPackets, []);
                return false;
            }

            int size = param_0.m_cbSize;
            if (size < MIN_PACKET_SIZE) // Discard short packet, will always throw an exception when the game tries to handle it
            {
                AntiCrasher.Instance.Flag(clientId, AntiCrashReason.InvalidPacketLength);
                if (AntiCrasher.Instance.packetLogging)
                    PacketLogger.EnqueuePacket(clientId, -2, _totalHandledPackets, []);
                return false;
            }

            Il2CppStructArray<byte> data = new(size);
            Marshal.Copy(param_0.m_pData, data, 0, size);

            Packet packet = new();
            packet.SetBytes(data);

            packet.ReadInt(true); // Packet length, discard
            int type = packet.ReadInt(true);

            if (AntiCrasher.Instance.packetLogging)
                PacketLogger.EnqueuePacket(clientId, param_1, _totalHandledPackets, data);

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
                    case ClientPackets.playerReload: AntiCrasher.Instance.Flag(clientId, AntiCrashReason.UnusedPlayerReloadPacket); return false;
                }
            }
            else
            {
                if (!Enum.IsDefined(typeof(ServerPackets), type))
                {
                    AntiCrasher.Instance.Flag(clientId, AntiCrashReason.InvalidServerPacketType);
                    return false;
                }

                if (clientId != SteamMatchmaking.GetLobbyOwner(SteamManager.Instance.currentLobby).m_SteamID)
                {
                    if ((ServerPackets)type == ServerPackets.sendSerializedInventory || (ServerPackets)type == ServerPackets.sendSerializedDrop)
                        AntiCrasher.Instance.Flag(clientId, AntiCrashReason.UnauthorizedServerPacketFromNonHost);
                    return false;
                }

                switch ((ServerPackets)type)
                {
                    case ServerPackets.playerReload:
                    {
                        if (size < MIN_PACKET_SIZE + sizeof(ulong))
                            return false;

                        ulong otherClientId = packet.ReadUlong(true);
                        AntiCrasher.Instance.Flag(otherClientId, AntiCrashReason.UnusedPlayerReloadPacketFromHost, banOffender: false);
                        return false;
                    }
                }
            }

            return true;
        }

        // Exceptions thrown in HandlePacket are not caught by the base game, and leads to packets that would have been handled later in the same frame being lost
        // Here we catch the exception, but continue handling packets as though no error occured
        [HarmonyPatch(typeof(SteamPacketManager), nameof(SteamPacketManager.Method_Private_Static_Void_SteamNetworkingMessage_t_Int32_0))]
        [HarmonyFinalizer]
        internal static Exception FinalSteamPacketManagerHandlePacket(SteamNetworkingMessage_t param_0, int param_1, Exception __exception)
        {
            if (__exception != null)
                AntiCrasher.Instance.Log.LogError($"An exception occurred handling a {(SteamPacketManager_NetworkChannel)param_1} packet from {SteamFriends.GetFriendPersonaName(param_0.m_identityPeer.GetSteamID())} ({param_0.m_identityPeer.GetSteamID64()}):\n{__exception}");

            return null;
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
                AntiCrasher.Instance.Flag(param_0, AntiCrashReason.InvalidPlayerPositionPacket);
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

        // Don't allow invalid animations
        [HarmonyPatch(typeof(ServerHandle), nameof(ServerHandle.PlayerAnimation))]
        [HarmonyPrefix]
        [HarmonyPriority(int.MaxValue)]
        internal static bool PreServerHandlePlayerAnimation(ulong param_0, Packet param_1)
        {
            int initialReadPos = param_1.get_readPos();

            int playerAnimation = param_1.ReadInt(true);

            if (!Enum.IsDefined(typeof(OnlinePlayerMovement_PlayerAnimation), playerAnimation))
            {
                AntiCrasher.Instance.Flag(param_0, AntiCrashReason.InvalidPlayerAnimationPacket);
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

            ItemData item = ItemManager.GetItemById(itemId);
            if (
                item == null ||
                item.itemName != "Snowball" ||
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
                ItemPrefab itemPrefab = shared.GetComponent<ItemPrefab>();
                if (itemPrefab != null && itemPrefab.itemData.itemID != itemId)
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

        // Don't allow players with invalid rotations
        [HarmonyPatch(typeof(ClientHandle), nameof(ClientHandle.PlayerAnimation))]
        [HarmonyPrefix]
        [HarmonyPriority(int.MaxValue)]
        internal static bool PreClientHandlePlayerAnimation(Packet param_0)
        {
            int initialReadPos = param_0.get_readPos();

            ulong clientId = param_0.ReadUlong(true);
            int playerAnimation = param_0.ReadInt(true);

            if (!Enum.IsDefined(typeof(OnlinePlayerMovement_PlayerAnimation), playerAnimation))
            {
                AntiCrasher.Instance.Flag(clientId, AntiCrashReason.InvalidPlayerAnimationPacketFromHost, banOffender: false);
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


    internal static class ChatboxPatches
    {
        // Don't allow force message to make the messages string really long
        [HarmonyPatch(typeof(Chatbox), nameof(Chatbox.ForceMessage))]
        [HarmonyPostfix]
        internal static void PostChatboxForceMessage(Chatbox __instance)
        {
            if (__instance.messages.text.Length > __instance.get_maxChars())
                __instance.messages.text = __instance.messages.text[^(__instance.get_purgeAmount())..];
	    }
    }


    internal static class PlayerChatDropPatches
    {
        // Don't do rich text on player names in player chat drops
        private static bool _shouldNotParse = false;

        [HarmonyPatch(typeof(Deobf_SteamInventory), nameof(Deobf_SteamInventory.PlayerChatDrop))]
        [HarmonyPrefix]
        internal static void PreDeobf_SteamInventoryPlayerChatDrop()
        {
            _shouldNotParse = true;
        }
        [HarmonyPatch(typeof(Deobf_SteamInventory), nameof(Deobf_SteamInventory.PlayerChatDrop))]
        [HarmonyPostfix]
        internal static void PostDeobf_SteamInventoryPlayerChatDrop()
        {
            _shouldNotParse = false;
        }

        [HarmonyPatch(typeof(SteamFriends), nameof(SteamFriends.GetFriendPersonaName))]
        [HarmonyPostfix]
        internal static void PostSteamFriendsGetFriendPersonaName(ref string __result)
        {
            if (_shouldNotParse)
                __result = $"<noparse>{__result.Replace("noparse", "")}</noparse>";
        }


        // Don't accept player chat drops from players that just recently got a drop
        private static readonly HashSet<ulong> _recentDrops = [];

        // Don't accept player chat drops from players with potential rich text in their names
        [HarmonyPatch(typeof(ServerHandle), nameof(ServerHandle.PlayerDropSerialized))]
        [HarmonyPrefix]
        [HarmonyPriority(int.MaxValue)]
        internal static bool PreServerHandlePlayerDropSerialized(ulong param_0)
        {
            if (_recentDrops.Contains(param_0))
                return false;

            _recentDrops.Add(param_0);
            MainManager.Instance.StartCoroutine(CoroChatDropCooldown(param_0));

            string name = SteamFriends.GetFriendPersonaName(new(param_0));
            int start = name.IndexOf('<');
            if (start == -1 || start + 1 >= name.Length)
                return true;

            int end = name.IndexOf(">", start + 1);
            return end == -1;
        }

        private static IEnumerator CoroChatDropCooldown(ulong clientId)
        {
            yield return new WaitForSeconds(1f);
            _recentDrops.Remove(clientId);
        }
    }


    internal static class AntiShadowModPatches
    {
        // Forces players hiding with ShadowMod (or having just recently joined) to be listed in the player list
        [HarmonyPatch(typeof(PlayerList), nameof(PlayerList.UpdateList))]
        [HarmonyPostfix]
        internal static void PostPlayerListUpdateList(PlayerList __instance)
        {
            if (!SteamManager.Instance.IsLobbyOwner())
                return;

            foreach (Client client in LobbyManager.Instance.GetClients())
            {
                if (client.field_Public_CSteamID_0 == CSteamID.Nil || __instance.field_Private_Dictionary_2_UInt64_MonoBehaviourPublicRabaicRaTeusscTepiObUnique_0.ContainsKey(client.field_Public_CSteamID_0.m_SteamID))
                    continue;

                PlayerListingPrefab playerListPlayer = UnityEngine.Object.Instantiate(__instance.namePrefab, __instance.contentParent).GetComponent<PlayerListingPrefab>();
                __instance.field_Private_Dictionary_2_UInt64_MonoBehaviourPublicRabaicRaTeusscTepiObUnique_0.Add(client.field_Public_CSteamID_0.m_SteamID, playerListPlayer);

                playerListPlayer.username.text = Chatbox.RemoveRichText(SteamFriends.GetFriendPersonaName(client.field_Public_CSteamID_0));
                if (GameManager.Instance.activePlayers.ContainsKey(client.field_Public_CSteamID_0.m_SteamID))
                    playerListPlayer.SetPlayer(GameManager.Instance.activePlayers[client.field_Public_CSteamID_0.m_SteamID]);
                else
                    playerListPlayer.SetSpectator(client.field_Public_CSteamID_0.m_SteamID);

                playerListPlayer.background.color = Color.yellow;
                playerListPlayer.icon.color = Color.yellow;
            }
        }
    }
}