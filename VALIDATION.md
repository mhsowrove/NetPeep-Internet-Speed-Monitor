# Validation — 17 November 2026

## Version 2.0 compact/native Windows update

- Eleven shared Node test groups pass, including decimal Auto/B/KB/MB/GB formatting, tiny fixed-GB values, and validated unit/theme preferences.
- Native x64 Windows app compiled with the installed .NET Framework C# compiler. It links to Microsoft WebView2 SDK 1.0.4314.35 and uses the machine's shared WebView2 runtime. No Electron runtime is in the Windows payload.
- Native selftest passed: day/week totals, unit conversions, ledger validation and preference serialization roundtrip.
- Actual WebView2/native integration passed against one adapter and six observed processes: all four fixed-unit selector controls changed both backend state and taskbar units; light/dark themes applied; Overview and Settings fit the 420×530 client area without vertical scrolling; native samples continued; developer name and donation section matched the saved profile; transparent meter corner alpha was zero; chosen taskbar position persisted; close-to-tray and saved settings passed. Real screenshots were visually inspected. Donation/contact links were not opened and no payment action was performed.
- The shared Electron development host also passed integration after the compact-UI changes, including the new unit/theme preferences. This was executed on Windows and does not establish macOS runtime correctness.
- Windows Setup built successfully with Inno Setup: **2,355,619 bytes**, versus approximately 101 MB for the former Electron package. Portable ZIP: **368,175 bytes**. These sizes exclude the separately installed WebView2 runtime and its cache. The installer itself was built, not installed on the user's system; its unpacked payload was the app exercised above.
- Approved network icon replaced the bird in SVG, 1024px PNG, multi-resolution Windows ICO and monochrome macOS template artwork.
- Remaining checks: clean-machine installation with/without WebView2, installer/uninstaller behavior, real pointer dragging at multiple DPI settings, long-run/battery behavior, signed distribution and actual macOS runs. Earlier per-process collection limitations remain unchanged.

To reproduce the native Windows selftest/integration checks, see `WINDOWS.md`. Older validation notes below describe the earlier Electron implementation, not additional proof for the native host.

## Version 1.1 taskbar update

- Nine automated tests pass, including three new geometry tests for bottom, top, resized, negative-coordinate and vertical taskbars.
- Native Windows taskbar discovery returned the actual primary taskbar and notification-area rectangles.
- Electron integration passed: actual taskbar placement, 112px meter width, unlock/reposition/repin preserving the chosen horizontal location, saved pinned state, transparent computed background, zero alpha at the captured corner, and upload above download. Existing collector/dashboard/adapter/toggle/close-to-tray checks also passed.
- Remaining manual/platform checks: auto-hide animations, Explorer restart, changed DPI while running, real pointer dragging, mixed-DPI multi-monitor setups and full-screen applications. The overlay targets only the primary taskbar, does not reserve shell space and may need manual positioning to avoid existing icons.
- macOS validation and per-process byte-accuracy limitations listed below remain unchanged.

Host: Windows x64, Node 24.22.1, Electron 42.11.18, electron-builder 26.18.0. macOS was not available.

## Passed

- Six Node test groups: counter baselines/resets/disappearance/long gaps; physical/manual adapter selection; calendar periods including Monday week/month transition; atomic persistence/readback/corrupt backup; macOS interface parser; macOS process parser with socket-row exclusion.
- JavaScript syntax checks for main process, renderer, collectors and integration harness.
- Windows PowerShell successfully compiled the native C# collector without a .NET SDK.
- Native adapter counters and active process ownership/connection counts read successfully in a normal nonadministrator desktop context. The restricted command environment returned access denied for the process table; this was reported honestly in collector status.
- Real Electron integration harness: one adapter, seven observed processes, healthy native collection; period-tab selection; adapter selection; floating-meter toggling; close-to-tray behavior; continued sampling; no renderer console errors observed during interaction; actual screenshot captured. Process byte rates correctly remained unavailable for a nonadministrator launch.
- Local daily ledger written on disk, including received/transmitted bytes; durable readback also covered in automated tests.
- Windows unpacked application built and launched. Dashboard and live-meter UI inspected through computer-use; actual live rates, local totals and per-process dashes/connection counts visible.

## Not verified here

- Administrator-only EStats enablement and process-byte correctness against a controlled IPv4 TCP transfer. This native implementation follows documented IP Helper contracts, but needs the administrator-mode acceptance test in README.
- macOS process/adapter counters on an actual Mac, menu-bar layout, permission behavior, Intel/Apple Silicon builds, signing and notarization. macOS parsers were tested with fixtures only.
- Physical sleep/resume, real midnight/timezone changes, multiple real adapters/VPN paths, traffic-loss bounds and sustained performance/battery consumption. Deterministic counter/calendar logic was tested; these end-to-end scenarios require hardware testing.
- Public-distribution signing and clean-machine installation. The supplied Windows build is unsigned and should be treated as a development build.

## Reproduce integration check

From the source directory after `npm ci`:

```powershell
$env:NETPEEP_SMOKE_DIR = 'C:\path\to\temporary\netpeep-smoke'
$env:NETPEEP_SCREENSHOT = 'C:\path\to\NetPeep-preview.png'
.\node_modules\.bin\electron.cmd scripts\smoke.cjs
```

The smoke harness uses its own data directory, performs actual native sampling, writes `smoke-result.json` and quits normally. It does not require an elevated Windows account. Do not use a production history directory for smoke testing. Use the platform acceptance checklist in README for remaining checks.

## Native API references

- Microsoft `GetExtendedTcpTable`: https://learn.microsoft.com/en-us/windows/win32/api/iphlpapi/nf-iphlpapi-getextendedtcptable
- Microsoft `GetPerTcpConnectionEStats`: https://learn.microsoft.com/en-us/windows/win32/api/iphlpapi/nf-iphlpapi-getpertcpconnectionestats
- Microsoft `SetPerTcpConnectionEStats`: https://learn.microsoft.com/en-us/windows/win32/api/iphlpapi/nf-iphlpapi-setpertcpconnectionestats
- Microsoft data-counter layout/semantics: https://learn.microsoft.com/en-us/windows/win32/api/tcpestats/ns-tcpestats-tcp_estats_data_rod_v0
- Electron tray platform behavior: https://www.electronjs.org/docs/latest/api/tray
- macOS: consult `man nettop` and `man netstat` on the target OS release; runtime parse failures are reported as unavailable measurements.
