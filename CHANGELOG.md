# Changelog

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
