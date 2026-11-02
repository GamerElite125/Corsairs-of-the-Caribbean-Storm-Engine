# Corsairs of the Caribbean

A free, non-commercial fan rework of Corsairs Ship Pack in full English, by DIRERAVEN. It adds:
- deck walking at sea and diving
- bigger jungles and beaches
- swimming in the harbours
- its own launcher, with news and updates

This repository holds the launcher, its source code, the news feed and the patches.
**It does not contain the game.** You need your own installation of the base game.

## For players
1. Download `CotC-Launcher-v1.1.0.zip` from **Releases**.
2. Unzip it into your game folder: the folder that has `engine.exe`.
3. Start `Corsairs of the Caribbean.exe`. It shows the news and offers new patches when they come out.

A patch only writes the files inside it. Every file it replaces is backed up first to
`Docs_AND_Report\UpdateBackups\<version>\`, and nothing is ever deleted.

## For the maintainer
- `launcher\CotcLauncher.cs`: the launcher source (C#, .NET Framework 4.8).
  To build it, run `launcher\build.cmd`. It uses the compiler that comes with Windows.
- `launcher\launcher_cotc.ini`: which repository the launcher reads (`Repo=`).
- `news.json`: the news shown in the launcher, newest first.
- `launcher\PUBLISHING_UPDATES.md`: how to post news and publish a patch.

## Credits
See `launcher\launcher_assets\credits.txt`. The game is built on Corsairs Ship Pack (LEOPARD's team and
contributors), Storm Engine (Akella and the storm-devs project) and Sea Dogs: City of Abandoned
Ships (Akella / Seaward). It is not an official Sea Dogs, Corsairs or Pirates of the Caribbean release.
