"""Rules and resolvers for media sources on Linux."""
import re
from typing import Optional, List
from nowcast.models import SourceInfo
from nowcast.settings import Settings
from nowcast import text

class Rule:
    def __init__(self, pattern: str, name: str, search: Optional[str] = None, client_id: Optional[str] = None, is_video: bool = False, is_browser: bool = False):
        self.regex = re.compile(pattern, re.IGNORECASE)
        self.name = name
        self.search = search
        self.client_id = client_id
        self.is_video = is_video
        self.is_browser = is_browser

RULES: List[Rule] = [
    Rule(r"yandex.*music|music.*yandex|яндекс.?музык", "Яндекс Музыка" if text.RU else "Yandex Music", "https://music.yandex.ru/search?text=", "1563159088265822392"),
    Rule(r"spotify", "Spotify", "https://open.spotify.com/search/", "1563159215709749389"),
    Rule(r"soundcloud", "SoundCloud", "https://soundcloud.com/search?q=", "1563159356940361779"),
    Rule(r"youtube.?music", "YouTube Music", "https://music.youtube.com/search?q=", "1563162689163886642"),
    Rule(r"youtube", "YouTube", "https://www.youtube.com/results?search_query=", "1563159477673263216", is_video=True),
    Rule(r"twitch", "Twitch", "https://www.twitch.tv/search?term=", "1563159932218646588", is_video=True),
    Rule(r"telegram|ayugram", "Telegram", None, "1563159812382920706"),
    Rule(r"vk.?music|vk музык|vkontakte|вконтакте|\bvk\b|\bвк\b", "VK Музыка" if text.RU else "VK Music", "https://vk.com/audio?q=", "1563159553619525723"),
    Rule(r"applemusic|apple music|itunes", "Apple Music", "https://music.apple.com/search?term=", "1563159657676021881"),
    Rule(r"amberol", "Amberol", None),
    Rule(r"rhythmbox", "Rhythmbox", None),
    Rule(r"clementine|strawberry", "Strawberry", None),
    Rule(r"mpv", "mpv", None),
    Rule(r"vlc", "VLC", None),
    Rule(r"yandex|^browser", "Яндекс Браузер" if text.RU else "Yandex Browser", is_browser=True),
    Rule(r"chrome|chromium", "Chrome", is_browser=True),
    Rule(r"msedge|microsoftedge", "Edge", is_browser=True),
    Rule(r"firefox", "Firefox", is_browser=True),
    Rule(r"opera", "Opera", is_browser=True),
    Rule(r"vivaldi", "Vivaldi", is_browser=True),
    Rule(r"brave", "Brave", is_browser=True),
]

def match_rule(identifier: str) -> Optional[Rule]:
    for rule in RULES:
        if rule.regex.search(identifier):
            return rule
    return None

def fallback_name(app_id: str) -> str:
    cleaned = re.sub(r"^org\.mpris\.MediaPlayer2\.", "", app_id, flags=re.IGNORECASE)
    parts = cleaned.split(".")
    name = parts[-1].capitalize() if parts else app_id
    return name or ("Музыка" if text.RU else "Music")

def resolve(app_id: str, settings: Settings) -> SourceInfo:
    custom_name = None
    app_lower = app_id.lower()
    for key, val in settings.names.items():
        if key.lower() in app_lower:
            custom_name = val
            break

    rule = match_rule(custom_name) if custom_name else match_rule(app_id)
    name = custom_name or (rule.name if rule else fallback_name(app_id))

    return SourceInfo(
        name=name,
        search=rule.search if rule else None,
        client_id=rule.client_id if rule else None,
        is_video=rule.is_video if rule else False,
        is_browser=rule.is_browser if rule and rule.name == name else False,
    )
