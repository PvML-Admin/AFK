# Changelog

## 0.2.17

- Coloured chat lines match. Red, orange and green game messages, including the six potion warnings on a red chat, map to the right pot and state. The strip along the top of the window is not treated as chat. The debug log includes the text read from each line.
- An empty capture frame keeps the last hitpoints, prayer and other readings. Minimized is shown only when the client really is minimized.
- The RuneMetrics total stays put. A jump or a drop is ignored until the same new total is read again several times.
- AFK Warden binds to the Kavros client under the Companion overlay that toggled it, including a client hwnd or pid passed in by Companion. It leaves the Companion overlay and Clue Helper windows alone, stays on that client, and says the client is closed instead of moving to another one.
- The client picker shows the character name Companion writes for that client's process. With no name, the clients are labelled Client 1, Client 2 and Client 3. The name is a label only.
- Each potion shows Active, with a countdown only where the duration is known, Low in orange, or Expired in red. The short names stay.

## 0.2.16

- Chat potion lines have to match the whole phrase. Ordinary chat, news and trivia no longer raise a potion alert. Lines already on screen when you attach are ignored, and each new line pings at most once. An expired potion stays quiet until a drink or a warning re-arms it.
- The debug log lists each chat line, the phrase it matched, the score, and whether it alerted, was already seen, was already on screen, or did not match. It also records the RuneMetrics total and why a read was missed.
- "Keep RuneMetrics XP open" stays hidden until the tracker has been missing for 5 seconds.
- RuneMetrics is found from the XP / XP/h / ETA header row, including a panel that is all zeros and one that also shows Damage. The potion line uses short labels so it fits the panel.

## 0.2.15

- Potion alerts are read from the chat box. Elder overload, overload, aggression, prayer renewal, antifire and antipoison each keep their own state: active, running low, or expired. A line already on screen does not alert again when the chat scrolls. The buff bar no longer raises potion alerts.
- Idle follows the RuneMetrics total XP. Keep that window open. An increase resets idle for skilling and for combat, along with hitpoints changes and clicks or keys.
- A covered or unfocused client is read with Windows Graphics Capture. A minimized client shows Minimized and pauses the reads. PrintWindow is not used.

## 0.2.14

- Live potion timers are read from the buff icons themselves. A full-size bar of four flasks at 4 minutes, a client with 6m, 5m, 6m and 6m, and another account's four flasks at 11 minutes all keep those times. Prayer and a stack count stay off the potion clock.
- Each confirmed XP drop resets idle. The same number still floating in the same spot for about a second is not a second drop. The backpack grid, once found, stays put when a later frame misses it.

## 0.2.13

- The buff bar is found by its row of timers, wherever it sits. A still client shot scaled back up reads four potions at 5 minutes. Pasting the same buff bar in another corner still reads.
- An orange plus in the scenery is not an XP drop. A drop needs the xp letters, and the search follows it instead of locking onto the trees. The same drop is still found after it is moved.
- The backpack grid is searched across the whole client and stays put once it repeats. Windows scale stays on 100, 150 or 200 after two agreeing reads.
- Boosted hitpoints such as 1471/990 stay legal.

## 0.2.12

- A potion is recognised from the flask itself: Elder is the dark teal flask, Aggro the purple flask, and Overload the green flask with a dark rim. The first clean 1–60 minute or second read is kept. A later read can correct it, and one odd reading does not replace a timer that is already counting. An empty buff frame leaves the clocks alone for a minute. The panel lists each potion with the time it has left.
- The backpack is the lower-right grid of slot borders, including when the window cuts off the bottom rows, at 100%, 150% and 200%.
- Orange XP drops stay visible while they float and fade. The hitpoint and prayer bars keep the last place they were found. Idle counts clicks, keys, XP drops and backpack changes, and the seconds keep moving on the panel.

## 0.2.11

- Buff times, hitpoints and prayer are matched against glyph stamps cut from the live captures, at 100%, 150% and 200%. A thin "1m" is not kept when the rest of the timer is still readable.
- The orange "+N xp" drop is found from a plus stamp confirmed by the xp letters. A backpack change counts only when the slots form a real grid.
- Enlarging a capture by 1.5 or 2 samples one source pixel, so a 12 stays a 12. Hitpoints 1633/1060, prayer 1040/990, the 12 minute flasks and the 11 minute Elder row read at 100%, 150% and 200%.

## 0.2.10

- Each potion keeps its own clock, matched by how the icon looks rather than where it sits, because buffs move. A reading is kept only after it agrees twice. A shorter reading replaces a longer one only when it repeats. A missed timer keeps counting for about 30 seconds. Prayer, stack counts, and a red ring with no flask or crystal are ignored. The panel shows the soonest confirmed potion by name.
- Woodcutting XP is the orange "+N xp" text above the player, not a hash of the scenery. Scene noise no longer holds the Skilling pill at 0 seconds. A backpack change is a slot filling or emptying.

## 0.2.9

- The whole screen no longer flashes black. The periodic capture never calls PrintWindow. A visible client is a small screen copy, focused or not. A covered client is left unread, and hitpoints, prayer and idle stay on the last good values. A black frame cannot raise an alert. The panel's topmost state changes only when that state changes.

## 0.2.8

- The game is read while it is behind other windows. A visible slot is still a small screen copy. A covered slot is taken from the window itself, and a black result falls back to the screen.
- Potion times keep minutes and seconds apart. The buff strip widens when the icons move, and one missed read does not drop a timer that is already counting down.
- XP drops and backpack changes count as skilling without a mode switch. Idle waits through the skilling gap, including while other motion is on screen, then warns when skilling stops.

## 0.2.7

- Life and prayer stay readable when the coloured fill is too short to find. Implausible values are dropped, and the panel moves when two of three reads agree.
- Potion timers keep counting down between reads. A missed frame does not jump the timer.
- Whole-client copies back off when the HUD cannot be read. Life and prayer share one grab, and slow slots are copied once a second.
- The panel stays above the game while the game is in front, without taking focus.
- Skilling idle uses XP drops, backpack changes, and clicks.

## 0.2.6

- AFK teleport uses the Kavros rates. Normal is 25 minutes. Grandmaster adds 25 minutes and the tree perk adds 15. The two switches are independent and saved. Old 300-second settings migrate to the normal rate. The warning lead is still adjustable.
- Capture stops while paused. Missing HUD locks retry on a delay, stable layouts use small screen regions, and full searches skip pixels that cannot contain a bar. The capture worker reuses its pixel buffers.
- The HP and prayer readers isolate the number line above each bar and retain manually pinned regions. Prayer can update while draining continuously. The bright-background screenshot reads 1,633 / 1,060 HP and 1,040 / 990 prayer.
- Potion recognition groups adjacent buff icons and reads white timer text against bright scenery. A brief missed timer shows its last reading for up to four seconds. An estimated value cannot trigger an alert.
- Opening Warden raises the game-owned overlay. Opening it again signals the running copy. Settings remain accessible when the game is minimized.

## 0.2.5

- The window opens on the first click. The panel shows Starting immediately. Client search, settings, the update check, and the first full capture run in the background. A second launch brings the existing window forward, or replaces it if it is stuck. Closing exits the process.
- Capture follows the small locked slots (life, prayer, buff strip, XP) about twice a second. A full client search runs at startup, when a lock is lost, and about every 12 seconds, off the UI thread. Debug images are saved only when that option is on, at most every 30 seconds, as a smaller JPEG.
- Life and prayer accept a short current value such as 917/1,060 or 9/990. The panel updates when two of the last three reads agree, and it does not show the maximum as the current value. A read with no new sample for five seconds is marked stale.
- Idle ignores mouse movement. Clicks and keys count only while the game is in front. An XP drop or a change in the backpack also counts. The reason is written to the debug log when that log is on.
- Potion time comes from potion icons (flasks, overload, antifire-style), not from a prayer buff or a stack count.

## 0.2.4

- Life and prayer numbers are read from the full bar slot, not the shrinking coloured fill. A good slot is kept, so a short fill on a later frame does not drop the number.
- A read with an unrecognised glyph is discarded. When the maximum has three or more digits, a shorter current value is discarded too, so a clipped `9/990` cannot replace `936/990`. Prayer is still shown only after three consistent reads.
- Buff icons are searched anywhere, including a detached bar, and their borders and timers tolerate a smaller icon. Minute timers and plain numbers of 10 or more show immediately. Each icon's timer text is written to the debug log.
- Idle resets only for input while the game window is in front, a change in life, an adrenaline increase, a target bar that actually changes width, or a hitsplat. Prayer drain, swaying trees, and other players do not count. The reason is written to the debug log. Pausing watching shows Paused on the panel.

## 0.2.3

- The window appears before the client list and the update check. Those run off the UI thread. Capture pauses while a window is dragged, and Settings uses the window manager for the drag.
- Life numbers are read again when the heart icon sits in the box. Prayer is shown only after three consistent reads, so a single bad frame does not jump the value.
- Idle ignores swaying trees, water and other people. It resets on input to the foreground client, a change in life, prayer or adrenaline, a target bar, a hitsplat, or a compact XP counter.
- The panel header is one bar. Settings and the collapse button sit on that bar, and the gold line runs the full width.
- Each read is appended to `%LocalAppData%\KavrosAfkWarden\debug-log.txt`. A full frame with the detected rectangles is saved as `debug-capture.png` in the same folder.

## 0.2.2

- Life is the red bar and prayer is the purple bar, wherever the HUD strip sits. Yellow adrenaline and green summoning are not life or prayer. Current may be higher than the cap, with thousands separators and no fixed upper limit.
- Buff timers are read from green-bordered icons anywhere on the client. `10m` is minutes. A plain number such as `60` is seconds.
- Combat and skilling count as activity. A target health bar, hitsplat or HUD value change, an XP or inventory change, or motion around the player resets the idle timer.
- Settings number boxes, labels, the AFK sound switch, and the client list use the navy and gold theme. The watch pill says GAME DETECTED. Header buttons are flat. Pinning freezes one capture while the rectangle is dragged.

## 0.2.1

- The installer splash says Kavros AFK Warden.
- The panel stays with the chosen client when you tab out and hides only while that client is minimized. Dragging the header uses the window manager.
- Capture no longer flushes the desktop compositor or scans the whole client on every tick. Windows Graphics Capture is not started unless its yellow border can be turned off, so the game has no capture border.
- Settings can save a debug capture of the frame and the detected regions under %LocalAppData%\KavrosAfkWarden.

## 0.2.0

- Life and prayer are read from the movable HUD strip: the red bar is life points and the purple bar is prayer, including comma separators and a current value above the maximum.
- Buff icons with a green border are read anywhere. `11m` is eleven minutes. A plain number alerts only after that icon has shown minutes or counted down, so a stack count does not warn.
- The status panel follows the chosen Kavros window and hides when that window is minimized or another application is in front. A green GAME DETECTED pill names the client. One client is watched, chosen from the list or by clicking it.
- Settings number boxes are text fields. A volume slider covers alert sounds. Settings stays open when another application is focused. Update checks repeat every three hours while startup checks are on.

## 0.1.0

- Idle, low hitpoints, low prayer, potion running out, and AFK-teleport warnings.
- Potion running out watches the buff-bar timers and warns below 15 seconds, with its own sound and notification switches. Pin buff bar if the timers are not along the top.
- Each alert has its own on/off switch, threshold, sound switch, and notification switch. Global sound and notification switches sit on top of those.
- Settings are saved under `%LocalAppData%\KavrosAfkWarden\settings.txt`.
- Watching continues while Kavros is visible but not focused, and while it is covered, using the window surface when Windows can provide it.
- Alerts are a sound (optional) and an always-on-top popup on the desktop, plus a Windows toast when the shell accepts one. They are not drawn inside the game.
