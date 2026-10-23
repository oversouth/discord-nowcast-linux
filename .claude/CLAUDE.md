# discord-nowcast

Показывает в статусе Discord («Слушает …», activity type 2) трек, который играет в любом приложении Windows. Один `discord-nowcast.exe` со значком в трее. Родственный проект: `playsin333/claude-code-player`, оттуда взято чтение медиасессий.

## Как устроено (src/)

- `Program.cs`: версия (`Version`, из неё же `AssemblyInfo.cs` — информация о версии в exe, чтобы антивирусы меньше ругались), один экземпляр (мьютекс `Local\discord-nowcast`, брошенный забирается; после обновления ждём старую копию до 15 с).
- `Engine.cs`: отдельный STA-поток, раз в секунду `Media.Read` → `Pick` → `Activity.Build` → `Discord.SetActivity`. У каждого сервиса своё приложение Discord (`ClientId` в правилах `Sources.cs`, `Settings.ClientIdFor`; свой `clientId` в настройках перекрывает все): другой источник → закрыть канал (это убирает статус) и подключиться с другим ID. Отправка только при изменении и не чаще раза в 4 с (лимит Discord 5/20 с). Перечитывает `config.json` по FileSystemWatcher.
- `WinRT.cs`, `Media.cs`: медиасессии через рефлексию по типам `..., ContentType=WindowsRuntime`, потому что встроенный csc не читает Windows.winmd (его нет без Windows SDK). Ожидание операций через `WindowsRuntimeSystemExtensions.AsTask`, поток обложки через `AsStreamForRead`, оба вызываются рефлексией.
- `Sources.cs`: имя источника (`names` из настроек → имя из меню «Пуск» через Shell.Application для `_crx_` и `!` → правила) и ссылка поиска для кнопки. Для браузеров сайт ищется по заголовкам окон (`WindowTitles.cs`): окно, где есть название трека или исполнитель, их вырезаем и ищем сервис в остатке; найденный сайт `Media` запоминает на трек. YouTube и Twitch — `IsVideo`, тип 3 «Смотрит». Логотип источника в углу обложки (small_image) пробовали в 1.1.0 и убрали по решению автора: Discord показывал вместо него «?».
- `Covers.cs`: обложка всегда из медиасессии, выкладывается на litterbox.catbox.moe (72h), при сбое на 30 минут переключается на uguu.se (3h). Multipart собирается вручную и с User-Agent: MultipartFormDataContent пишет boundary в кавычках и `filename*`, PHP-хостинги не видят файл, а WAF без User-Agent отвечает 500. Нет обложки → нет `assets`, Discord показывает иконку приложения (small_image без large_image не показывается).
- `Activity.cs`: строки, `status_display_type`, обложка, полоска, кнопка «Слушать на …» (поиск в том же сервисе, иначе YouTube).
- `Discord.cs`: `\\.\pipe\discord-ipc-N`, кадр op/длина/JSON, ping→pong. `IsAlive` через PeekNamedPipe: иначе перезапуск Discord не виден, пока не сменится трек. Discord ищется каждые 3 с только среди существующих каналов; через 10 с после подключения статус уходит повторно.
- `Theme.cs`: тёмные цвета Discord, рендерер меню трея (`ToolStripManager.Renderer`), свой `Theme.CheckBox` (системный остаётся белым), тёмный заголовок окна через DWM.
- `TrayApp.cs`: NotifyIcon и меню (в т.ч. «Источники в статусе»); `SettingsForm.cs`: окно настроек, каждое изменение сразу пишется в config.json (Settings.Save) и движок перечитывает его; `Startup.cs`: новая установка = exe запущен из нового места (маркер `installed-at` с путём) → HKCU Run и ярлык на рабочем столе; потом выбор пользователя не трогаем (удаление 1.0.x с остановкой процессов powershell убрано в 1.3.0: на него срабатывали антивирусы); `Updater.cs`: GitHub releases/latest, замена exe через переименование в `.old`.
- `Text.cs`: строки по-русски на русской Windows, иначе по-английски. Лог (`%APPDATA%\discord-nowcast\log.txt`) по-английски.

## Важно

- Код на C# 5: `build.ps1` собирает встроенным `Framework64\v4.0.30319\csc.exe`. Нельзя `$""`, `?.`, `nameof`, `=>`-члены, инициализаторы автосвойств, `out var`.
- Пути у сессии Claude длинные: `git clone -c core.longpaths=true`; PowerShell 5.1 не запускает `-File` по длинному абсолютному пути (относительный из текущей папки работает).
- Тестовый запуск из временной папки: заранее создать `%APPDATA%\discord-nowcast\config.json`, иначе первый запуск пропишет автозапуск на временный путь.
- Иконка `assets/icon.ico`: маленькие размеры — классические DIB, не PNG, иначе System.Drawing.Icon и трей рисуют мусор. Сделана из картинки ворона автора.
- Основной язык проекта русский: README.md на русском, README.en.md на английском (менять оба), CHANGELOG.md и заметки релизов на русском. Код, комментарии и лог — по-английски.
- Проверка сборки без Windows: `mcs -langversion:5` (mono) с заглушками для `WindowsRuntimeSystemExtensions`/`WindowsRuntimeStreamExtensions`; окно можно снять под `xvfb-run`. Шаблоны issues двуязычные, вопросы уводятся в Discussions.

## Релизы

- Поднять `Version` в `src/Program.cs` и добавить `## X.Y.Z` в `CHANGELOG.md` (оттуда заметки релиза). При пуше в `main` `.github/workflows/release.yml` на windows-latest собирает exe и создаёт релиз `vX.Y.Z` с `discord-nowcast.exe`. README ссылается на `releases/latest/download/discord-nowcast.exe`, а программа обновляется из этого же ассета.
- `gh` на машине автора нет; настройки репозитория (Discussions, описание, темы) автор меняет сам на сайте.
- Коммиты от имени `playsin <335104183+playsin333@users.noreply.github.com>`, без строки Co-Authored-By.
