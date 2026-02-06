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
            if (!banOffender || !SteamManager.Instance.IsLobbyOwner() || clientId == SteamUser.GetSteamID().m_SteamID || LobbyManager.bannedPlayers.Contains(clientId))
            {
                Log.LogInfo($"Flagged {SteamFriends.GetFriendPersonaName(new(clientId))} ({clientId}) for: {reason}");

                if (Chatbox.Instance)
                    Chatbox.Instance.AppendMessage(0ul, $"Flagged {SteamFriends.GetFriendPersonaName(new(clientId))} ({clientId}) for: {reason}", "AntiCrasher");

                return;
            }

            Log.LogInfo($"Banned {SteamFriends.GetFriendPersonaName(new(clientId))} ({clientId}) for: {reason}");

            if (Chatbox.Instance)
                Chatbox.Instance.AppendMessage(0ul, $"Banned {SteamFriends.GetFriendPersonaName(new(clientId))} ({clientId}) for: {reason}", "AntiCrasher");

            // Disabled banning functionality for now, still need to properly test against false flags and that this is effective at catching crashers

            //if (PersistentDataCompatibility.Enabled)
                //PersistentDataCompatibility.SetClientData(clientId, "Banned", $"[AntiCrasher] detected: {reason}");

            LobbyTracker.blockedMembers.Add(clientId);
            SteamManager.Instance.StopP2P(new(clientId));
            LobbyManager.Instance.KickPlayer(clientId);
        }
    }
    
    internal enum AntiCrashReason
    {
        InvalidPacketLength,
        InvalidClientPacketType,
        InvalidServerPacketType,

        UnusedPingPongPacket,
        UnusedTryBuyItemPacket,
        UnusedRequestGameStartedCooldownPacket,
        UnusedColorChangeRequestPacket,

        InvalidPlayerPositionPacket,
        InvalidPlayerRotationPacket,
        InvalidCrabDamagePacket,
        InvalidPlayerDamagePacket,

        InvalidPlayerPositionPacketFromHost,
        InvalidPlayerRotationPacketFromHost,
        InvalidPlayerDamagePacketFromHost
    }
}