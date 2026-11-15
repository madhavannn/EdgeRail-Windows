# EdgeRail for Windows

A standalone Windows utility inspired by Padrail's edge-control concept. This is an independent implementation, not an official Padrail release or a port of its proprietary app. No Padrail code or artwork is included.

## Start

1. Extract the ZIP into a folder.
2. Open **EdgeRail.exe**. No installer or administrator rights are required.
3. Use **Automatic** mode or choose **Compatibility** mode for predictable pointer-based operation.
4. Change the four rail mappings in the settings window. Changes save automatically.

Requires Windows 10/11 with .NET Framework 4.8 or newer. The executable is managed AnyCPU; x86/x64 are intended targets. ARM64 behavior has not been tested. This development build is unsigned.

## Two ways to use it

**Native touchpad edges (hardware dependent):** Place exactly one finger on a physical touchpad edge, then slide along that edge. Native input uses Windows Raw Input and the HID parser, without a vendor driver. The status line shows whether compatible coordinates actually arrive. Registration alone does not mean the touchpad is supported. Some drivers hide this data; some report layouts cannot be decoded safely. Native gestures are not guaranteed on every laptop. The app observes input; it does not suppress the driver's normal pointer movement or scrolling.

**Compatibility mode (touchpad or mouse):** Hold **Alt**, move the pointer to within about 12 pixels of a screen edge, then slide the pointer along it. Release Alt to finish. This uses the **screen boundary**, not the physical touchpad boundary. For multiple monitors, use an outer screen edge; shared edges are easily crossed. Moving away from the active rail requires releasing Alt to rearm. Mouse button presses cancel a compatibility gesture.

In Automatic mode, native gestures and compatibility gestures are both available. While Alt is held, native gestures are suspended to avoid duplicate actions. There is no automatic claim of hardware support based on device names. Pausing disables both live input modes.

The illustrated pad in settings is a **preview only**. Click near an edge and drag along it to test gesture recognition without changing volume, brightness or other apps.

## Default mappings

| Edge | Action | Positive direction |
| --- | --- | --- |
| Left | Display brightness | Up |
| Right | System volume | Up |
| Top | Adaptive | Right |
| Bottom | Previous / next media track | Right = next |

Available mappings: Off, Volume, Brightness, Tabs, Zoom, Tracks, Desktops, UndoRedo, Adaptive.

- Volume uses Windows media keys; step size follows Windows.
- Brightness uses Windows WMI monitor controls in 5-point steps and changes supported active internal displays. Most external monitors do not expose this interface. Unsupported displays show a message, without pretending brightness changed.
- Tabs uses Ctrl+Tab / Ctrl+Shift+Tab.
- Zoom uses Ctrl+Plus / Ctrl+Minus; handling depends on the foreground app.
- Tracks uses next / previous media keys.
- Desktops uses Ctrl+Win+Right / Ctrl+Win+Left.
- UndoRedo uses Ctrl+Y / Ctrl+Z; some apps use different redo shortcuts.
- Adaptive selects tabs for common browsers, tracks for common media apps, zoom for selected design/PDF/photo apps, and virtual desktops elsewhere. These defaults are built in; per-app editing is not included in this version.
- Keyboard backlight control is not included: Windows does not provide a universal vendor-independent control.

Shortcuts go to the foreground application and can be blocked by Windows when an elevated app is in front. Standard permission level is recommended. The app temporarily releases and restores held modifiers when injecting an action. Corners, multi-finger contacts and contacts that begin in the center are excluded from native rail activation. Reports that cannot confirm total contact count are rejected, so some hybrid HID layouts fall back to compatibility mode.

## Controls and settings

- **Ctrl+Alt+P:** Pause/resume.
- **Ctrl+Alt+O:** Open settings.
- **Tray icon:** Open settings, pause/resume, or quit.
- Closing settings leaves EdgeRail running in the tray. Choose **Quit EdgeRail** to stop it.
- Edge width controls the physical native edge band. Travel per step sets the distance needed for each action (a lower value is more sensitive). Compatibility activation stays close to the screen border.
- No automatic startup is installed. No networking, accounts, analytics, updater, service, or keyboard logging is used.
- Configuration: `%LOCALAPPDATA%\EdgeRail\settings.xml`.
- To uninstall, quit the tray app and delete its extracted folder. Optionally remove that settings folder.

## Build and verification

All source is in `source/`. No package downloads are needed. Double-click `source\build.cmd` to rebuild without changing PowerShell execution settings. Alternatively, where PowerShell scripts are permitted:

```powershell
powershell -NoProfile -File .\source\build.ps1
```

The output is `source\dist\EdgeRail.exe` and its config file. Compilation uses the Windows .NET Framework compiler.

```powershell
$exe = (Resolve-Path .\EdgeRail.exe).Path
$test = Start-Process -FilePath $exe -ArgumentList '--self-test', 'results.txt' -PassThru -Wait
$test.ExitCode
Get-Content .\results.txt
Start-Process -FilePath $exe -ArgumentList '--smoke-test' -Wait
```

Self-tests do not change system volume/brightness or send shortcuts. They cover activation, direction, jitter, cancellation, multi-finger rejection, settings serialization, adaptive routing, and Win32 input layout. Smoke testing creates the settings window without registering input listeners, a tray icon or hotkeys. Physical native touchpad behavior requires testing on each device; automated tests cannot establish universal hardware support.

## API references

- https://learn.microsoft.com/en-us/windows/win32/inputdev/using-raw-input
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawinputdevice
- https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/hidpi/nf-hidpi-hidp_getvaluecaps
- https://learn.microsoft.com/en-us/windows/win32/input-precisiontouchpad/precision-touchpad-portal

## License

This original EdgeRail implementation is provided under the MIT license in LICENSE.txt. Padrail is a separate project owned by its authors.
