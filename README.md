# CyberScreenSaver

Cyberpunk multi-monitor screensavers for Windows. Retro terminal aesthetic with green-on-black CRT effects, scanlines, and animated displays.

## Screensavers

| Name | Description |
|------|-------------|
| **CyberScreenSaver** | Multi-monitor combo — shows all 3 effects on different screens |
| **CyberOscilloscope** | Multi-channel signal analysis waveforms |
| **CyberClock** | Intrusion detection terminal with hacking logs and system clock |
| **CyberNetwork** | Animated network topology with nodes and data packets |
| **CyberLiveNetwork** | Live network scanner — displays real devices from your LAN |

## Install

1. Download the `.scr` file from [Releases](https://github.com/Antairus/CyberScreenSaver/releases)
2. Copy to `C:\Windows\System32\`
3. Right-click Desktop > Personalize > Lock screen > Screen saver settings
4. Select from the dropdown

Or double-click the `.scr` file to test without installing.

## Uninstall

Run `CyberScreenSaver-Uninstall.exe` — removes all screensavers and cached data.

## Requirements

- Windows 10/11
- Microsoft Edge (pre-installed on all Windows 10/11)
- .NET Framework 4.8 (built into Windows 10/11)

## How It Works

Each `.scr` is a lightweight C# executable (~84KB) that:
1. Extracts embedded HTML/JS screensaver files
2. Launches Microsoft Edge in kiosk mode (fullscreen, no UI)
3. Monitors mouse/keyboard input to exit

The screensaver visuals are HTML5 Canvas + JavaScript — no external dependencies.

**CyberLiveNetwork** additionally runs a built-in network scanner (ARP + ping + NetBIOS) and serves device data to the display via a local HTTP API.

## Build From Source

Compile with the built-in .NET Framework compiler (no SDK needed):

```cmd
csc.exe /target:winexe /out:CyberScreenSaver.exe ^
  /resource:screensaver_oscilloscope.html,screensaver_oscilloscope.html ^
  /resource:screensaver_datarain.html,screensaver_datarain.html ^
  /resource:screensaver_network.html,screensaver_network.html ^
  /resource:screensaver_livenetwork.html,screensaver_livenetwork.html ^
  /reference:System.Web.Extensions.dll ^
  CyberScreenSaver.cs
```

The exe auto-detects which screensaver to show based on its filename. Copy and rename for each variant:
- `CyberScreenSaver.scr` — combo (all 3)
- `CyberOscilloscope.scr` — oscilloscope only
- `CyberClock.scr` — clock/terminal only
- `CyberNetwork.scr` — network topology only
- `CyberLiveNetwork.scr` — live network scanner

## License

MIT
