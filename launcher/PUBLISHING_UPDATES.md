# Publishing updates and news for the Corsairs of the Caribbean launcher

The launcher gets updates and news from a GitHub repository you own. Nothing is linked to the
old CSP update servers.

## One-time setup

1. Create a GitHub repository, for example `Corsairs-of-the-Caribbean-Storm-Engine` (it can be public; it only
   needs to hold releases and `news.json`, not the whole game).
2. Upload `news.json` (a starter copy is in this folder) to the repository's `main` branch.
3. In the game folder, open `launcher_cotc.ini` and set your repository:

   ```
   [Updates]
   Repo=YOUR-GITHUB-NAME/Corsairs-of-the-Caribbean-Storm-Engine
   Branch=main
   CheckOnStart=1
   ```

   Ship this ini with the game so every player's launcher looks in the same place.

## Publishing a patch

1. Make your changes in the game folder and test them.
2. Build the patch zip from the files you changed:

   ```
   .\make_patch.ps1 -Version 1.1.1 -Since "2026-11-01 12:00"
   ```

   or list the files yourself (one path per line, relative to the game folder) and use
   `-FileList changed.txt`. Saves, settings (`options`, `engine.ini`) and the Docs folder are
   never included.
3. On GitHub: Releases > Draft a new release.
   - Tag: `v1.1.1` (always higher than the last one)
   - Title and description: the patch notes. Players see them on the launcher's Updates page.
   - Attach `patch-v1.1.1.zip`.
   - Publish.

Players' launchers see the new release, show the notes, and install it when they press
"Install updates". Several missing patches install in order, oldest first.

## Posting news

Edit `news.json` in the repository (newest first):

```json
[
  { "date": "2026-11-20", "title": "Patch 1.1.1", "text": "Fixed ..." },
  { "date": "2026-11-01", "title": "v1.1.0 Beta", "text": "..." }
]
```

## Why it can't break your own changes

- A patch only writes the files inside its zip. It never deletes anything.
- Every file it replaces is copied to `Docs_AND_Report\UpdateBackups\<version>\` first.
- Paths that try to leave the game folder are refused.
- If the launcher itself is in the patch, it is swapped in the next time the launcher starts.

## Rebuilding the launcher

Run `build.cmd` in this folder. It uses the C# compiler that comes with Windows and writes
`Corsairs of the Caribbean.exe` into the game folder. Source: `CotcLauncher.cs`.
