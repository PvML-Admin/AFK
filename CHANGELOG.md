# Changelog

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
