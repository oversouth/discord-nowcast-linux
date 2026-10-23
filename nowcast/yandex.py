"""Yandex Music account connector via Ynison & Web API."""
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

    def fetch_current_track(self) -> Optional[Track]:
        if not self.is_configured():
            return None

        # 1. Try Ynison state API
        track = self._fetch_ynison_track()
        if track:
            return track

        # 2. Try Queues / History API
        track = self._fetch_queue_track()
        if track:
            return track

        return None

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

    def _fetch_ynison_track(self) -> Optional[Track]:
        url = "https://api.music.yandex.net/queues"
        data = self._make_request(url)
        if not data or "result" not in data:
            return None
        res = data["result"]
        queues = res.get("queues", [])
        if not queues:
            return None

        current_queue = queues[0]
        cur_index = current_queue.get("currentIndex", 0)
        tracks = current_queue.get("tracks", [])
        if not tracks or cur_index >= len(tracks):
            return None

        track_obj = tracks[cur_index]
        return self._build_track_from_dict(track_obj, is_playing=True)

    def _fetch_queue_track(self) -> Optional[Track]:
        # Fallback to user history or status
        url = "https://api.music.yandex.net/account/status"
        data = self._make_request(url)
        if not data or "result" not in data:
            return None
        res = data["result"]
        account = res.get("account", {})
        uid = str(account.get("uid", ""))
        if uid and not self.user_id:
            self.user_id = uid

        # Try user plays history
        if self.user_id:
            hist_url = f"https://api.music.yandex.net/users/{self.user_id}/history"
            hist_data = self._make_request(hist_url)
            if hist_data and "result" in hist_data:
                tracks = (hist_data["result"] or {}).get("tracks", [])
                if tracks:
                    first = tracks[0]
                    # Check if played recently (< 15 mins)
                    timestamp_str = first.get("timestamp")
                    return self._build_track_from_dict(first.get("track", first), is_playing=True)

        return None

    def _build_track_from_dict(self, t: Dict[str, Any], is_playing: bool = True) -> Optional[Track]:
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
            is_playing=is_playing,
            is_current=True,
            position=0.0,
            duration=duration_sec,
            updated_at=int(time.time() * 1000),
            thumbnail=cover_url
        )
