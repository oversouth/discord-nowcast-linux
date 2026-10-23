"""Configuration manager for discord-nowcast."""
import json
import os
from dataclasses import dataclass, field
from typing import Dict, List, Optional
from nowcast.log import Logger
from nowcast.models import SourceInfo

DEFAULT_CLIENT_ID = "1562901782579912804"
CONFIG_DIR = os.path.expanduser("~/.config/discord-nowcast")
CONFIG_PATH = os.path.join(CONFIG_DIR, "config.json")

@dataclass
class Settings:
    client_id: str = DEFAULT_CLIENT_ID
    in_list: str = "source"  # source, artist, title, app
    show_paused: bool = False
    covers: bool = True
    cover_search: bool = True
    buttons: bool = True
    check_updates: bool = True
    image: str = ""
    hide: List[str] = field(default_factory=list)
    names: Dict[str, str] = field(default_factory=dict)
    # Yandex Music account support
    yandex_token: str = ""
    yandex_login: str = ""
    yandex_user_id: str = ""

    @classmethod
    def load(cls) -> "Settings":
        settings = cls()
        os.makedirs(CONFIG_DIR, exist_ok=True)
        if not os.path.exists(CONFIG_PATH):
            settings.save()
            return settings
        try:
            with open(CONFIG_PATH, "r", encoding="utf-8") as f:
                data = json.load(f)
            settings.client_id = data.get("clientId", settings.client_id).strip() or DEFAULT_CLIENT_ID
            settings.in_list = data.get("inList", settings.in_list)
            settings.show_paused = bool(data.get("showPaused", settings.show_paused))
            settings.covers = bool(data.get("covers", settings.covers))
            settings.cover_search = bool(data.get("coverSearch", settings.cover_search))
            settings.buttons = bool(data.get("buttons", settings.buttons))
            settings.check_updates = bool(data.get("checkUpdates", settings.check_updates))
            settings.image = data.get("image", settings.image)
            settings.hide = list(data.get("hide", settings.hide))
            settings.names = dict(data.get("names", settings.names))
            settings.yandex_token = data.get("yandexToken", settings.yandex_token).strip()
            settings.yandex_login = data.get("yandexLogin", settings.yandex_login).strip()
            settings.yandex_user_id = data.get("yandexUserId", settings.yandex_user_id).strip()
        except Exception as e:
            Logger.write(f"config.json could not be read, using defaults: {e}")
        return settings

    def save(self):
        os.makedirs(CONFIG_DIR, exist_ok=True)
        data = {
            "clientId": self.client_id,
            "inList": self.in_list,
            "showPaused": self.show_paused,
            "covers": self.covers,
            "coverSearch": self.cover_search,
            "buttons": self.buttons,
            "checkUpdates": self.check_updates,
            "image": self.image,
            "hide": self.hide,
            "names": self.names,
            "yandexToken": self.yandex_token,
            "yandexLogin": self.yandex_login,
            "yandexUserId": self.yandex_user_id,
        }
        with open(CONFIG_PATH, "w", encoding="utf-8") as f:
            json.dump(data, f, indent=2, ensure_ascii=False)

    def client_id_for(self, source: SourceInfo) -> str:
        if self.client_id != DEFAULT_CLIENT_ID or not source.client_id:
            return self.client_id
        return source.client_id

    def hides(self, app: str, source_name: str) -> bool:
        app_lower = app.lower()
        src_lower = source_name.lower()
        for word in self.hide:
            w = word.lower()
            if w in app_lower or w in src_lower:
                return True
        return False

    def set_hidden(self, app: str, source_name: str, is_hidden: bool):
        if is_hidden:
            if not self.hides(app, source_name):
                self.hide.append(source_name)
        else:
            app_lower = app.lower()
            src_lower = source_name.lower()
            self.hide = [
                w for w in self.hide
                if w.lower() not in app_lower and w.lower() not in src_lower
            ]
