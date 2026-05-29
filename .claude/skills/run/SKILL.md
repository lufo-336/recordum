---
name: run
description: Launch the Recordum WPF app for visual verification — builds and starts src/Recordum.WPF, optionally reset local app state before launch.
---

# Run Recordum (WPF)

Use this skill when the user asks to launch / start / open Recordum, or when a code change needs a visual smoke verification before commit. Recordum is a `.NET 10` WPF app whose entrypoint is `src/Recordum.WPF/Recordum.WPF.csproj`; it cannot be driven headlessly. You launch it; the user verifies on screen.

## Steps

1. **Build first.** Always run `dotnet build src/Recordum.WPF` (or `--no-restore` if the user is iterating fast and packages haven't changed) and confirm a clean build before launching. A launch on a stale binary is worse than no launch.

2. **Optional reset before launch.** If the user wants a clean slate (`--clean` / "fresh state" / "reset"), delete the per-user state directory **before** running. On Windows the path is `%LocalAppData%\Recordum` (resolves to `C:\Users\<USER>\AppData\Local\Recordum`). It contains the SQLCipher DB (`recordum.db`), the DPAPI-wrapped key files (`db.key`, `db.salt`, `autobackup.key`), `attachments/`, `backups/`, and `logs/`. Removing it forces a fresh first-run experience (migrations re-applied from empty, seed data loaded).

   PowerShell:
   ```powershell
   Remove-Item -Recurse -Force "$env:LocalAppData\Recordum" -ErrorAction SilentlyContinue
   ```
   Bash (under WSL or git-bash):
   ```bash
   rm -rf "$LOCALAPPDATA/Recordum" 2>/dev/null
   ```

   **Confirm with the user before deleting** unless they explicitly said `--clean` / "wipe" / "reset" — the SQLCipher DB and `.recbak` backups inside `backups/` are real user data, even on a dev machine.

3. **Launch in background.** The app opens a Window on the user's desktop and blocks. Use the Bash tool with `run_in_background: true` so the conversation can continue:
   ```bash
   dotnet run --project src/Recordum.WPF --no-build
   ```
   Use `--no-build` because step 1 already built. Background command returns a task ID; you'll be notified when the app exits (user closes the window, or it crashes).

4. **What "verified" looks like.** The user closes the window after testing → exit code 0. If they report a crash or visual bug, you'll get the task notification with the exit code; read the output log file (the one the background command names) to inspect the stack trace.

5. **Tell the user what to look at.** When the change you're verifying is UI / theming / a specific tab, give them a checklist (3-5 bullet points) of what to confirm. E.g.: "Open Dark mode in Settings, click into a habit, then tell me: 1) is text readable? 2) does Detail tab open without crash? 3) ...". A launch without follow-up questions wastes the visual session.

## Failure modes

- **Build error.** Don't launch a broken binary. Surface the compiler diagnostics to the user; don't auto-fix unless it's a trivial typo on your own recent edit.
- **App crashes at launch (exit code ≠ 0).** Read the output file the background task wrote. The app's own logger (`%LocalAppData%\Recordum\logs\recordum.log`) also captures unhandled exceptions globally — check both. Common culprits historically: XAML parse errors (CommandParameter type mismatches), SkiaSharp native bindings missing, SQLCipher key transition failures.
- **WebView2 missing.** Not a crash — `IWebView2Availability.IsAvailable=false`, the editor falls back to `TextBox`. The "WebView2 not available" banner appears above `HabitScreen`. Not a launch failure, but a feature degradation worth noting to the user.
- **GPU / Aero issue.** Rare. WPF compositor problems usually surface as a black window. Suggest the user try `--disable-gpu` (not currently supported but can be added) or run in safe mode.

## What this skill does NOT do

- It does not auto-test the app — no Playwright / WinAppDriver wiring exists. Visual verification is human-in-the-loop.
- It does not run unit tests; for that use `dotnet test` directly.
- It does not handle multi-user / installed-app scenarios — only the dev `dotnet run` flow.
