# AFK Warden

AFK Warden is a Companion module for Kavros. It watches the game window and warns you when you have gone idle, when hitpoints or prayer run low, and shortly before the inactivity timer would send you to AFK. Version 0.1.0 is a Windows .NET Framework 4.8 application, built and packaged the same way as [Kavros Clue Helper](https://github.com/Zenity-sys/kavros-clue-helper) 0.6.10.

The public module record is [`companion.module.json`](companion.module.json). The Companion panel can read it from this repository's `main` branch. While the program is running it also writes `%LocalAppData%\Kavros\Companion\modules\afk-warden.json` with `Running · v0.1.0`.

The window uses Clue Helper's theme directly: `source/KavrosTheme.cs`, `OpaqueButton`, `KavrosCheckBox`, the Cinzel and Outfit fonts, the crest, the navy panels, the gold primary button, the dark secondary buttons, and the status pills.

## Install

Requires Windows 10 or 11 and .NET Framework 4.8.

A release tag `v0.1.0` (the first three parts of `source/app.manifest`) builds with `source/build.ps1`, packs with Velopack 1.2.161, and publishes:

- `KavrosAfkWarden-win-Setup.exe`
- `KavrosAfkWarden-win-Portable.zip`
- `KavrosAfkWarden-<version>-full.nupkg`
- `releases.win.json`

Close the previous copy with Exit. Run Setup, or extract the whole portable zip into a new folder and run `KavrosAfkWarden.exe` beside its `assets` folder. Do not run a bare copy of the exe with no `assets` folder.

Settings has **Check for updates at startup** and **Check for updates**. Both talk only to this repository's GitHub Releases. A folder copy that was not installed with Setup, and is not the portable package, cannot apply an update by itself.

## What you see

The small panel stays on the desktop while you use another application. It does not hide when Kavros loses focus. Drag the header to move it. **Settings** and **−** are in the header. **−** collapses the panel to the header; **+** opens it again. **Pause** is the gold button. **Exit** is the dark button.

**Settings** is the large window: crest, Cinzel "AFK WARDEN" title, and three cards (Watch, Alerts, Channels). Closing that window returns to the panel.

An alert is an always-on-top popup on the monitor you are using, drawn with the same navy, gold rule, and status pill. It does not take keyboard focus. A Windows toast is also raised when the shell accepts `Kavros.AfkWarden`. Sound is separate from that popup.

## Settings

Saved in `%LocalAppData%\KavrosAfkWarden\settings.txt`. They come back the next time you open the module.

| Setting | Default | What it does |
| --- | --- | --- |
| Idle alert | On, 12 seconds | Warns when the character view and minimap dot have been still for this long. |
| Low hitpoints | On, 30 percent | Warns below this percent, or below an absolute number if Percent is off. |
| Low prayer | On, 20 percent | Same, for prayer. |
| Potion running out | On, 15 seconds | Warns when any buff-bar timer drops below this. Each alert still has its own Sound and Notify boxes. |
| AFK teleport | On, 300 second timer, 30 second lead | Warns this many seconds before the inactivity timer expires. |
| Play sounds | On | Master switch. Also each alert has its own Sound box. Both must be on. |
| Show notifications | On | Master switch for the popup and the toast. Each alert has its own Notify box. Both must be on. |
| Cooldown | 20 seconds | After an alert fires, it will not fire again until the condition has cleared and this long has passed. |
| Stays | 8 seconds | How long the popup stays up. Click it to dismiss it sooner. |
| Keep panel on top | On | The status panel stays above other windows. The alert popup is always on top either way. |
| Pause watching | Off | Freezes every timer and does not alert. |

Test notification and Test sound always run, so you can check the devices. Live alerts obey the switches.

Pin hitpoints or Pin prayer if the automatic search is looking at the wrong number. Drag a rectangle around that readout. Pin buff bar does the same for the row of potion timers. Clear pins returns to the automatic search.

## How the game is read

Kavros does not give this program a hitpoint value, a prayer value, or an inactivity countdown. Clue Helper's rule is the same one used here: read pixels from the window, do not read process memory, and do not send mouse or keyboard input into the game.

Clue Helper captures with `Graphics.CopyFromScreen` on the client rectangle. That call does not need the game to be the foreground window. Its own capture checks say an unobstructed background client is readable. It refuses the frame when the window is minimized, and it refuses the frame when another window covers the sample points, because those screen pixels are no longer the game.

AFK Warden keeps that screen path for a client that is actually on a monitor, focused or not. When Windows Graphics Capture is available it prefers the window's own surface instead, which still updates while the client is behind other windows and while another application has focus. The cursor is not included, and the frame is cropped back to the client area so a pinned rectangle means the same thing on both paths.

A minimized Kavros window is not composed. Neither the screen nor the window surface has a frame. Hitpoints, prayer, and idle cannot be read until you restore it (it can stay behind other windows). The inactivity timer does not need those pixels, so it keeps running and can still notify you. The panel says so while the window is minimized.

Two similar frames are required before a number is trusted. A single bad frame does not alert, and a missing frame is not treated as a recovery.

**Hitpoints and prayer.** The modern HUD is a green life bar and a blue prayer bar along the bottom of the client, with a white or tinted number, often `current/max`. Legacy mode uses the red and blue orbs by the minimap; those are a fill, read as a percent. Percent mode is the default because a Kavros life total can be a few hundred or well into the thousands. An orb percent is not compared with an absolute threshold. If percent mode is on and the maximum was not read, that alert waits.

**Potions.** The buff bar is the row of small timers along the top of the client. The shortest one is watched. It warns when that time drops below the threshold (15 seconds unless you change it). A plain number is seconds, so `65` is 65 seconds and `15` is 15. `1:05`, or `1m 05s` when the letters sit between the two numbers, is one minute and five seconds. A timer written only as `1m` or `15s` is skipped, so the letter is not read as part of the count and a minute is not turned into one second. The search does not look at the hitpoint and prayer numbers along the bottom. If the buff bar is somewhere else, or the text is too small to separate, use Pin buff bar and drag around the timers. No timers means nothing is running out. A missed frame does not clear an alert that already fired.

**Idle.** The character view is the centre of the client, outside the minimap, the top ribbon, and the bottom bar. Movement is either a clear change in that view or the minimap player dot moving. A skilling animation counts as movement. Moving the mouse does not. Time the window cannot be seen is not counted as stillness.

**AFK teleport.** RS3's logout timer is hidden. Public Kavros notes mention an AFK area and do not publish the delay or a countdown on screen. The default full duration is 5 minutes, the RS3 base and the usual private-server value, and it is configurable from 60 to 1800 seconds. The lead time defaults to 30 seconds. The clock starts when Warden first sees the client, because earlier idle time is not visible. It resets when the cursor moves over the game, when a mouse button goes down over the game, or when a key is held while the game is focused. Typing into another application does not reset it. Character animation does not reset it. Pause, or the game closing, freezes it.

## Rebuild

On Windows, with .NET Framework 4.8:

```
powershell -ExecutionPolicy Bypass -File source\build.ps1
KavrosAfkWarden.exe --self-test results.txt
```

The headless test checks the alert gates, the per-alert sound and notification switches, the settings file, idle versus the AFK clock, minimized and covered capture decisions, digit and bar reading, and that `companion.module.json` matches `source/app.manifest`. It does not open a window and it does not capture the desktop.

Tag `v0.1.0` to publish a release. The tag must match the first three parts of the version in `source/app.manifest`.

## What was not verified

This copy was built and logic-tested on Linux. The Windows compiler, Velopack pack, the live WinForms theme, and a running Kavros client were not executed here. The exact AFK-area delay on Kavros was not published in the sources checked, and the HUD anchors can move if the interface scale changes. Pin the number if the automatic search misses it.
