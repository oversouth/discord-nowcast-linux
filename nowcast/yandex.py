"""Yandex Music account connector via Web API."""
import json
import time
import urllib.request
import urllib.parse
from typing import Optional, Dict, Any, List
from nowcast.models import Track, SourceInfo
from nowcast.settings import Settings
from nowcast.log import Logger
from nowcast import text

class YandexClient:
    def __init__(self, token: str, user_id: str = ""):
        self.token = token.strip()
        self.user_id = user_id.strip()
        self._last_error_time = 0.0

    def is_configured(self) -> bool:
        return bool(self.token)

    def fetch_user_id(self) -> Optional[str]:
        if self.user_id:
            return self.user_id
        url = "https://login.yandex.ru/info"
        data = self._make_request(url)
        if data and "id" in data:
            self.user_id = str(data["id"])
            return self.user_id
        return None

    def fetch_current_track(self) -> Optional[Track]:
        if not self.is_configured():
            return None

        uid = self.fetch_user_id()

        # 1. Try user likes / recent tracks
        if uid:
            url = f"https://api.music.yandex.net/users/{uid}/likes/tracks"
            data = self._make_request(url)
            if data and "result" in data:
                lib = data["result"].get("library", {})
                tracks = lib.get("tracks", [])
                if tracks:
                    first = tracks[0]
                    track_id = str(first.get("id"))
                    ts_str = first.get("timestamp")
                    # Fetch full track metadata
                    full_track = self.fetch_track_info(track_id)
                    if full_track:
                        return full_track

        return None

    def fetch_track_info(self, track_id: str) -> Optional[Track]:
        url = f"https://api.music.yandex.net/tracks/{track_id}"
        data = self._make_request(url)
        if not data or "result" not in data or not data["result"]:
            return None

        t = data["result"][0]
        title = t.get("title", "")
        artists_list = t.get("artists", [])
        artists = ", ".join(a.get("name", "") for a in artists_list if a.get("name"))
        if not title and not artists:
            return None

        albums_list = t.get("albums", [])
        album_name = albums_list[0].get("title", "") if albums_list else ""
        duration_ms = t.get("durationMs", 0)
        duration_sec = duration_ms / 1000.0 if duration_ms else 0.0

        cover_uri = t.get("coverUri") or (albums_list[0].get("coverUri") if albums_list else None)
        cover_url = None
        if cover_uri:
            cover_url = "https://" + cover_uri.replace("%%", "400x400")

        source = SourceInfo(
            name="Яндекс Музыка" if text.RU else "Yandex Music",
            search="https://music.yandex.ru/search?text=",
            client_id="1563159088265822392",
            is_video=False,
            is_browser=False
        )

        return Track(
            app="yandex-music-account",
            source=source,
            artist=artists,
            title=title,
            album=album_name,
            is_playing=True,
            is_current=True,
            position=0.0,
            duration=duration_sec,
            updated_at=int(time.time() * 1000),
            thumbnail=cover_url
        )

    def _make_request(self, url: str) -> Optional[Dict[str, Any]]:
        headers = {
            "Authorization": f"OAuth {self.token}",
            "User-Agent": "YandexMusic/Linux nowcast",
            "Accept": "application/json",
        }
        req = urllib.request.Request(url, headers=headers)
        try:
            with urllib.request.urlopen(req, timeout=4) as resp:
                if resp.status == 200:
                    return json.loads(resp.read().decode("utf-8"))
        except Exception as e:
            now = time.time()
            if now - self._last_error_time > 60:
                Logger.write(f"Yandex Music API request failed ({url}): {e}")
                self._last_error_time = now
        return None
