"""Discord Rich Presence Activity builder."""
import urllib.parse
from typing import Dict, Any, Optional
from nowcast.models import Track
from nowcast.settings import Settings
from nowcast.covers import CoverResolver
from nowcast import text

class ActivityBuilder:
    def __init__(self, covers: CoverResolver):
        self.covers = covers
        self.use_links = True

    def build(self, track: Track, started_at: Optional[int], settings: Settings) -> Dict[str, Any]:
        source = track.source.name
        artist = track.artist if track.artist else source
        act_type = 3 if track.source.is_video else 2
        activity: Dict[str, Any] = {
            "type": act_type,
            "instance": False
        }

        # Status display type: 0: App, 1: State line, 2: Details line
        if settings.in_list == "artist":
            self._set_lines(activity, track.title, artist, 1)
        elif settings.in_list == "title":
            self._set_lines(activity, track.title, artist, 2)
        elif settings.in_list == "app":
            self._set_lines(activity, track.title, artist, 0)
        else:
            line = f"{track.artist} — {track.title}" if track.artist else track.title
            self._set_lines(activity, line, source, 1)

        state = activity.get("state", "")
        if not track.is_playing:
            activity["state"] = self._limit(f"{state} · {text.PAUSED}")

        assets: Dict[str, Any] = {}
        cover_url = self.covers.find(track, settings.cover_search) if settings.covers else None
        image = cover_url or (settings.image if settings.image else None)

        if image:
            assets["large_image"] = image
            caption = track.album if track.album else (None if state.startswith(source) else source)
            if caption:
                assets["large_text"] = self._limit(caption)

        if assets:
            activity["assets"] = assets

        if started_at is not None and track.is_playing and track.duration > 0:
            activity["timestamps"] = {
                "start": started_at,
                "end": started_at + int(track.duration * 1000)
            }

        button = self._create_button(track)
        if settings.buttons:
            activity["buttons"] = [button]

        url = button.get("url", "")
        if self.use_links and len(url) <= 256:
            activity["details_url"] = url
            if "large_image" in assets:
                assets["large_url"] = url

        return activity

    def _set_lines(self, activity: Dict[str, Any], details: str, state: str, in_list: int):
        activity["details"] = self._limit(details)
        activity["state"] = self._limit(state)
        activity["status_display_type"] = in_list

    def _create_button(self, track: Track) -> Dict[str, str]:
        words = track.artist if (track.source.name == "Twitch" and track.artist) else f"{track.artist} {track.title}"
        query = urllib.parse.quote(words.strip())
        has_own = track.source.search is not None
        template = text.WATCH_ON if track.source.is_video else text.LISTEN_ON
        label = template.format(track.source.name) if has_own else text.FIND_ON_YOUTUBE
        base_url = track.source.search if has_own else "https://www.youtube.com/results?search_query="
        url = base_url + query
        return {
            "label": self._limit(label, 32),
            "url": url
        }

    def _limit(self, s: str, max_len: int = 128) -> str:
        s = s.strip()
        if len(s) > max_len:
            s = s[:max_len - 1] + "…"
        while len(s) < 2:
            s += "⠀"
        return s
