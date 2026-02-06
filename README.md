# CrabGameAntiCrasher
A BepInEx mod for Crab Game that acts to remedy the effects of crashers.

### Dependencies
- [CrabDevKit](https://thunderstore.io/c/crab-game/p/lammas123/CrabDevKit/)
- [PersistentData](https://thunderstore.io/c/crab-game/p/lammas123/PersistentData/)

## What does this do?
- P2P sessions and packets from those who aren't in your current lobby are discarded, avoiding 24/7 crashers.
- Invalid packets which would result in exceptions and legitimate packets being missed are discarded, and those who send them are flagged.
- Exceptions that occur during the handling of any packet are ignored, as to not lead to packets being handled later that frame being ignored.
- Unused packets are discarded and those who use them are flagged, a sign of random packet spam crashers. (PingPong, ColorChangeRequest, RequestGameStartedCooldown, TryBuyItem)
- Packets containing NaN, Infinite, or otherwise absurd/impossible float/Vector3 values for position, rotation, and damage direction, which could lead to an unplayable state are discarded and those who send them are flagged, often the result of invalid knockback crashers.

## What does it mean when someone is flagged?
- A log and chat message is sent to notify you of what was flagged and who triggered it.
- If you're the host, you'll kick and discard future packets from them until they rejoin. Later it'll become bans (and permanent PersistentData bans) but I need to ensure there's no false flags first.
