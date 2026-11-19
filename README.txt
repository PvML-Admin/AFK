KAVROS AFK WARDEN v0.2.3
=========================

Close the previous copy with Exit. Extract the WHOLE ZIP into a new folder,
then run KavrosAfkWarden.exe beside its assets folder.

Requires Windows 10/11 and .NET Framework 4.8.

The module watches Kavros and warns when you go idle, when hitpoints or
prayer run low, and shortly before the inactivity timer would send you to
AFK. It reads the game window locally and does not send game input.

INSTALL
Run KavrosAfkWarden-win-Setup.exe, or use the portable zip as above.
Settings has "Check for updates at startup" and a Check for updates button.
Both contact only this project's GitHub Releases. A loose folder copy that
was not installed with Setup, and is not the portable package, cannot apply
an update by itself.

THE PANEL
The small panel stays up while you use another application. It does not hide
when Kavros loses focus. Drag the header to move it. Settings and - / + are
in the header. Pause is the gold button. Exit is the dark button.
Settings is the large window. Closing it returns to the panel.

ALERTS
A notification is an always-on-top popup on the monitor you are using. It
does not take keyboard focus. A Windows toast is also raised when Windows
accepts it. Sound is a separate switch.
An alert fires once. It fires again only after the condition has cleared and
the cooldown has elapsed.
Play sounds and Show notifications are the master switches. Each alert also
has its own Sound and Notify box. Both the master and the alert's own box
must be on. Test notification and Test sound always run, so you can check
the devices. Live alerts obey the switches.
Settings are saved in %LocalAppData%\KavrosAfkWarden\settings.txt.

DEFAULTS
Idle: on, 12 seconds without animation or movement.
Hitpoints: on, below 30 percent (turn Percent off for an absolute number).
Prayer: on, below 20 percent.
Potions: on, below 15 seconds left on any buff-bar timer.
AFK teleport: on, 300 second timer, warn 30 seconds before.
Cooldown: 20 seconds. Popup: 8 seconds.
The panel follows the chosen client and hides only while that client is minimized. The alert popup stays on top.

HOW IT READS KAVROS
There is no hitpoint, prayer, or countdown value to query. Pixels only,
no process memory, no game input.
CopyFromScreen reads a client that is actually on a monitor. It does not
need the game to be focused. A minimized window, or a client another
window covers, is not readable, because those screen pixels are not the game.
Windows Graphics Capture is not started unless its yellow system border can
be turned off. This build does not start it, so the game has no capture border.
A minimized window is not composed. Hitpoints, prayer, and idle cannot be
read until you restore it. The AFK timer does not need those pixels, so it
keeps running and can still notify you.
A number is used after two similar frames. A missing frame is not a recovery.
Pin hitpoints or Pin prayer if the automatic search is looking at the wrong
readout. Pin buff bar if the potion timers are not along the top. Clear
pins returns to the automatic search.
A buff timer is a plain number of seconds (65 means 65 seconds, not 1:05)
or minutes and seconds with a colon (1:05). "1m 05s" is read when the
letters sit between the two numbers. "1m" or "15s" alone is skipped.
Modern HUD: green life bar and blue prayer bar along the bottom, usually
current/max. Legacy orbs by the minimap are a fill, read as a percent.
Percent mode is the default because life totals vary so widely. An orb
percent is not compared with an absolute threshold.

IDLE
The centre of the client, outside the minimap and the bars. A skilling
animation counts as movement. Moving the mouse does not. Time the window
cannot be seen is not counted as stillness.

AFK TIMER
The client does not draw this countdown. RS3's hidden logout base is 5
minutes, and public Kavros notes do not publish the AFK-area delay. The
default is 300 seconds, configurable from 60 to 1800. The clock starts when
Warden first sees the client. It resets when the cursor moves over the game,
when a mouse button goes down over the game, or when a key is held while the
game is focused. Typing in another app does not reset it. Animation does not
reset it. Pause, or the game closing, freezes it.

REBUILD
powershell -ExecutionPolicy Bypass -File source\build.ps1
KavrosAfkWarden.exe --self-test results.txt
Tag v0.1.0, matching source\app.manifest, to publish.
See THIRD_PARTY.txt for fonts, Velopack, and branding.
