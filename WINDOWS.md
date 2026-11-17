# NetPeep 2.0 for Windows

The Windows release uses a small native .NET Framework host and the system's Microsoft Edge WebView2 runtime. It does **not** bundle Electron or an entire browser. The shared HTML/CSS/JavaScript interface also runs in the Electron/macOS source build.

## Install or run

1. Quit an older NetPeep version from its tray menu first.
2. Run `NetPeep-2.0.0-Windows-x64-Setup.exe`. It installs for your account without administrator rights and includes an uninstaller. Desktop shortcut and start-at-sign-in are optional installer choices.
3. Alternatively extract the entire portable ZIP into a folder and run `NetPeep.exe`. Keep the DLLs, `src/`, and `assets/` beside it.

Requirements: Windows 10/11 x64, .NET Framework 4.6.2 or newer, and Microsoft Edge WebView2 Evergreen Runtime. These are normally present on current Windows installations. The installer deliberately does not include the much larger WebView2 runtime. If missing, the app explains how to obtain it from Microsoft's official website; the native taskbar monitor can still run. The dashboard needs that runtime. This reduces the app download, not the size of Microsoft's separately maintained shared runtime or its per-user browser cache.

This build is unsigned. Signing credentials are not included. Test and sign before public distribution.

## Controls

- **Overview:** live download/upload and today/week/month/all-time totals. Weeks start Monday. LAN traffic is included and history is recorded only while the app runs.
- **Apps:** observed process activity. Per-app Windows byte counts are partial IPv4 TCP samples and require an explicit administrator launch. No automatic elevation. UDP/QUIC, IPv6 and short-lived connections are excluded. Dashes mean unavailable, not zero.
- **Settings → Units:** Auto, B, KB, MB, GB. Applies to live rates, adapter totals, app rates, the tray tooltip and the transparent taskbar meter. Units use decimal bytes: 1 KB = 1,000 B. Changing display units never changes stored byte totals.
- **Settings → Theme:** System, Light, Dark. Saved locally. The native title bar follows the selected app theme.
- **Taskbar:** two transparent rows, upload above download, colored arrows. Unlock from the tray menu or Settings, drag, then pin with the checkmark or tray command. Pinned readings are click-through. Position is saved. Only the primary taskbar is targeted and no shell space is reserved; choose an empty section when crowded.
- **About:** version, developer credit, email/Facebook links, optional PayPal donation and bKash number copy. Payments are not processed in the app and no link opens until clicked.
- **Closing the dashboard** keeps monitoring active. Quit from the tray icon.

Developer: MD SOWROVE HOSSAIN. The developer's contact/donation details were recovered from the saved profile supplied by the user; they are not invented placeholders.

## Data and safety

Native data lives at `%APPDATA%\NetPeep\usage.json`, preserving the earlier version-1 daily-history schema. Unit/theme/pin settings are in the same file. Writes occur every five samples and on normal quit using atomic replacement. A `.last-good` backup is maintained; invalid history is copied to a timestamped `.corrupt-...` file. Sudden termination can lose the last few seconds. Export before manually resetting the file.

WebView2's profile/cache is separate at `%LOCALAPPDATA%\NetPeep\WebView2`. It is not bundled into the app or included in history exports. The UI has no remote assets or network data service. A local virtual-host mapping serves packaged files, navigation and popup windows are blocked, CSP disallows external connections, and native actions are restricted to named commands. Donation/contact links are a fixed allowlist. No captured packet payload, remote address, or hostname is stored.

## Build from source

Use Windows PowerShell with the .NET Framework C# compiler already installed on Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\icons.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File native\build.ps1
```

The build downloads the pinned official `Microsoft.Web.WebView2` SDK 1.0.4314.35 from NuGet if it is not cached. It uses only the net462 Core/WinForms assemblies and x64 loader; no .NET SDK or Node.js is required for the native Windows build. Dependencies remain under `.build/` and are excluded from the source archive.

To also build Setup, install Inno Setup 6 and provide its compiler:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File native\build.ps1 -InnoCompiler 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
```

The portable ZIP and Setup are written beside the source directory; the unpacked app is `release\native-windows\`. The shared UI is in `src/app.*`, reusable unit formatting in `src/format.js`, WebView2 bridge in `src/native-bridge.js`, native host/store/taskbar drawing in `native/NativeApp.cs`, and IP Helper collector in `native/windows.cs`.

## Test

```powershell
# UI-independent native checks:
.\release\native-windows\NetPeep.exe --selftest C:\path\to\selftest.txt

# Real WebView2 + native collector + screenshot checks; isolated history directory:
.\release\native-windows\NetPeep.exe --smoke C:\path\to\test-folder

# Shared formatting, parser and counter tests (requires Node.js):
npm test
```

The native integration harness exercises actual unit selectors, themes, native samples, About/Donate visibility, transparent taskbar output, saved position, close-to-tray and preference persistence. It captures compact-window screenshots and does not open donation URLs or initiate payments. See `VALIDATION.md` for outcomes and limits.

Microsoft WebView2 SDK license/notices accompany the binaries. The shared runtime updates independently via Microsoft. The original app code is MIT licensed.
