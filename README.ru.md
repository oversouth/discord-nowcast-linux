<div align="center">

# discord-nowcast

**Трек из любого приложения в статусе Discord.**

Яндекс Музыка, Spotify, SoundCloud, VK, Telegram, YouTube в браузере, AIMP, локальные файлы: всё, что играет в Windows.

[![release](https://img.shields.io/github/v/release/playsin333/discord-nowcast?color=5865f2)](https://github.com/playsin333/discord-nowcast/releases/latest)
[![downloads](https://img.shields.io/github/downloads/playsin333/discord-nowcast/total?color=3d3c38)](https://github.com/playsin333/discord-nowcast/releases)
![platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-3d3c38)
[![license](https://img.shields.io/badge/license-MIT-3d3c38)](LICENSE)

[English](README.md) · **Русский**

**[⬇ Скачать](https://github.com/playsin333/discord-nowcast/releases/latest/download/discord-nowcast.zip)** · [Обсуждения](https://github.com/playsin333/discord-nowcast/discussions) · [Сообщить о баге](https://github.com/playsin333/discord-nowcast/issues/new?template=bug.yml)

</div>

## Что получится

В профиле и в списке участников сервера появляется «Слушает …», как у Spotify:

- исполнитель и название трека;
- откуда играет: Яндекс Музыка, Telegram, SoundCloud, Chrome;
- обложка — та же картинка, что в самом плеере;
- полоска времени трека, если приложение его сообщает.

На паузе или когда ничего не играет, статус убирается сам.

## Установка

Ничего ставить не нужно: программа работает на встроенном в Windows PowerShell.

1. **[Скачайте `discord-nowcast.zip`](https://github.com/playsin333/discord-nowcast/releases/latest/download/discord-nowcast.zip)** и распакуйте в папку, которую не будете удалять, например `Документы\discord-nowcast`.
2. **Запустите `install.cmd`.** Программа начнёт работать без окна и будет стартовать вместе с Windows.

Если Windows пишет «Система Windows защитила ваш компьютер», нажмите **Подробнее → Выполнить в любом случае**: файлы скачаны из интернета и не подписаны.

Discord должен быть запущен (приложение на компьютере, не сайт), а в **Настройки → Конфиденциальность активности** включено «Делиться обнаруженной активностью».

| Файл | Что делает |
| --- | --- |
| `install.cmd` | запускает в фоне и добавляет в автозагрузку |
| `uninstall.cmd` | останавливает и убирает из автозагрузки |
| `start.cmd` | запускает в окне, видно, что отправляется в Discord |
| `check.cmd` | показывает, что сейчас видит Windows, без Discord |

Обновление: скачайте новый архив, распакуйте поверх старой папки и снова запустите `install.cmd`.

## Настройки

Всё в `config.json`. После правки запустите `install.cmd` ещё раз, чтобы программа перечитала файл.

| Настройка | Что значит |
| --- | --- |
| `clientId` | приложение Discord, чьё имя стоит после «Слушает». Встроенное работает сразу; чтобы было своё имя, создайте приложение на [discord.com/developers/applications](https://discord.com/developers/applications) (**New Application**) и впишите сюда его **Application ID** |
| `inList` | что видно в списке участников после «Слушает»: `source` — откуда играет (по умолчанию), `artist` — исполнитель, `title` — трек, `app` — имя приложения Discord |
| `showPaused` | `true` — не убирать статус на паузе, а писать «пауза» |
| `covers` | `false` — без обложек |
| `lengths` | `false` — не искать длину трека в Deezer для плееров, которые её не сообщают |
| `image` | ссылка на картинку, которая показывается, если плеер не отдал обложку |
| `hide` | приложения, которые не показывать: `["chrome", "Telegram"]` |
| `names` | свои названия источников: `{"chrome": "YouTube"}` |

Пример:

```json
{
  "clientId": "1562901782579912804",
  "inList": "source",
  "showPaused": false,
  "covers": true,
  "lengths": true,
  "image": "",
  "hide": ["Claude"],
  "names": { "chrome": "YouTube" }
}
```

## Как это работает

Windows сама знает, что играет: это та плашка с треком, которая появляется при нажатии кнопок громкости. `nowcast.ps1` раз в секунду читает её (системные медиасессии) и, когда трек меняется, отправляет статус в Discord через его локальный канал, тот же, через который статус ставят игры.

- **Название источника** берётся из идентификатора приложения. Если сайт установлен как приложение (SoundCloud или YouTube Music через «Установить» в Chrome или Edge), берётся его имя из меню «Пуск». Поэтому в статусе будет SoundCloud, а не Chrome.
- **Обложка** — картинка, которую плеер отдаёт Windows вместе с треком. Discord показывает картинки только по ссылке из интернета, поэтому она выкладывается на временный хостинг [litterbox.catbox.moe](https://litterbox.catbox.moe) на трое суток. Если плеер картинку не отдал, в статусе иконка приложения Discord.
- **Частота.** Discord принимает не больше пяти обновлений за 20 секунд, поэтому статус меняется не чаще раза в четыре секунды.

## Ограничения

- Приложение должно сообщать Windows о треке. Почти все так делают; если `check.cmd` его не видит, программа его тоже не увидит.
- В обычной вкладке браузера видно только сам браузер, а не сайт: Chrome, а не SoundCloud. Установите сайт как приложение или задайте своё название в `names`.
- Не каждый плеер отдаёт обложку. Telegram, например, часто не отдаёт, и тогда видна иконка приложения.
- Для полоски времени нужна длина трека. Если плеер её не сообщает (Telegram), длина ищется в Deezer, а время программа считает сама; перемотку в таком плеере не видно, а для трека, которого нет в Deezer, показывается только прошедшее время.

## Приватность

Исполнитель и название трека уходят в Discord (это и есть статус) и в Deezer, только для плееров, которые не сообщают длину трека. Картинка обложки из плеера выкладывается на litterbox.catbox.moe; `"covers": false` это отключает. Больше ничего никуда не отправляется.

Лог текущего запуска: `%TEMP%\discord-nowcast.log`.

## Вопросы, баги, идеи

- [Обсуждения](https://github.com/playsin333/discord-nowcast/discussions): вопросы, идеи, скриншоты своего статуса.
- [Сообщить о баге](https://github.com/playsin333/discord-nowcast/issues/new?template=bug.yml): больше всего помогают вывод `check.cmd` и лог.

Изменения — в [CHANGELOG.md](CHANGELOG.md).

Лицензия: [MIT](LICENSE).
