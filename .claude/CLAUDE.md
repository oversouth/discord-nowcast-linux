# discord-nowcast

Показывает в статусе Discord («Слушает …», activity type 2) трек, который играет в любом приложении Windows. Один `discord-nowcast.exe` со значком в трее. Родственный проект: `playsin333/claude-code-player`, оттуда взято чтение медиасессий.

## Как устроено (src/)

- `Program.cs`: версия (`Version`), один экземпляр (мьютекс `Local\discord-nowcast`, брошенный забирается; после обновления ждём старую копию до 15 с).
- `Engine.cs`: отдельный STA-поток, раз в секунду `Media.Read` → `Pick` → `Activity.Build` → `Discord.SetActivity`. Отправка только при изменении и не чаще раза в 4 с (лимит Discord 5/20 с). Перечитывает `config.json` по FileSystemWatcher.
- `WinRT.cs`, `Media.cs`: медиасессии через рефлексию по типам `..., ContentType=WindowsRuntime`, потому что встроенный csc не читает Windows.winmd (его нет без Windows SDK). Ожидание операций через `WindowsRuntimeSystemExtensions.AsTask`, поток обложки через `AsStreamForRead`, оба вызываются рефлексией.
- `Sources.cs`: имя источника (`names` из настроек → имя из меню «Пуск» через Shell.Application для `_crx_` и `!` → правила) и ссылка поиска для кнопки. Логотип источника в углу обложки (small_image) пробовали в 1.1.0 и убрали по решению автора: Discord показывал вместо него «?».
- `Covers.cs`: обложка всегда из медиасессии, выкладывается на litterbox.catbox.moe (72h). Нет обложки → нет `assets`, Discord показывает иконку приложения (small_image без large_image не показывается).
- `Activity.cs`: строки, `status_display_type`, обложка, полоска, кнопка «Слушать на …» (поиск в том же сервисе, иначе YouTube).
- `Discord.cs`: `\\.\pipe\discord-ipc-N`, кадр op/длина/JSON, ping→pong. `IsAlive` через PeekNamedPipe: иначе перезапуск Discord не виден, пока не сменится трек. Discord ищется каждые 3 с только среди существующих каналов; через 10 с после подключения статус уходит повторно.
- Планы (ждут автора): своё приложение Discord на каждый сервис («Слушает SoundCloud», «Смотрит YouTube» типом 3) — автор создаст 8 приложений и пришлёт ID; распознавание сайта по заголовку окна браузера (PlaybackType у Chrome всегда Music, по нему видео не отличить).
- `TrayApp.cs`: NotifyIcon и меню (в т.ч. «Источники в статусе»); `SettingsForm.cs`: окно настроек, каждое изменение сразу пишется в config.json (Settings.Save) и движок перечитывает его; `Startup.cs`: новая установка = exe запущен из нового места (маркер `installed-at` с путём) → HKCU Run и ярлык на рабочем столе; потом выбор пользователя не трогаем, удаление 1.0.x (ярлык в Startup + процесс powershell с `-File …nowcast.ps1`); `Updater.cs`: GitHub releases/latest, замена exe через переименование в `.old`.
- `Text.cs`: строки по-русски на русской Windows, иначе по-английски. Лог (`%APPDATA%\discord-nowcast\log.txt`) по-английски.

## Важно

- Код на C# 5: `build.ps1` собирает встроенным `Framework64\v4.0.30319\csc.exe`. Нельзя `$""`, `?.`, `nameof`, `=>`-члены, инициализаторы автосвойств, `out var`.
- Пути у сессии Claude длинные: `git clone -c core.longpaths=true`; PowerShell 5.1 не запускает `-File` по длинному абсолютному пути (относительный из текущей папки работает).
- Не писать в командах проверки текст вида `-File путь\nowcast.ps1`: exe при запуске убивает такие процессы powershell (это удаление 1.0.x).
- Тестовый запуск из временной папки: заранее создать `%APPDATA%\discord-nowcast\config.json`, иначе первый запуск пропишет автозапуск на временный путь.
- Иконка `assets/icon.ico`: маленькие размеры — классические DIB, не PNG, иначе System.Drawing.Icon и трей рисуют мусор. Сделана из картинки ворона автора.
- README.md на английском, README.ru.md на русском: менять оба. Шаблоны issues двуязычные, вопросы уводятся в Discussions.

## Релизы

- Поднять `Version` в `src/Program.cs` и добавить `## X.Y.Z` в `CHANGELOG.md` (оттуда заметки релиза). При пуше в `main` `.github/workflows/release.yml` на windows-latest собирает exe и создаёт релиз `vX.Y.Z` с `discord-nowcast.exe`. README ссылается на `releases/latest/download/discord-nowcast.exe`, а программа обновляется из этого же ассета.
- `gh` на машине автора нет; настройки репозитория (Discussions, описание, темы) автор меняет сам на сайте.
- Коммиты от имени `playsin <335104183+playsin333@users.noreply.github.com>`, без строки Co-Authored-By.
