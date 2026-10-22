# Changelog

## 1.2.0

- **Settings window** from the tray menu: what the member list shows, cover art, the "Listen on …" button, keeping the status on pause, sources, starting with Windows, update checks. Changes apply at once.
- **Choose the sources.** *Sources in the status* in the tray menu and the settings window has a checkbox per app playing now: clear one to keep, say, YouTube in the browser out of the status.
- **Discord started after the music** now picks up the status within seconds. Before, a Discord that was closed and opened again went unnoticed until the track changed. The status is also sent once more ten seconds after connecting, since a Discord still starting up can drop the first one.
- **Every new install** — the exe downloaded again or moved — starts with Windows and gets a desktop shortcut, even when settings from an earlier install are still there. Autostart turned off or a shortcut deleted stay that way afterwards.
- A click on a line in *What Windows sees* copies the app's ID, for reporting a player that is not recognized.

Окно настроек, выбор источников галочками, статус подхватывается после запуска Discord, автозагрузка и ярлык при каждой новой установке.

## 1.1.1

- **Desktop shortcut.** The first start puts a discord-nowcast shortcut on the desktop, next to adding itself to startup. Delete it and it stays deleted; move the exe and the shortcut follows.
- The source logo in the corner of the cover is gone: Discord showed a question mark instead of it. The `sourceIcons` setting goes with it.

Ярлык на рабочем столе при первом запуске; логотип источника в углу обложки убран.

## 1.1.0

- **One exe instead of scripts.** discord-nowcast is now a single small `discord-nowcast.exe` with a raven icon: download, run, done. It starts with Windows by itself. The PowerShell 1.0.x is stopped and removed from startup on the first run.
- **Tray icon.** A raven by the clock: the current track on hover, and a menu to hide the status for an hour, see what Windows reports, turn starting with Windows on or off, open the settings and the log, check for updates, exit.
- **Source logo on the cover.** Yandex Music, Spotify, SoundCloud, Telegram, YouTube, VK, Apple Music and browsers get their logo in the corner of the cover. Off with `"sourceIcons": false`.
- **"Listen on …" button** in the status: a search for the track in the same service, YouTube for Telegram and browsers. Off with `"buttons": false`.
- **Updates.** Checks GitHub every six hours and updates itself in place on a click. Off with `"checkUpdates": false`.
- Settings moved to `%APPDATA%\discord-nowcast\config.json` and apply as soon as the file is saved. The interface is in Russian on a Russian Windows and in English elsewhere.

Один .exe вместо скриптов, значок у часов с меню, логотип источника на обложке, кнопка «Слушать на …» и обновление в один клик.

## 1.0.2

- Track length is no longer looked up on Deezer for players that do not report it: the bar went wrong after seeking, which discord-nowcast cannot see. Such players (Telegram) show elapsed time again, and nothing goes to Deezer.

Длина трека больше не ищется в Deezer: после перемотки полоска врала. У Telegram снова показывается прошедшее время.

## 1.0.1

- **Progress bar for Telegram** and other players that report no track length: the length is looked up on Deezer and the time is counted by discord-nowcast itself. Turn off with `"lengths": false`.
- The source is no longer shown twice in the card ("Telegram / Telegram") when the track has no album.

Полоска времени и для Telegram (длина трека ищется в Deezer), источник больше не повторяется дважды.

## 1.0.0

The first release.

- **Listening to … in Discord** for whatever plays on Windows: Yandex Music, Spotify, SoundCloud, VK, Telegram, browsers, AIMP, local files. Artist, title, where it plays, and a progress bar when the app reports the track length.
- **Cover art** straight from the player, the same picture it shows. Without one, the application icon.
- **Source names** for common apps, and the Start menu name for sites installed as apps, so SoundCloud reads as SoundCloud, not Chrome.
- **When several things play**, a track with a known length wins over a video feed in the browser.
- **Install in one click**: `install.cmd` starts it with no window and adds it to startup; `uninstall.cmd` removes it.
- **Settings** in `config.json`: what the member list shows, keeping the status on pause, hidden apps, your own source names.
- Clears the status on pause and when nothing plays; reconnects when Discord restarts.

---

Первый релиз: «Слушает …» в Discord для всего, что играет в Windows, обложка из плеера, установка в один клик. Подробности — в [README.ru.md](README.ru.md).
