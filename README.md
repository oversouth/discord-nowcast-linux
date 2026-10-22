<div align="center">

<img src="assets/icon.png" alt="" width="96">

# discord-nowcast

**Show the track from any app in your Discord status.**

Yandex Music, Spotify, SoundCloud, VK, Telegram, YouTube in a browser, AIMP, local files: anything that plays on Windows.

[![release](https://img.shields.io/github/v/release/playsin333/discord-nowcast?color=5865f2)](https://github.com/playsin333/discord-nowcast/releases/latest)
[![downloads](https://img.shields.io/github/downloads/playsin333/discord-nowcast/total?color=3d3c38)](https://github.com/playsin333/discord-nowcast/releases)
![platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-3d3c38)
[![license](https://img.shields.io/badge/license-MIT-3d3c38)](LICENSE)

**English** · [Русский](README.ru.md)

**[⬇ Download discord-nowcast.exe](https://github.com/playsin333/discord-nowcast/releases/latest/download/discord-nowcast.exe)** · [Discussions](https://github.com/playsin333/discord-nowcast/discussions) · [Report a bug](https://github.com/playsin333/discord-nowcast/issues/new?template=bug.yml)

</div>

## What you get

Your profile and the server member list show **Listening to …**, just like Spotify does:

- the artist and the track title;
- where it plays: Yandex Music, Telegram, SoundCloud, Chrome;
- the cover art, the same picture the player shows, with the source's logo in its corner;
- a progress bar, when the app reports the track length;
- a **Listen on SoundCloud** button (or Yandex Music, Spotify…) that opens the track for whoever clicks it.

When the music is paused or stops, the status clears itself.

## Install

1. **[Download `discord-nowcast.exe`](https://github.com/playsin333/discord-nowcast/releases/latest/download/discord-nowcast.exe)** and put it in a folder you will keep, for example `Documents\discord-nowcast`.
2. **Run it.** A raven appears by the clock, and from now on it starts with Windows.

That's all: one small file, nothing else to install. It needs Windows 10 or 11 and the Discord desktop app (not the website), with **Settings → Activity Privacy → Share your detected activities with others** turned on.

If Windows says *"Windows protected your PC"*, click **More info → Run anyway**: the file comes from the internet and is not signed.

## The tray icon

The raven by the clock, at the bottom right of the screen, is how you control it. Only you see it; nobody in Discord does. Hover for the current track, click for the menu:

| Item | What it does |
| --- | --- |
| Hide status for an hour | clears the status for an hour; click again to bring it back |
| What Windows sees | every app reporting a track right now, playing or paused |
| Start with Windows | on by default |
| Settings | opens `config.json`; changes apply as soon as you save |
| Open log | what was sent to Discord and any errors |
| Check for updates | downloads the new version and restarts; it also checks by itself every six hours |
| Exit | stops it and clears the status |

## Settings

`config.json` lives in `%APPDATA%\discord-nowcast` and opens from the tray menu.

| Setting | What it means |
| --- | --- |
| `clientId` | the Discord application whose name follows "Listening to". The built-in one works out of the box; for your own name create an app at [discord.com/developers/applications](https://discord.com/developers/applications) (**New Application**) and paste its **Application ID** here |
| `inList` | what the member list shows after "Listening to": `source` where it plays (default), `artist`, `title`, or `app` for the application name |
| `showPaused` | `true` keeps the status on pause, marked as paused |
| `covers` | `false` turns cover art off |
| `sourceIcons` | `false` removes the source's logo from the cover's corner |
| `buttons` | `false` removes the "Listen on …" button |
| `checkUpdates` | `false` stops the automatic update check |
| `image` | a picture URL to show when the player gives no cover |
| `hide` | apps never to show: `["chrome", "Telegram"]` |
| `names` | your own source names: `{"chrome": "YouTube"}` |

## How it works

Windows already knows what is playing: it is the track card that pops up when you press the volume keys. discord-nowcast reads those system media sessions once a second and, when something changes, sends the status to Discord over its local IPC pipe, the same one games use.

- **Source name.** Taken from the app's ID. A site installed as an app (SoundCloud or YouTube Music via *Install* in Chrome or Edge) is named as in the Start menu, so the status says SoundCloud, not Chrome.
- **Cover art.** The picture the player hands to Windows along with the track. Discord only shows pictures by URL, so it is uploaded to the temporary host [litterbox.catbox.moe](https://litterbox.catbox.moe) for three days. If the player gives no picture, Discord shows the application icon.
- **The button.** Players do not tell Windows the track's link, so "Listen on …" opens a search for the track in the same service; for Telegram and browsers, a YouTube search. Discord does not let you click your own buttons, only other people's.
- **Rate limit.** Discord accepts at most five updates per 20 seconds, so the status changes at most once every four seconds.

## Limitations

- The app has to tell Windows what it plays. Almost every app does; if it is missing under *What Windows sees*, discord-nowcast cannot see it either.
- A regular browser tab shows up as the browser, not the site: Chrome, not SoundCloud. Install the site as an app, or name it yourself in `names`.
- The progress bar needs the app to report the track length. Yandex Music, Spotify and most sites do; Telegram does not, so it shows elapsed time.

## Privacy

The artist and title go only to Discord: that is the status itself. The cover picture from the player is uploaded to litterbox.catbox.moe; `"covers": false` turns that off. The update check asks GitHub for the latest release. Nothing else is sent anywhere.

## Building it yourself

```
powershell -ExecutionPolicy Bypass -File build.ps1
```

It uses the C# compiler that ships with .NET Framework 4.8 on every Windows 10 and 11, so there is nothing to install. The code is in `src`.

## Questions, bugs, ideas

- [Discussions](https://github.com/playsin333/discord-nowcast/discussions): questions, ideas, show your setup.
- [Report a bug](https://github.com/playsin333/discord-nowcast/issues/new?template=bug.yml): a screenshot of *What Windows sees* and the log help the most.

Changes are listed in [CHANGELOG.md](CHANGELOG.md). Source logos come from [Simple Icons](https://simpleicons.org) (CC0); the brands belong to their owners.

License: [MIT](LICENSE).
