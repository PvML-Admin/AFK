# Changelog

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
