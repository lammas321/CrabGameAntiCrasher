using HarmonyLib;
using SteamworksNative;

namespace AntiCrasher
{
    internal static class SessionVerifier
    {
        internal static void Init()
        {
            Harmony harmony = new($"{MyPluginInfo.PLUGIN_NAME}.{nameof(SessionVerifier)}");
            harmony.PatchAll(typeof(SessionVerifier));
        }

        internal static bool IsValid(ulong clientId)
            => clientId == SteamUser.GetSteamID().m_SteamID || (LobbyTracker.currentMembers.Contains(clientId) && !LobbyTracker.blockedMembers.Contains(clientId));


        // Don't accept P2P networking sessions with users not in your lobby
        [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.NewAcceptP2P), [typeof(CSteamID)])]
        [HarmonyPrefix]
        [HarmonyPriority(int.MaxValue)]
        internal static bool PreSteamManagerNewAcceptP2P(CSteamID param_1)
            => IsValid(param_1.m_SteamID);

        [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.NewAcceptP2P), [typeof(SteamNetworkingIdentity)])]
        [HarmonyPrefix]
        [HarmonyPriority(int.MaxValue)]
        internal static bool PreSteamManagerNewAcceptP2P(SteamNetworkingIdentity param_1)
            => IsValid(param_1.GetSteamID64());
    }
}