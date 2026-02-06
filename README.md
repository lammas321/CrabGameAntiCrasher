# CrabGameAntiCrasher
A BepInEx mod for Crab Game that acts to remedy the effects of crashers.

### Dependencies
- [CrabDevKit](https://thunderstore.io/c/crab-game/p/lammas123/CrabDevKit/)
- [PersistentData](https://thunderstore.io/c/crab-game/p/lammas123/PersistentData/)

## What does this defend against?
- Discards P2P sessions and packets from those who aren't in your current lobby, avoiding 24/7 crashers.
- Discards invalid packets, which would result in exceptions and legitimate packets being missed.
- Discards unused packets and flags those who use them, a sign of random packet spam crashers. (PingPong, ColorChangeRequest, RequestGameStartedCooldown, TryBuyItem)
- Discards and flags packets containing NaN, Infinite, or otherwise absurd/impossible float/Vector3 values for position, rotation, and damage direction, which could lead to an unplayable state, often the result of NaN knockback crashers.
