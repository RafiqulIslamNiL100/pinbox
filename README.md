# Pinbox

A Windows desktop app: sign in, keep a numbered list of saved messages, and
click any one of them to send it into whatever text box you were last
focused on (a browser comment box, an email client, a chat app, etc.).

## Download and install

**[Download Pinbox-for-Windows.zip](./Pinbox-for-Windows.zip)** — that's
the whole app, no installer required, no other downloads needed.

1. Extract the zip
2. Double-click **`Install Pinbox.bat`** inside it

That copies Pinbox to your PC, creates a Desktop and Start Menu shortcut,
and launches it. Windows SmartScreen may show an "unrecognized app"
warning the first run since it isn't code-signed — click "More info" →
"Run anyway".

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

## Building it yourself

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/RafiqulIslamNiL100/pinbox.git
cd pinbox
build-installer.bat
```

That produces `Pinbox-for-Windows.zip` in the project root — the same
file linked at the top of this README.

Or manually:
```bash
dotnet publish src/Pinbox/Pinbox.csproj -r win-x64 -c Release --self-contained true -o publish-output
```

## Publishing an update

1. Bump `<Version>` in `src/Pinbox/Pinbox.csproj` and the `version` field
   in `version.json` (must match)
2. Run `build-installer.bat`
3. Commit and push `Pinbox-for-Windows.zip` and `version.json`

Anyone with Pinbox already installed will see the update banner next time
they open it.

## Known limitations (v1)

- No auto-submit — inserts the message only, you send it.
- No global hotkey yet; you switch to the Pinbox window and click a
  message.
- Accounts are local-only; no password reset or multi-device sync.
- Windows only.
