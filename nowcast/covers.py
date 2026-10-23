"""Track cover resolver via iTunes Search API and image hosting."""
import json
import re
import urllib.request
import urllib.parse
from typing import Optional, Dict, Any, Set
from nowcast.models import Track
from nowcast.log import Logger
from nowcast import text

class CoverResolver:
    def __init__(self):
        self.cache: Dict[str, str] = {}
        self.searched: Set[str] = set()

    def find(self, track: Track, search_itunes: bool) -> Optional[str]:
        if track.key in self.cache:
            return self.cache[track.key]

        # 1. If thumbnail is already an HTTP URL (from Yandex API or MPRIS artUrl)
        if isinstance(track.thumbnail, str) and track.thumbnail.startswith("http"):
            self.cache[track.key] = track.thumbnail
            return track.thumbnail

        # 2. iTunes Search lookup
        if search_itunes and track.key not in self.searched:
            self.searched.add(track.key)
            url = self._search_itunes(track)
            if url:
                self.cache[track.key] = url
                return url

        # 3. If file:// URL thumbnail, we could read local art
        if isinstance(track.thumbnail, str) and track.thumbnail.startswith("file://"):
            local_path = urllib.parse.unquote(track.thumbnail[7:])
            uploaded = self._upload_local_image(local_path)
            if uploaded:
                self.cache[track.key] = uploaded
                return uploaded

        return None

    def _normalize(self, s: str) -> str:
        s = re.sub(r"[\s\-_.,/()\[\]]+", " ", s)
        return s.strip().lower()

    def _search_itunes(self, track: Track) -> Optional[str]:
        if track.source.is_video or not track.artist or not track.title:
            return None

        artist_norm = self._normalize(re.split(r",|&|\bfeat\.?|\bft\.?|\bx\b", track.artist, flags=re.IGNORECASE)[0])
        title_norm = self._normalize(track.title)
        if len(artist_norm) < 2 or len(title_norm) < 2:
            return None

        clean_title = re.sub(r"\s*[(\[].*?[)\]]", "", track.title)
        query = urllib.parse.quote(f"{track.artist} {clean_title}")
        countries = ["RU", "US"] if text.RU else ["US"]

        for country in countries:
            url = f"https://itunes.apple.com/search?entity=song&limit=10&country={country}&term={query}"
            headers = {"User-Agent": "discord-nowcast-linux/2.0"}
            req = urllib.request.Request(url, headers=headers)
            try:
                with urllib.request.urlopen(req, timeout=3) as resp:
                    if resp.status == 200:
                        data = json.loads(resp.read().decode("utf-8"))
                        results = data.get("results", [])
                        for res in results:
                            found_artist = self._normalize(res.get("artistName", ""))
                            found_title = self._normalize(res.get("trackName", ""))
                            art = res.get("artworkUrl100")
                            if not art:
                                continue
                            if (found_artist.startswith(artist_norm) or artist_norm.startswith(found_artist)) and \
                               (found_title.startswith(title_norm) or title_norm.startswith(found_title)):
                                return art.replace("100x100bb", "512x512bb")
            except Exception as e:
                Logger.write(f"iTunes search failed: {e}")
        return None

    def _upload_local_image(self, file_path: str) -> Optional[str]:
        try:
            with open(file_path, "rb") as f:
                data = f.read()
            # Upload to 0x0.st or litterbox
            boundary = "----nowcastlinuxboundary"
            body = (
                f"--{boundary}\r\n"
                f'Content-Disposition: form-data; name="fileToUpload"; filename="cover.jpg"\r\n'
                f"Content-Type: image/jpeg\r\n\r\n"
            ).encode("utf-8") + data + f"\r\n--{boundary}\r\nContent-Disposition: form-data; name=\"reqtype\"\r\n\r\nfileupload\r\n--{boundary}\r\nContent-Disposition: form-data; name=\"time\"\r\n\r\n72h\r\n--{boundary}--\r\n".encode("utf-8")

            req = urllib.request.Request(
                "https://litterbox.catbox.moe/resources/internals/api.php",
                data=body,
                headers={
                    "Content-Type": f"multipart/form-data; boundary={boundary}",
                    "User-Agent": "discord-nowcast-linux/2.0",
                }
            )
            with urllib.request.urlopen(req, timeout=8) as resp:
                res = resp.read().decode("utf-8").strip()
                if res.startswith("https://"):
                    return res
        except Exception as e:
            Logger.write(f"Cover upload error: {e}")
        return None
