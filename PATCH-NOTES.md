# AFK Warden v0.2.6

- AFK teleport uses the Kavros rates. Normal is 25 minutes. Grandmaster adds 25 minutes and the tree perk adds 15. The two switches are independent and saved. Old 300-second settings migrate to the normal rate. The warning lead is still adjustable.
- Capture stops while paused. Missing HUD locks retry on a delay, stable layouts use small screen regions, and full searches skip pixels that cannot contain a bar. The capture worker reuses its pixel buffers.
- The HP and prayer readers isolate the number line above each bar and retain manually pinned regions. Prayer can update while draining continuously. The bright-background screenshot reads 1,633 / 1,060 HP and 1,040 / 990 prayer.
- Potion recognition groups adjacent buff icons and reads white timer text against bright scenery. A brief missed timer shows its last reading for up to four seconds. An estimated value cannot trigger an alert.
- Opening Warden raises the game-owned overlay. Opening it again signals the running copy. Settings remain accessible when the game is minimized.

