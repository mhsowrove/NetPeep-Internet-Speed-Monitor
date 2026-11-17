# macOS build status

No macOS binary was produced on the Windows workstation. An actual electron-builder attempt rejected the host with `Build for macOS is supported only on macOS`. The Mac runtime, collectors, permissions and menu-bar behavior have not been exercised on a Mac here.

## GitHub Actions

The **Desktop builds** workflow runs a real `macos-latest` runner for each architecture:

- `NetPeep-macOS-arm64-development`: Apple Silicon (M-series Macs).
- `NetPeep-macOS-x64-development`: Intel Macs.

Each successful job uploads a DMG and application ZIP as workflow artifacts. Open the repository's **Actions → Desktop builds → successful run → Artifacts**. Artifacts only exist after the Mac jobs finish successfully; source upload does not by itself prove successful packaging or runtime behavior.

Builds are explicitly ad-hoc signed for development and **not Apple Developer ID signed or notarized**. Public release distribution requires Apple signing/notarization credentials and runtime testing on both architectures. A GitHub runner build is a packaging check, not a test of live network attribution or interactive menu-bar behavior.

## Build on a Mac

```sh
npm ci
npm test
npm run dist:mac:dev -- arm64 # Apple Silicon development DMG + ZIP
npm run dist:mac:dev -- x64   # Intel development DMG + ZIP
```

Files go to `release/macos/`. For a public signed/notarized build use the normal `dist:mac` command with your own Apple signing setup instead of the ad-hoc development helper.

macOS currently uses the shared Electron host and bundled `netstat`/`nettop` tools. Unlike the new small Windows host, the Mac build still bundles Electron and will be much larger than the Windows installer. See README for measurement limits and the remaining Mac acceptance checks.
