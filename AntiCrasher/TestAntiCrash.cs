//#define Test
//#define TestWeaponSpawn

#if Test || TestWeaponSpawn
using CrabDevKit.Intermediary;
using SteamworksNative;
using System;
using UnityEngine;
#endif

namespace AntiCrasher
{
    internal static class TestAntiCrash
    {
        internal static void Test()
        {
#if Test
            // TODO
            // Limit handling to those not in GameManager.activePlayers or GameManager.spectators
            // Called every second on loading screen once scene is ready, limit how often this is handled, no flagging?
            // ClientSend.LoadingRequestEnterGame();

            // Limit handling to those not in GameManager.activePlayers or GameManager.spectators
            // Called once on scene load, track times called and scenes loaded since first time called for each
            // ClientSend.RequestFreezeTime();
            // ClientSend.GameModeLoaded();

            // Called every 5 seconds if the player hasn't spawned locally, limit how often these are handled, no flagging?
            // ClientSend.GameRequestToSpawn(false);
            // ClientSend.GameRequestAllPlayers();

            // Limit to valid ItemManager items or -1
            // ClientSend.SendActiveItem(999999999);

            // Limit to GameManager.activePlayers, don't flag
            // ClientSend.SendSpectating(9999999999999999999UL);



            // InvalidPacketLength
            Packet packet = new(0);
            SteamPacketManager.SendPacket(SteamUser.GetSteamID(), packet, 8, SteamPacketManager_NetworkChannel.ToClient);
            SteamPacketManager.SendPacket(SteamUser.GetSteamID(), packet, 8, SteamPacketManager_NetworkChannel.ToServer);
            packet.Dispose();

            // InvalidClientPacketType
            packet = new(-1);
            packet.WriteLength();
            SteamPacketManager.SendPacket(SteamUser.GetSteamID(), packet, 8, SteamPacketManager_NetworkChannel.ToServer);
            packet.Dispose();

            // InvalidServerPacketType
            packet = new(-1);
            packet.WriteLength();
            SteamPacketManager.SendPacket(SteamUser.GetSteamID(), packet, 8, SteamPacketManager_NetworkChannel.ToClient);
            packet.Dispose();

            if (!SteamManager.Instance.IsLobbyOwner())
            {
                // UnauthorizedServerPacketFromNonHost
                packet = new((int)ServerPackets.sendSerializedInventory);
                packet.WriteLength();
                SteamPacketManager.SendPacket(SteamUser.GetSteamID(), packet, 8, SteamPacketManager_NetworkChannel.ToClient);
                packet.Dispose();
            }

            if (SteamManager.Instance.IsLobbyOwner())
            {
                // UnusedPingPongPacket
                ClientSend.PingPong();

                // UnusedColorChangeRequestPacket
                ClientSend.RequestColor(0);

                // UnusedRequestGameStartedCooldownPacket
                ClientSend.RequestGameStartedCooldown();

                // UnusedTryBuyItemPacket
                ClientSend.TryBuyItem(0);

                // UnusedPlayerReloadPacket
                ClientSend.PlayerReload(1f);

                // UnusedPlayerReloadPacketFromHost
                packet = new((int)ServerPackets.playerReload);
                packet.Write(SteamUser.GetSteamID().m_SteamID);
                packet.Write(1f);
                packet.WriteLength();
                SteamPacketManager.SendPacket(SteamUser.GetSteamID(), packet, 8, SteamPacketManager_NetworkChannel.ToClient);
                packet.Dispose();
            }

            // InvalidPlayerPositionPacket
            ClientSend.PlayerPosition(new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity), SteamUser.GetSteamID().m_SteamID);

            // InvalidPlayerRotationPacket
            ClientSend.PlayerRotation(float.PositiveInfinity, float.PositiveInfinity, SteamUser.GetSteamID().m_SteamID);

            if (SteamManager.Instance.IsLobbyOwner())
            {
                // InvalidPlayerAnimationPacket
                ClientSend.PlayerAnimation(-1, true);

                // InvalidCrabDamagePacket
                ClientSend.DamageCrab(-1, -1);
                ClientSend.DamageCrab(0, 0);

                // InvalidPlayerDamagePacket
                ClientSend.DamagePlayer(SteamUser.GetSteamID().m_SteamID, 0, new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity), 0, 0);

                // InvalidPlayerPositionPacketFromHost
                packet = new((int)ServerPackets.playerPosition);
                packet.Write(SteamUser.GetSteamID().m_SteamID);
                packet.Write(new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity));
                packet.WriteLength();
                SteamPacketManager.SendPacket(SteamUser.GetSteamID(), packet, 8, SteamPacketManager_NetworkChannel.ToClient);
                packet.Dispose();

                // InvalidPlayerRotationPacketFromHost
                packet = new((int)ServerPackets.playerRotation);
                packet.Write(SteamUser.GetSteamID().m_SteamID);
                packet.Write(float.PositiveInfinity);
                packet.Write(float.PositiveInfinity);
                packet.WriteLength();
                SteamPacketManager.SendPacket(SteamUser.GetSteamID(), packet, 8, SteamPacketManager_NetworkChannel.ToClient);
                packet.Dispose();

                // InvalidPlayerAnimationPacketFromHost
                packet = new((int)ServerPackets.playerAnimation);
                packet.Write(SteamUser.GetSteamID().m_SteamID);
                packet.Write(-1);
                packet.Write(true);
                packet.WriteLength();
                SteamPacketManager.SendPacket(SteamUser.GetSteamID(), packet, 8, SteamPacketManager_NetworkChannel.ToClient);
                packet.Dispose();

                // InvalidPlayerDamagePacketFromHost
                packet = new((int)ServerPackets.damagePlayer);
                packet.Write(SteamUser.GetSteamID().m_SteamID);
                packet.Write(SteamUser.GetSteamID().m_SteamID);
                packet.Write(0);
                packet.Write(new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity));
                packet.Write(0);
                packet.WriteLength();
                SteamPacketManager.SendPacket(SteamUser.GetSteamID(), packet, 8, SteamPacketManager_NetworkChannel.ToClient);
                packet.Dispose();
            }
#endif

#if TestWeaponSpawn
            try
            {
                bool flag = SteamManager.Instance == null;
                if (!flag)
                {
                    ulong steamID = SteamManager.Instance.field_Private_CSteamID_0.m_SteamID;
                    int num = 4;
                    int num2 = 2;
                    for (int i = 0; i < 1; i++)
                    {
                        AntiCrasher.Instance.Log.LogInfo("Spawning weapon!");
                        ClientSend.SendActiveItem(num);
                        ClientSend.TryDropItem(num, num2, 0);
                        ClientSend.TryInteract(num2);
                        ClientSend.TryInteract(num2);
                        bool isInfinite = true;
                        if (isInfinite)
                        {
                            ClientSend.TryDropItem(num, num2, 0);
                        }
                        bool shouldPickUp = true;
                        if (shouldPickUp)
                        {
                            ClientSend.TryInteract(num2);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AntiCrasher.Instance.Log.LogError($"An error occured testing WeaponSpawner:\n{ex}");
            }
#endif
        }
    }
}