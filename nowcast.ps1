# discord-nowcast: показывает в статусе Discord трек, который играет в любом
# приложении Windows: Яндекс Музыка, Spotify, Telegram, браузер, плееры.
#
# Трек берётся из системных медиасессий (тех же, что в окошке громкости),
# в Discord уходит через его локальный канал (named pipe discord-ipc-N).
#
# Запускается во встроенном Windows PowerShell 5.1. Файл сохранён в UTF-8
# с BOM: без него 5.1 прочитает русские строки неправильно.

param(
    # Только напечатать, что играет, и выйти: для проверки без Discord.
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$logPath = Join-Path ([IO.Path]::GetTempPath()) 'discord-nowcast.log'

function Write-Log([string]$text) {
    $line = '{0:HH:mm:ss} {1}' -f (Get-Date), $text
    Write-Host $line
    try { Add-Content -LiteralPath $logPath -Value $line -Encoding UTF8 } catch { }
}

# --- Настройки ---------------------------------------------------------------

$config = @{
    clientId   = ''
    inList     = 'source'
    showPaused = $false
    covers     = $true
    image      = ''
    hide       = @()
    names      = @{}
}
$configPath = Join-Path $here 'config.json'
if (Test-Path -LiteralPath $configPath) {
    $fromFile = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($property in $fromFile.PSObject.Properties) {
        if ($property.Name -eq 'names') {
            foreach ($name in $property.Value.PSObject.Properties) { $config.names[$name.Name] = [string]$name.Value }
        }
        else {
            $config[$property.Name] = $property.Value
        }
    }
}

# --- Медиасессии Windows -----------------------------------------------------

try {
    Add-Type -AssemblyName System.Runtime.WindowsRuntime
    $asTaskGeneric = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
        $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and
        $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
    } | Select-Object -First 1

    # Ждёт WinRT-операцию не дольше $timeoutMs: зависшее приложение теряет
    # только свою сессию, а не всю программу.
    function Await($operation, [Type]$resultType, [int]$timeoutMs = 3000) {
        $task = $asTaskGeneric.MakeGenericMethod($resultType).Invoke($null, @($operation))
        if (-not $task.Wait($timeoutMs)) { throw "no answer in $timeoutMs ms" }
        $task.Result
    }

    $null = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows.Media.Control, ContentType = WindowsRuntime]
    $managerType = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager]
    $propertiesType = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties]
    $manager = Await ($managerType::RequestAsync()) $managerType 15000
}
catch {
    Write-Log "Не удалось открыть медиасессии Windows: $($_.Exception.Message)"
    exit 1
}

function Get-Timeline($session) {
    try {
        $timeline = $session.GetTimelineProperties()
        @{
            position  = ($timeline.Position - $timeline.StartTime).TotalSeconds
            duration  = ($timeline.EndTime - $timeline.StartTime).TotalSeconds
            updatedAt = $timeline.LastUpdatedTime.ToUnixTimeMilliseconds()
        }
    }
    catch {
        @{ position = 0; duration = 0; updatedAt = 0 }
    }
}

# Все сессии, где есть что показать.
function Get-Sessions {
    $current = $manager.GetCurrentSession()
    $currentApp = if ($current) { [string]$current.SourceAppUserModelId } else { $null }
    $list = @()
    foreach ($session in @($manager.GetSessions())) {
        try {
            $properties = Await ($session.TryGetMediaPropertiesAsync()) $propertiesType
            if (-not $properties -or (-not $properties.Title -and -not $properties.Artist)) { continue }
            $app = [string]$session.SourceAppUserModelId
            $timeline = Get-Timeline $session
            $track = @{
                app       = $app
                source    = Get-SourceName $app
                artist    = [string]$properties.Artist
                title     = [string]$properties.Title
                album     = [string]$properties.AlbumTitle
                isPlaying = $session.GetPlaybackInfo().PlaybackStatus -eq 'Playing'
                isCurrent = $app -eq $currentApp
                position  = $timeline.position
                duration  = $timeline.duration
                updatedAt = $timeline.updatedAt
            }
            $list += Repair-Track $track
        }
        catch {
            # Сессия может закрыться между списком и чтением; её просто нет.
        }
    }
    , $list
}

# --- Названия источников -----------------------------------------------------

# Идентификатор приложения в Windows -> понятное имя. Порядок важен: Яндекс
# Музыка раньше Яндекс Браузера.
$sourceNames = [ordered]@{
    'yandex.*music|music.*yandex' = 'Яндекс Музыка'
    'spotify'                     = 'Spotify'
    'telegram'                    = 'Telegram'
    'vk.?music|vkontakte|\bvk\b'  = 'VK Музыка'
    'soundcloud'                  = 'SoundCloud'
    'ZuneMusic'                   = 'Медиаплеер Windows'
    'AppleMusic|iTunes'           = 'Apple Music'
    'aimp'                        = 'AIMP'
    'foobar'                      = 'foobar2000'
    'vlc'                         = 'VLC'
    'musicbee'                    = 'MusicBee'
    'yandex|^browser'             = 'Яндекс Браузер'
    'chrome'                      = 'Chrome'
    'msedge|MicrosoftEdge'        = 'Edge'
    'firefox|308046B0AF4A39CB'    = 'Firefox'
    'opera'                       = 'Opera'
    'vivaldi'                     = 'Vivaldi'
    'brave'                       = 'Brave'
}

# Имена из меню «Пуск». Там видно, что Chrome._crx_… — это установленный
# сайт, например SoundCloud, а не просто Chrome. Перечитываются не чаще
# раза в минуту, когда встречается незнакомое приложение.
$startApps = @{}
$startAppsReadAt = [DateTime]::MinValue
function Get-StartAppName([string]$app) {
    if (-not $startApps.ContainsKey($app) -and ([DateTime]::Now - $script:startAppsReadAt).TotalSeconds -ge 60) {
        $script:startAppsReadAt = [DateTime]::Now
        try {
            foreach ($entry in @(Get-StartApps)) { $startApps[[string]$entry.AppID] = [string]$entry.Name }
        }
        catch {
        }
    }
    $startApps[$app]
}

function Get-SourceName([string]$app) {
    # Свои названия из config.json важнее встроенных: {"chrome": "SoundCloud"}.
    foreach ($key in $config.names.Keys) {
        if ($app -like "*$key*") { return $config.names[$key] }
    }
    # Установленные сайты и приложения из Магазина: имя как в меню «Пуск».
    if ($app -match '_crx_|!') {
        $name = Get-StartAppName $app
        if ($name) { return $name }
    }
    foreach ($pattern in $sourceNames.Keys) {
        if ($app -match $pattern) { return $sourceNames[$pattern] }
    }
    # Microsoft.Package_8wekyb3d8bbwe!App или Something.exe -> Package / Something.
    $name = ($app -replace '!.*$', '' -replace '_[a-z0-9]{13}$', '' -replace '\.exe$', '').Split('.')[-1]
    if ($name) { $name } else { 'Музыка' }
}

# Хвосты, которые сайты дописывают к названиям: (Official Video), [HD] и т.п.
$junk = '\s*[(\[](?:official\s+)?(?:music\s+|lyric\s+)?(?:video|audio|visuali[sz]er|lyrics?)(?:\s+video)?[)\]]|\s*[(\[](?:hd|hq|4k|official|клип|премьера клипа|премьера)[)\]]'

function Repair-Track($track) {
    $artist = $track.artist.Trim()
    $title = ($track.title -replace $junk, '').Trim()
    # Вкладки браузера часто дают пустого исполнителя и «Исполнитель - Трек».
    if (-not $artist -and $title -match '^(.+?)\s+[-–—]\s+(.+)$') {
        $artist = $Matches[1]
        $title = $Matches[2]
    }
    $track.artist = ($artist -replace '\s*-\s*Topic$', '' -replace 'VEVO$', '').Trim()
    $track.title = $title
    $track
}

function Test-Hidden($track) {
    foreach ($word in @($config.hide)) {
        if ($word -and ($track.app -like "*$word*" -or $track.source -like "*$word*")) { return $true }
    }
    $false
}

function Get-Key($track) { "$($track.app)|$($track.artist)|$($track.title)" }

# Что показывать: играющее, сначала то, что уже показано, потом «текущее»
# по мнению Windows. На паузе — показанное, если включено showPaused.
$script:shownKey = $null
function Select-Track($list) {
    $visible = @($list | Where-Object { -not (Test-Hidden $_) })
    $playing = @($visible | Where-Object { $_.isPlaying })
    $pool = @(if ($playing.Count -gt 0) { $playing } elseif ($config.showPaused) { $visible })
    if ($pool.Count -eq 0) { return $null }
    # Если играет несколько, трек с известной длиной важнее: так выглядит
    # музыка, а лента коротких роликов в браузере длину обычно не сообщает.
    $timed = @($pool | Where-Object { $_.duration -gt 0 })
    if ($timed.Count -gt 0) { $pool = $timed }
    $shown = $pool | Where-Object { (Get-Key $_) -eq $script:shownKey } | Select-Object -First 1
    if ($shown) { return $shown }
    $current = $pool | Where-Object { $_.isCurrent } | Select-Object -First 1
    if ($current) { return $current }
    $pool[0]
}

# --- Обложки -----------------------------------------------------------------

# Discord показывает картинку только по ссылке, поэтому обложку ищем в
# открытом каталоге iTunes: туда уходят исполнитель и название.
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
$coverCache = @{}
function Find-Cover($track) {
    if (-not $config.covers -or -not $track.title) { return $null }
    $key = "$($track.artist)|$($track.title)"
    if ($coverCache.ContainsKey($key)) { return $coverCache[$key] }
    if ($coverCache.Count -gt 500) { $coverCache.Clear() }
    $url = $null
    try {
        $term = [Uri]::EscapeDataString("$($track.artist) $($track.title)".Trim())
        $answer = Invoke-RestMethod -Uri "https://itunes.apple.com/search?term=$term&entity=song&limit=5" -TimeoutSec 5 -UseBasicParsing
        if ($answer -is [string]) { $answer = $answer | ConvertFrom-Json }
        $artist = $track.artist.ToLowerInvariant()
        foreach ($result in @($answer.results)) {
            $found = ([string]$result.artistName).ToLowerInvariant()
            # Чужая песня с похожим названием хуже, чем никакой обложки.
            if ($artist -and -not ($found.Contains($artist) -or $artist.Contains($found))) { continue }
            if ($result.artworkUrl100) {
                $url = ([string]$result.artworkUrl100) -replace '\d+x\d+bb', '512x512bb'
                break
            }
        }
    }
    catch {
        # Нет сети или каталог не ответил: без обложки. В кэш не кладём,
        # чтобы попробовать ещё раз на следующем треке.
        return $null
    }
    $coverCache[$key] = $url
    $url
}

# --- Статус ------------------------------------------------------------------

# Discord принимает строки от 2 до 128 символов.
function Limit([string]$text) {
    $text = $text.Trim()
    if ($text.Length -gt 128) { $text = $text.Substring(0, 127) + [char]0x2026 }
    while ($text.Length -lt 2) { $text += [char]0x2800 }
    $text
}

# Когда трек начался, в миллисекундах; $null, если приложение не сообщает время.
function Get-StartedAt($track) {
    if ($track.duration -le 0) { return $null }
    $now = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $position = $track.position * 1000
    # Позиция дана на момент updatedAt, с тех пор трек играл дальше.
    if ($track.isPlaying -and $track.updatedAt -gt 0) { $position += [math]::Max(0, $now - $track.updatedAt) }
    $position = [math]::Min($position, $track.duration * 1000)
    [int64]($now - $position)
}

function New-Activity($track, $startedAt) {
    $artist = if ($track.artist) { $track.artist } else { $track.source }
    $activity = @{ type = 2; instance = $false }
    # Что видно в списке участников сервера: «Слушает …».
    switch ([string]$config.inList) {
        'artist' {
            $activity.details = Limit $track.title
            $activity.state = Limit $artist
            $activity.status_display_type = 1
        }
        'title' {
            $activity.details = Limit $track.title
            $activity.state = Limit $artist
            $activity.status_display_type = 2
        }
        'app' {
            $activity.details = Limit $track.title
            $activity.state = Limit $artist
            $activity.status_display_type = 0
        }
        default {
            $line = if ($track.artist) { "$($track.artist) — $($track.title)" } else { $track.title }
            $activity.details = Limit $line
            $activity.state = Limit $track.source
            $activity.status_display_type = 1
        }
    }
    if (-not $track.isPlaying) { $activity.state = Limit "$($activity.state) · пауза" }

    $assets = @{}
    $cover = Find-Cover $track
    $image = if ($cover) { $cover } elseif ($config.image) { [string]$config.image } else { $null }
    if ($image) {
        $assets.large_image = $image
        $assets.large_text = Limit $(if ($track.album) { $track.album } else { $track.source })
    }
    if ($assets.Count -gt 0) { $activity.assets = $assets }

    if ($null -ne $startedAt -and $track.isPlaying) {
        $activity.timestamps = @{ start = $startedAt; end = $startedAt + [int64]($track.duration * 1000) }
    }
    $activity
}

# --- Канал Discord -----------------------------------------------------------

# Кадр: код операции (int32), длина (int32), JSON в UTF-8.
function Send-Frame($pipe, [int]$op, $body) {
    $json = $body | ConvertTo-Json -Compress -Depth 10
    $bytes = [Text.Encoding]::UTF8.GetBytes($json)
    $header = [byte[]]([BitConverter]::GetBytes([int32]$op) + [BitConverter]::GetBytes([int32]$bytes.Length))
    $pipe.Write($header, 0, 8)
    $pipe.Write($bytes, 0, $bytes.Length)
    $pipe.Flush()
}

function Read-Exactly($pipe, [int]$count) {
    $buffer = New-Object byte[] $count
    $got = 0
    while ($got -lt $count) {
        $task = $pipe.ReadAsync($buffer, $got, $count - $got)
        if (-not $task.Wait(10000)) { throw 'Discord не отвечает' }
        if ($task.Result -le 0) { throw 'Discord закрыл соединение' }
        $got += $task.Result
    }
    , $buffer
}

function Read-Frame($pipe) {
    while ($true) {
        $header = Read-Exactly $pipe 8
        $op = [BitConverter]::ToInt32($header, 0)
        $length = [BitConverter]::ToInt32($header, 4)
        $json = if ($length -gt 0) { [Text.Encoding]::UTF8.GetString((Read-Exactly $pipe $length)) } else { '{}' }
        $data = $json | ConvertFrom-Json
        # Ping -> pong, и читаем дальше.
        if ($op -eq 3) { Send-Frame $pipe 4 $data; continue }
        if ($op -eq 2) { throw "Discord закрыл соединение: $($data.message)" }
        return $data
    }
}

$script:pipe = $null
function Connect-Discord {
    for ($index = 0; $index -lt 10; $index++) {
        $pipe = New-Object System.IO.Pipes.NamedPipeClientStream('.', "discord-ipc-$index", [IO.Pipes.PipeDirection]::InOut, [IO.Pipes.PipeOptions]::Asynchronous)
        try {
            $pipe.Connect(300)
        }
        catch {
            $pipe.Dispose()
            continue
        }
        try {
            Send-Frame $pipe 0 @{ v = 1; client_id = [string]$config.clientId }
            $reply = Read-Frame $pipe
            if ($reply.evt -eq 'READY') {
                $script:pipe = $pipe
                Write-Log "Подключился к Discord ($($reply.data.user.username))"
                return $true
            }
        }
        catch {
            Write-Log "Discord отказал: $($_.Exception.Message)"
        }
        $pipe.Dispose()
    }
    $false
}

function Disconnect-Discord {
    if ($script:pipe) {
        try { $script:pipe.Dispose() } catch { }
        $script:pipe = $null
    }
}

# $null очищает статус.
function Set-Activity($activity) {
    $arguments = @{ pid = $PID }
    if ($activity) { $arguments.activity = $activity }
    Send-Frame $script:pipe 1 @{ cmd = 'SET_ACTIVITY'; args = $arguments; nonce = [guid]::NewGuid().ToString() }
    $reply = Read-Frame $script:pipe
    if ($reply.evt -eq 'ERROR') { Write-Log "Discord не принял статус: $($reply.data.message)" }
}

# --- Запуск ------------------------------------------------------------------

if ($Check) {
    $list = Get-Sessions
    if ($list.Count -eq 0) { Write-Host 'Сейчас ничего не играет (или приложение не сообщает Windows о треке).' }
    foreach ($track in $list) {
        $state = if ($track.isPlaying) { 'играет' } else { 'пауза' }
        Write-Host ("[{0}] {1} — {2} ({3}, {4:0}/{5:0} с) id: {6}" -f $track.source, $track.artist, $track.title, $state, $track.position, $track.duration, $track.app)
    }
    $chosen = Select-Track $list
    if ($chosen) { Write-Host "В статус пойдёт: $(Get-Key $chosen)" }
    exit 0
}

if (-not ([string]$config.clientId -match '^\d{15,}$')) {
    Write-Log 'В config.json не указан clientId — ID приложения с discord.com/developers (см. README).'
    exit 1
}

# Одна копия на пользователя: автозапуск и ручной запуск не спорят за статус.
$isFirst = $false
$mutex = New-Object System.Threading.Mutex($true, 'Local\discord-nowcast', [ref]$isFirst)
if (-not $isFirst) {
    Write-Log 'discord-nowcast уже запущен.'
    exit 0
}

Set-Content -LiteralPath $logPath -Value '' -Encoding UTF8
Write-Log 'discord-nowcast запущен. Ctrl+C — выход.'

$sentKey = $null        # что сейчас в статусе: ключ трека или 'none'
$sentStart = $null
$sentPlaying = $null
$sentAt = [DateTime]::MinValue
$nextConnect = [DateTime]::MinValue

try {
    while ($true) {
        if (-not $script:pipe -and [DateTime]::Now -ge $nextConnect) {
            if (Connect-Discord) { $sentKey = $null } else { $nextConnect = [DateTime]::Now.AddSeconds(15) }
        }

        $track = $null
        try { $track = Select-Track (Get-Sessions) } catch { }

        if ($script:pipe) {
            $key = if ($track) { Get-Key $track } else { 'none' }
            $startedAt = if ($track) { Get-StartedAt $track } else { $null }
            $playing = if ($track) { $track.isPlaying } else { $null }
            # Перемотка: начало трека сдвинулось больше чем на 3 секунды.
            $jumped = $null -ne $startedAt -and $null -ne $sentStart -and [math]::Abs($startedAt - $sentStart) -gt 3000
            $changed = $key -ne $sentKey -or $playing -ne $sentPlaying -or $jumped
            # Discord пропускает не больше 5 обновлений за 20 секунд.
            if ($changed -and ([DateTime]::Now - $sentAt).TotalSeconds -ge 4) {
                try {
                    if ($track) {
                        Set-Activity (New-Activity $track $startedAt)
                        Write-Log "Статус: $($track.artist) — $($track.title) [$($track.source)]"
                    }
                    else {
                        Set-Activity $null
                        Write-Log 'Статус очищен: ничего не играет.'
                    }
                    $script:shownKey = if ($track) { $key } else { $null }
                    $sentKey = $key
                    $sentStart = $startedAt
                    $sentPlaying = $playing
                    $sentAt = [DateTime]::Now
                }
                catch {
                    Write-Log "Связь с Discord потеряна: $($_.Exception.Message)"
                    Disconnect-Discord
                    $nextConnect = [DateTime]::Now.AddSeconds(5)
                }
            }
        }

        Start-Sleep -Seconds 1
    }
}
finally {
    if ($script:pipe) {
        try { Set-Activity $null } catch { }
        Disconnect-Discord
    }
    $mutex.ReleaseMutex()
}
