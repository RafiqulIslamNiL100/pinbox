# Pinbox

A Windows desktop app: sign in, keep a numbered list of saved messages, and
click any one of them to send it into whatever text box you were last
focused on (a browser comment box, an email client, a chat app, etc.).

## Download and install

**[Download Pinbox-Setup.exe](./Pinbox-Setup.exe)** and run it — a normal
Windows installer wizard: Welcome → choose folder → Install → Finish. It
creates a Desktop shortcut, a Start Menu entry (with an uninstaller), and
launches Pinbox when done. No admin rights needed (installs to your user
profile), no other downloads required.

Windows SmartScreen may show an "unrecognized app" warning the first run
since it isn't code-signed — click "More info" → "Run anyway".

(There's also **[Pinbox-for-Windows.zip](./Pinbox-for-Windows.zip)** — a
portable version if you'd rather extract and run `Install Pinbox.bat`
yourself instead of using the installer. Both contain the same app.)

## How it works

- Built with **Avalonia UI** (.NET 8) — a cross-platform, WPF-like UI
  framework. Unlike WPF, it doesn't require Windows-only build tooling,
  which is what makes it possible to build and test this app on Linux/CI
  and publish straight to this repo instead of needing a separate
  installer-building step on a Windows machine.
- **Accounts & storage are local to the PC** — signing up creates a local
  account (name/email/password, PBKDF2-hashed) stored as JSON in
  `%APPDATA%\Pinbox`. There is no server; this is a login gate + per-account
  message list, not cross-device sync.
- **Sending a message**: clicking a saved message copies it to the
  clipboard, minimizes Pinbox (handing keyboard focus back to whatever
  window was active before), and sends `Ctrl+V` via the Windows `SendInput`
  API. It does **not** auto-submit — you press Enter/Send yourself.
- **Self-update**: on launch, Pinbox checks this repo's `version.json`. If
  a newer version is published, a banner offers to update — it downloads
  `Pinbox-for-Windows.zip`, swaps the installed files, and relaunches.
- **The installer** (`Pinbox-Setup.exe`) is built with NSIS, compiled via
  the standalone Linux `makensis` (Ubuntu's `nsis` package) — not
  electron-builder's Node wrapper, which downloads a code-signing toolkit
  that fails to extract without a Windows privilege some accounts don't
  have. `makensis` needs nothing beyond itself, so that problem doesn't
  apply here. See `installer/pinbox.nsi`.

## Building it yourself

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/RafiqulIslamNiL100/pinbox.git
cd pinbox
build-installer.bat
```

That produces `Pinbox-for-Windows.zip` in the project root (the portable
version). To also build `Pinbox-Setup.exe`, install
[NSIS](https://nsis.sourceforge.io/Download) and run:

```
makensis installer\pinbox.nsi
```

(this repo's copy was built with the standalone `makensis` on Linux, but
the Windows NSIS install works the same way.)

## Publishing an update

1. Bump `<Version>` in `src/Pinbox/Pinbox.csproj` and the `version` field
   in `version.json` (must match)
2. Run `build-installer.bat`, then `makensis installer\pinbox.nsi`
3. Commit and push `Pinbox-Setup.exe`, `Pinbox-for-Windows.zip`, and
   `version.json`

Anyone with Pinbox already installed will see the update banner next time
they open it.

## Known limitations (v1)

- No auto-submit — inserts the message only, you send it.
- No global hotkey yet; you switch to the Pinbox window and click a
  message.
- Accounts are local-only; no password reset or multi-device sync.
- Windows only.
