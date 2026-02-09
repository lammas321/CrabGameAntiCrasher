## What does this do?
- P2P sessions and packets from those who aren't in your current lobby are discarded, avoiding any 24/7 crashers targeting you.
- Packets which would result in invalid game state (NaN, Infinite, or impossible values) are discarded, and those who send them are flagged.
- Packets that are normally unused by the game are discarded (PingPong, ColorChangeRequest, RequestGameStartedCooldown, TryBuyItem, PlayerReload), and those who send them are flagged.
- Any exceptions that occur during the handling of any packet are ignored, as to avoid packets being handled later that frame being lost.

## What does it mean when someone is flagged?
- A log and chat message (optionally disabled via config) is sent to notify you of what was flagged and who triggered it.
- If you're a client, under certain conditions you'll stop future P2P with the crasher. If the crasher is the host, you'll immediately leave the lobby.
  - Certain conditions include if the crasher is directly sending the packets to you.
  - If the crasher sends a crasher packet to the host and that's forwarded to you (because the host doesn't have Anti Crasher), you'll flag and discard. it but won't stop P2P with the host.
- If you're the host, you'll ban them and discard future packets from them until they rejoin.
  - If you have [PersistentData](https://thunderstore.io/c/crab-game/p/lammas123/PersistentData/), you may optionally enable permanently banning those who are flagged via config.