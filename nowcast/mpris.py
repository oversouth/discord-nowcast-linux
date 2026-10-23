"""Linux MPRIS2 media session reader using DBus."""
import os
import subprocess
from typing import List, Dict, Any, Optional
from nowcast.models import Track, SourceInfo
from nowcast.settings import Settings
from nowcast.sources import resolve, match_rule
from nowcast.log import Logger

class MPRISReader:
    def __init__(self):
        pass

    def list_players(self) -> List[str]:
        try:
            res = subprocess.run(
                ["busctl", "--user", "list", "--no-pager", "--no-legend"],
                capture_output=True, text=True, timeout=2
            )
            players = []
            for line in res.stdout.splitlines():
                parts = line.split()
                if parts:
                    name = parts[0]
                    if name.startswith("org.mpris.MediaPlayer2."):
                        players.append(name)
            return players
        except Exception:
            return []

    def get_player_metadata(self, player_bus_name: str) -> Dict[str, Any]:
        info: Dict[str, Any] = {
            "PlaybackStatus": "Stopped",
            "Metadata": {},
            "Position": 0.0,
        }
        try:
            status_res = subprocess.run(
                ["busctl", "--user", "get-property", player_bus_name,
                 "/org/mpris/MediaPlayer2", "org.mpris.MediaPlayer2.Player", "PlaybackStatus"],
                capture_output=True, text=True, timeout=1
            )
            if status_res.returncode == 0:
                parts = status_res.stdout.strip().split(maxsplit=1)
                if len(parts) > 1:
                    info["PlaybackStatus"] = parts[1].strip('"')

            pos_res = subprocess.run(
                ["busctl", "--user", "get-property", player_bus_name,
                 "/org/mpris/MediaPlayer2", "org.mpris.MediaPlayer2.Player", "Position"],
                capture_output=True, text=True, timeout=1
            )
            if pos_res.returncode == 0:
                parts = pos_res.stdout.strip().split(maxsplit=1)
                if len(parts) > 1:
                    try:
                        info["Position"] = float(parts[1]) / 1_000_000.0  # microseconds to seconds
                    except ValueError:
                        pass

            meta_res = subprocess.run(
                ["busctl", "--user", "call", player_bus_name,
                 "/org/mpris/MediaPlayer2", "org.freedesktop.DBus.Properties",
                 "Get", "ss", "org.mpris.MediaPlayer2.Player", "Metadata"],
                capture_output=True, text=True, timeout=2
            )
            if meta_res.returncode == 0:
                meta = self._parse_dbus_metadata(meta_res.stdout)
                info["Metadata"] = meta
        except Exception as e:
            Logger.write(f"Error querying MPRIS {player_bus_name}: {e}")
        return info

    def _parse_dbus_metadata(self, text: str) -> Dict[str, Any]:
        result = {}
        for line in text.splitlines():
            line = line.strip()
            if not line:
                continue
            if line.startswith('"xesam:title"'):
                parts = line.split(maxsplit=2)
                if len(parts) >= 3:
                    result["title"] = parts[2].strip('"')
            elif line.startswith('"xesam:artist"'):
                # Can be string or array
                parts = line.split(maxsplit=2)
                if len(parts) >= 3:
                    raw = parts[2].strip()
                    if raw.isdigit():
                        continue
                    result["artist"] = raw.strip('"')
            elif line.startswith('"xesam:album"'):
                parts = line.split(maxsplit=2)
                if len(parts) >= 3:
                    result["album"] = parts[2].strip('"')
            elif line.startswith('"mpris:artUrl"'):
                parts = line.split(maxsplit=2)
                if len(parts) >= 3:
                    result["artUrl"] = parts[2].strip('"')
            elif line.startswith('"mpris:length"'):
                parts = line.split(maxsplit=2)
                if len(parts) >= 3:
                    try:
                        result["length"] = float(parts[2]) / 1_000_000.0  # seconds
                    except ValueError:
                        pass
        return result

    def read_tracks(self, settings: Settings) -> List[Track]:
        tracks = []
        players = self.list_players()
        for p in players:
            data = self.get_player_metadata(p)
            meta = data.get("Metadata", {})
            title = meta.get("title", "")
            artist = meta.get("artist", "")
            if not title and not artist:
                continue
            source = resolve(p, settings)
            status = data.get("PlaybackStatus", "")
            is_playing = (status.lower() == "playing")
            pos = data.get("Position", 0.0)
            duration = meta.get("length", 0.0)
            art_url = meta.get("artUrl", None)

            track = Track(
                app=p,
                source=source,
                artist=artist,
                title=title,
                album=meta.get("album", ""),
                is_playing=is_playing,
                is_current=True,
                position=pos,
                duration=duration,
                updated_at=0,
                thumbnail=art_url
            )
            tracks.append(track)
        return tracks
