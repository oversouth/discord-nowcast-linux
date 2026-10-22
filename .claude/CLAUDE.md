# discord-nowcast

Показывает в статусе Discord («Слушает …», activity type 2) трек, который играет в любом приложении Windows. Родственный проект: `playsin333/claude-code-player`, оттуда взято чтение медиасессий (`helper/now-playing.ps1`).

## Как устроено

- `nowcast.ps1`: Windows PowerShell 5.1, без зависимостей. Раз в секунду читает GlobalSystemMediaTransportControls, выбирает играющую сессию (`Select-Track`), шлёт `SET_ACTIVITY` в `\\.\pipe\discord-ipc-N` (кадр: op int32, длина int32, JSON UTF-8). Отправляет только при изменении и не чаще раза в 4 с (лимит Discord 5/20 с).
- Имя источника (`Get-SourceName`): `names` из config → имя из `Get-StartApps` для `_crx_` (установленные сайты, например SoundCloud) и `!` (приложения Магазина) → встроенные шаблоны → хвост идентификатора.
- Обложки: всегда картинка из медиасессии (`Thumbnail`), выкладывается на litterbox.catbox.moe (72h); нет картинки → без `assets`, Discord показывает иконку приложения. Каталоги Deezer/iTunes пробовали и убрали по решению автора. Поток WinRT в 5.1 читается только через `AsStreamForRead`, вызванный рефлексией (`$asStreamForRead.Invoke`).
- Остановка копий: фильтр `*-File*nowcast.ps1*` и исключение `$PID`, иначе убивается процесс, в чьей команде просто есть это слово.
- `setup.ps1 install|uninstall`: ярлык в автозагрузке на `conhost.exe --headless powershell.exe … nowcast.ps1` (без окна), остановка по командной строке процесса.
- `nowcast.ps1 -Check` (или `check.cmd`) печатает сессии без Discord.

## Важно

- `.ps1` хранятся в UTF-8 с BOM и CRLF: в них русские строки, а 5.1 без BOM читает файл в ANSI.
- Пути у сессии Claude длинные: `git clone` нужен с `-c core.longpaths=true`, а PowerShell 5.1 не запускает скрипты по таким путям. Для проверки копировать в `%TEMP%\nowcast-test`.
- Проверка без Discord: поддельный сервер на `NamedPipeServerStream('discord-ipc-0')`, который отвечает READY и пишет полученные кадры в лог.
- README.md на английском, README.ru.md на русском: менять оба. Шаблоны issues двуязычные, вопросы уводятся в Discussions.

## Релизы

- Поднять `$version` в `nowcast.ps1` и добавить `## X.Y.Z` в `CHANGELOG.md` (оттуда заметки релиза). При пуше в `main` `.github/workflows/release.yml` создаёт тег `vX.Y.Z` и релиз с `discord-nowcast.zip` (файлы в CRLF, внутри папка `discord-nowcast`). README ссылается на `releases/latest/download/discord-nowcast.zip`.
- `gh` на машине автора нет; настройки репозитория (Discussions, описание, темы) автор меняет сам на сайте.
- Коммиты от имени `playsin <335104183+playsin333@users.noreply.github.com>`, без строки Co-Authored-By.
