"""Core models and structures for nowcast."""
from dataclasses import dataclass
from typing import Optional, Any

@dataclass
class SourceInfo:
    name: str
    search: Optional[str] = None
    client_id: Optional[str] = None
    is_video: bool = False
    is_browser: bool = False

@dataclass
class Track:
    app: str
    source: SourceInfo
    artist: str
    title: str
    album: str = ""
    is_playing: bool = False
    is_current: bool = False
    position: float = 0.0
    duration: float = 0.0
    updated_at: int = 0
    thumbnail: Optional[Any] = None

    @property
    def key(self) -> str:
        return f"{self.app}|{self.artist}|{self.title}"
