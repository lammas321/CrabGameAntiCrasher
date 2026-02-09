using BepInEx;
using BepInEx.IL2CPP;
using HarmonyLib;
using SteamworksNative;
using System.Globalization;

namespace AntiCrasher
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency("lammas123.CrabDevKit")]
    [BepInDependency("lammas123.PersistentData", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class AntiCrasher : BasePlugin
    {
        internal static AntiCrasher Instance;

        internal bool chatFlags = true;
        internal bool persistentDataBan = false;
        internal bool packetLogging = false;

        public override void Load()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            Instance = this;

            chatFlags = Config.Bind("AntiCrasher", "ChatFlags", true, "Whether to notify you about the Anti Crasher being flagged in the chatbox or not. You'll still be able to check for flags in the console or LogOutput.txt.").Value;
            persistentDataBan = Config.Bind("AntiCrasher", "PersistentDataBans", false, "If flagged crashers should be permanently banned via PersistentData if it's installed.").Value;
            packetLogging = Config.Bind("AntiCrasher", "PacketLogging", false, "If all received packets should be logged to PacketLog.txt at the root of your BepInEx directory.").Value;

            LobbyTracker.Init();
            SessionVerifier.Init();
            if (packetLogging)
                PacketLogger.Init();

            Harmony harmony = new(MyPluginInfo.PLUGIN_NAME);
            harmony.PatchAll(typeof(HandlePacketPatches));
            harmony.PatchAll(typeof(ServerHandlePatches));
            harmony.PatchAll(typeof(ClientHandlePatches));
            
            Log.LogInfo($"Initialized [{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION}]");
        }


        internal void Flag(ulong clientId, AntiCrashReason reason, bool banOffender = true)
        {
            Log.LogInfo($"Flagged {SteamFriends.GetFriendPersonaName(new(clientId))} ({clientId}) for: {reason}");

            if (chatFlags && Chatbox.Instance)
                Chatbox.Instance.AppendMessage(0ul, reason.ToString(), $"AntiCrasher Flagged {SteamFriends.GetFriendPersonaName(new(clientId))} for");

            if (!banOffender || !SteamManager.Instance.IsLobbyOwner() || clientId == SteamUser.GetSteamID().m_SteamID)
            {
                if (banOffender && clientId != SteamUser.GetSteamID().m_SteamID)
                {
                    if (clientId == SteamManager.Instance.originalLobbyOwnerId.m_SteamID)
                    {
                        SteamManager.Instance.LeaveLobby();
                        Prompt.Instance.NewPrompt("Anti Crasher", $"The host was flagged for {reason}, left lobby.");
                        return;
                    }

                    LobbyTracker.blockedMembers.Add(clientId);
                    SteamManager.Instance.StopP2P(new(clientId));
                }

                return;
            }

            
            if (persistentDataBan && PersistentDataCompatibility.Enabled)
                PersistentDataCompatibility.SetClientData(clientId, "Banned", $"[AntiCrasher] detected: {reason}");

            LobbyTracker.blockedMembers.Add(clientId);
            LobbyManager.Instance.BanPlayer(clientId);
        }
    }

    internal enum AntiCrashReason
    {
        InvalidPacketLength,
        InvalidClientPacketType,
        InvalidServerPacketType,

        UnusedPingPongPacket,
        UnusedColorChangeRequestPacket,
        UnusedRequestGameStartedCooldownPacket,
        UnusedTryBuyItemPacket,
        UnusedPlayerReloadPacket,

        UnusedPlayerReloadPacketFromHost,
        
        InvalidPlayerPositionPacket,
        InvalidPlayerRotationPacket,
        InvalidPlayerAnimationPacket,
        InvalidCrabDamagePacket,
        InvalidPlayerDamagePacket,
        
        InvalidPlayerPositionPacketFromHost,
        InvalidPlayerRotationPacketFromHost,
        InvalidPlayerAnimationPacketFromHost,
        InvalidPlayerDamagePacketFromHost
    }
}