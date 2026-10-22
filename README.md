<div align="center">

# discord-nowcast

**Show the track from any app in your Discord status.**

Yandex Music, Spotify, SoundCloud, VK, Telegram, YouTube in a browser, AIMP, local files: anything that plays on Windows.

[![release](https://img.shields.io/github/v/release/playsin333/discord-nowcast?color=5865f2)](https://github.com/playsin333/discord-nowcast/releases/latest)
[![downloads](https://img.shields.io/github/downloads/playsin333/discord-nowcast/total?color=3d3c38)](https://github.com/playsin333/discord-nowcast/releases)
![platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-3d3c38)
[![license](https://img.shields.io/badge/license-MIT-3d3c38)](LICENSE)

**English** · [Русский](README.ru.md)

**[⬇ Download](https://github.com/playsin333/discord-nowcast/releases/latest/download/discord-nowcast.zip)** · [Discussions](https://github.com/playsin333/discord-nowcast/discussions) · [Report a bug](https://github.com/playsin333/discord-nowcast/issues/new?template=bug.yml)

</div>

## What you get

Your profile and the server member list show **Listening to …**, just like Spotify does:

- the artist and the track title;
- where it plays: Yandex Music, Telegram, SoundCloud, Chrome;
- the cover art, the same picture the player shows;
- a progress bar, when the app reports the track length.

When the music is paused or stops, the status clears itself.

## Install

Nothing else to install: it runs on the PowerShell that ships with Windows.

1. **[Download `discord-nowcast.zip`](https://github.com/playsin333/discord-nowcast/releases/latest/download/discord-nowcast.zip)** and unzip it to a folder you will keep, for example `Documents\discord-nowcast`.
2. **Run `install.cmd`.** It starts in the background with no window and from now on starts with Windows.

If Windows says *"Windows protected your PC"*, click **More info → Run anyway**: the files come from the internet and are not signed.

The Discord desktop app must be running (not the website), with **Settings → Activity Privacy → Share your detected activities with others** turned on.

| File | What it does |
| --- | --- |
| `install.cmd` | starts it in the background and adds it to startup |
| `uninstall.cmd` | stops it and removes it from startup |
| `start.cmd` | runs it in a window, showing what is sent to Discord |
| `check.cmd` | shows what Windows reports as playing, without Discord |

To update, download the new zip, unzip it over the old folder and run `install.cmd` again.

## Settings

Everything lives in `config.json`. After editing it, run `install.cmd` again so the program rereads it.

| Setting | What it means |
| --- | --- |
| `clientId` | the Discord application whose name follows "Listening to". The built-in one works out of the box; for your own name create an app at [discord.com/developers/applications](https://discord.com/developers/applications) (**New Application**) and paste its **Application ID** here |
| `inList` | what the member list shows after "Listening to": `source` where it plays (default), `artist`, `title`, or `app` for the application name |
| `showPaused` | `true` keeps the status on pause, marked as paused |
| `covers` | `false` turns cover art off |
| `image` | a picture URL to show when the player gives no cover |
| `hide` | apps never to show: `["chrome", "Telegram"]` |
| `names` | your own source names: `{"chrome": "YouTube"}` |

Example:

```json
{
  "clientId": "1562901782579912804",
  "inList": "source",
  "showPaused": false,
  "covers": true,
  "image": "",
  "hide": ["Claude"],
  "names": { "chrome": "YouTube" }
}
```

## How it works

Windows already knows what is playing: it is the track card that pops up when you press the volume keys. `nowcast.ps1` reads those system media sessions once a second and, when something changes, sends the status to Discord over its local IPC pipe, the same one games use.

- **Source name.** Taken from the app's ID. A site installed as an app (SoundCloud or YouTube Music via *Install* in Chrome or Edge) is named as in the Start menu, so the status says SoundCloud, not Chrome.
- **Cover art.** The picture the player hands to Windows along with the track. Discord only shows pictures by URL, so it is uploaded to the temporary host [litterbox.catbox.moe](https://litterbox.catbox.moe) for three days. If the player gives no picture, Discord shows the application icon.
- **Rate limit.** Discord accepts at most five updates per 20 seconds, so the status changes at most once every four seconds.

## Limitations

- The app has to tell Windows what it plays. Almost every app does; if `check.cmd` does not see it, neither does discord-nowcast.
- A regular browser tab shows up as the browser, not the site: Chrome, not SoundCloud. Install the site as an app, or name it yourself in `names`.
- Not every player hands over a cover. Telegram, for example, often does not, and then the application icon is shown.
- The progress bar needs the app to report the track length. Yandex Music, Spotify and most sites do.

## Privacy

The artist and title go only to Discord: that is the status itself. The cover picture from the player is uploaded to litterbox.catbox.moe; `"covers": false` turns that off. Nothing else is sent anywhere.

The log of the current run is at `%TEMP%\discord-nowcast.log`.

## Questions, bugs, ideas

- [Discussions](https://github.com/playsin333/discord-nowcast/discussions): questions, ideas, show your setup.
- [Report a bug](https://github.com/playsin333/discord-nowcast/issues/new?template=bug.yml): the output of `check.cmd` and the log help the most.

Changes are listed in [CHANGELOG.md](CHANGELOG.md).

License: [MIT](LICENSE).
