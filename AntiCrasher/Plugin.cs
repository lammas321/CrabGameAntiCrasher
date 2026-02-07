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
    public class AntiCrasher : BasePlugin
    {
        internal static AntiCrasher Instance;

        public override void Load()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            Instance = this;

            LobbyTracker.Init();
            SessionVerifier.Init();

            Harmony harmony = new(MyPluginInfo.PLUGIN_NAME);
            harmony.PatchAll(typeof(HandlePacketPatches));
            harmony.PatchAll(typeof(ServerHandlePatches));
            harmony.PatchAll(typeof(ClientHandlePatches));
            
            Log.LogInfo($"Initialized [{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION}]");
        }


        internal void Flag(ulong clientId, AntiCrashReason reason, bool banOffender = true)
        {
            Log.LogInfo($"Flagged {SteamFriends.GetFriendPersonaName(new(clientId))} ({clientId}) for: {reason}");

            if (Chatbox.Instance)
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

            // Disabled permanent banning functionality for now, still need to properly test against false flags and that this is effective at catching crashers
            //if (PersistentDataCompatibility.Enabled)
                //PersistentDataCompatibility.SetClientData(clientId, "Banned", $"[AntiCrasher] detected: {reason}");

            LobbyTracker.blockedMembers.Add(clientId);
            LobbyManager.Instance.KickPlayer(clientId); // Will become BanPlayer
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