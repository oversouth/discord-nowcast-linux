"""Localized interface strings for Russian and English."""
import os

_lang = os.environ.get("LANG", "").lower()
_lc_all = os.environ.get("LC_ALL", "").lower()
RU = "ru" in _lang or "ru" in _lc_all

def t(ru: str, en: str) -> str:
    return ru if RU else en

ALREADY_RUNNING = t(
    "discord-nowcast уже работает.",
    "discord-nowcast is already running."
)
RUNNING = t(
    "discord-nowcast запущен в фоне.",
    "discord-nowcast is running in background."
)
NOTHING_PLAYING = t("Ничего не играет", "Nothing is playing")
DISCORD_OFF = t("Discord не запущен", "Discord is not running")
HIDDEN_UNTIL = t("Статус скрыт до {0}", "Status hidden until {0}")
MEDIA_UNAVAILABLE = t("В системе нет активных медиасессий (MPRIS/Yandex)", "No active media sessions found (MPRIS/Yandex)")
STARTING = t("Запускается…", "Starting…")

PAUSED = t("пауза", "paused")
PLAYING = t("играет", "playing")

LISTEN_ON = t("Слушать на {0}", "Listen on {0}")
WATCH_ON = t("Смотреть на {0}", "Watch on {0}")
FIND_ON_YOUTUBE = t("Найти на YouTube", "Find on YouTube")
