# Changelog

## 0.1.0

- Idle, low hitpoints, low prayer, and AFK-teleport warnings.
- Each alert has its own on/off switch, threshold, sound switch, and notification switch. Global sound and notification switches sit on top of those.
- Settings are saved under `%LocalAppData%\KavrosAfkWarden\settings.txt`.
- Watching continues while Kavros is visible but not focused, and while it is covered, using the window surface when Windows can provide it.
- Alerts are a sound (optional) and an always-on-top popup on the desktop, plus a Windows toast when the shell accepts one. They are not drawn inside the game.
