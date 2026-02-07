using HarmonyLib;
using SteamworksNative;
using System.Collections.Generic;

namespace AntiCrasher
{
    internal static class LobbyTracker
    {
        internal static HashSet<ulong> currentMembers = [];
        internal static HashSet<ulong> blockedMembers = []; // TODO: Once banning is used, make these blocks permanent
        
        internal static void Init()
        {
            Harmony harmony = new($"{MyPluginInfo.PLUGIN_NAME}.{nameof(LobbyTracker)}");
            harmony.PatchAll(typeof(LobbyTracker));
        }


        // Capture current members on lobby enter
        [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.Method_Private_Void_LobbyEnter_t_PDM_1))]
        [HarmonyPrefix]
        [HarmonyPriority(int.MaxValue)]
        internal static void PreSteamManagerLobbyEnter(LobbyEnter_t param_1)
        {
            currentMembers.Clear();
            blockedMembers.Clear();

            CSteamID lobbyId = new(param_1.m_ulSteamIDLobby);
            int members = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
            currentMembers.EnsureCapacity(members);

            for (int i = 0; i < members; i++)
                currentMembers.Add(SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i).m_SteamID);
        }

        // Track members as they join or leave
        [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.Method_Private_Void_LobbyChatUpdate_t_PDM_3))]
        [HarmonyPrefix]
        [HarmonyPriority(int.MaxValue)]
        internal static void PreSteamManagerPlayerJoinOrLeave(LobbyChatUpdate_t param_1)
        {
            if (param_1.m_rgfChatMemberStateChange == 1u)
                currentMembers.Add(param_1.m_ulSteamIDUserChanged);
            else
            {
                currentMembers.Remove(param_1.m_ulSteamIDUserChanged);
                blockedMembers.Remove(param_1.m_ulSteamIDUserChanged);
            }
        }

        // Clear members when leaving the lobby
        [HarmonyPatch(typeof(SteamMatchmaking), nameof(SteamMatchmaking.LeaveLobby))]
        [HarmonyPrefix]
        [HarmonyPriority(int.MaxValue)]
        internal static void PreSteamMatchmakingLeaveLobby()
        {
            currentMembers.Clear();
            blockedMembers.Clear();
        }
    }
}