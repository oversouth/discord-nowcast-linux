"""Core polling and synchronization engine."""
import time
import threading
from datetime import datetime, timedelta
from typing import Optional, List, Dict, Any
from nowcast.models import Track
from nowcast.settings import Settings
from nowcast.discord import DiscordIPC
from nowcast.mpris import MPRISReader
from nowcast.yandex import YandexClient
from nowcast.covers import CoverResolver
from nowcast.activity import ActivityBuilder
from nowcast.log import Logger
from nowcast import text

class Engine:
    def __init__(self):
        self.settings = Settings.load()
        self.discord = DiscordIPC()
        self.mpris = MPRISReader()
        self.yandex = YandexClient(self.settings.yandex_token, self.settings.yandex_user_id)
        self.covers = CoverResolver()
        self.activity_builder = ActivityBuilder(self.covers)

        self.running = False
        self.thread: Optional[threading.Thread] = None
        self.hidden_until: Optional[datetime] = None
        self.sessions: List[Track] = []
        self.status = text.STARTING

        # State tracking
        self.sent_key: Optional[str] = None
        self.sent_start: Optional[int] = None
        self.sent_playing: Optional[bool] = None
        self.sent_at: float = 0.0
        self.next_connect: float = 0.0
        self.shown_key: Optional[str] = None

    def start(self):
        self.running = True
        self.thread = threading.Thread(target=self._loop, name="nowcast-engine", daemon=True)
        self.thread.start()

    def stop(self):
        self.running = False
        if self.thread and self.thread.is_alive():
            self.thread.join(timeout=2.0)
        if self.discord.is_connected:
            try:
                self.discord.set_activity(None)
            except Exception:
                pass
            self.discord.dispose()

    def hide(self, duration: timedelta):
        self.hidden_until = datetime.now() + duration

    def show(self):
        self.hidden_until = None

    def _loop(self):
        Logger.write("Engine started")
        while self.running:
            try:
                self._tick()
            except Exception as e:
                Logger.write(f"Engine tick error: {e}")
            time.sleep(1.0)

    def _tick(self):
        # 1. Reload settings if updated
        # 2. Gather sessions from MPRIS and Yandex
        sessions: List[Track] = []
        try:
            mpris_tracks = self.mpris.read_tracks(self.settings)
            sessions.extend(mpris_tracks)
        except Exception as e:
            Logger.write(f"MPRIS read error: {e}")

        # Check Yandex Music account
        if self.yandex.is_configured():
            try:
                yandex_track = self.yandex.fetch_current_track()
                if yandex_track:
                    # Put Yandex account track if playing
                    sessions.insert(0, yandex_track)
            except Exception as e:
                Logger.write(f"Yandex Music fetch error: {e}")

        self.sessions = sessions

        is_hidden = self.hidden_until is not None and datetime.now() < self.hidden_until
        track = None if is_hidden else self._pick_track(sessions)

        # Discord connection handling
        if self.discord.is_connected and not self.discord.is_alive:
            Logger.write("Discord connection lost")
            self.discord.dispose()
            self.next_connect = time.time()

        if track:
            target_client_id = self.settings.client_id_for(track.source)
            if self.discord.is_connected and self.discord.client_id != target_client_id:
                Logger.write(f"Switching Discord client ID from {self.discord.client_id} to {target_client_id}")
                self.discord.dispose()

            if not self.discord.is_connected and time.time() >= self.next_connect:
                if not self.discord.connect(target_client_id):
                    self.next_connect = time.time() + 3.0

        if not self.discord.is_connected:
            self.status = text.DISCORD_OFF
            self.sent_key = None
            return

        # Prepare activity state
        if is_hidden:
            self.status = text.HIDDEN_UNTIL.format(self.hidden_until.strftime("%H:%M"))
        elif track:
            self.status = f"{track.artist} — {track.title}" if track.artist else track.title
        else:
            self.status = text.NOTHING_PLAYING

        key = track.key if track else None
        playing = track.is_playing if track else False
        start = self._calculate_started_at(track) if track else None

        changed = (key != self.sent_key or playing != self.sent_playing)
        # Discord rate limits: 5 updates per 20 seconds
        if not changed or (time.time() - self.sent_at) < 3.0:
            return

        try:
            if not track:
                self.discord.set_activity(None)
                Logger.write("Discord status cleared")
            else:
                activity = self.activity_builder.build(track, start, self.settings)
                err = self.discord.set_activity(activity)
                if err and self.activity_builder.use_links:
                    self.activity_builder.use_links = False
                    activity = self.activity_builder.build(track, start, self.settings)
                    self.discord.set_activity(activity)
                Logger.write(f"Status updated: {self.status} [{track.source.name}]")

            self.shown_key = key
            self.sent_key = key
            self.sent_start = start
            self.sent_playing = playing
            self.sent_at = time.time()
        except Exception as e:
            Logger.write(f"Failed to push status to Discord: {e}")
            self.discord.dispose()
            self.next_connect = time.time() + 5.0

    def _pick_track(self, sessions: List[Track]) -> Optional[Track]:
        visible = [t for t in sessions if not self.settings.hides(t.app, t.source.name)]
        pool = [t for t in visible if t.is_playing]
        if not pool and self.settings.show_paused:
            pool = visible
        if not pool:
            return None
        timed = [t for t in pool if t.duration > 0]
        if timed:
            pool = timed
        for t in pool:
            if t.key == self.shown_key:
                return t
        for t in pool:
            if t.is_current:
                return t
        return pool[0]

    def _calculate_started_at(self, track: Track) -> Optional[int]:
        if track.duration <= 0:
            return None
        now_ms = int(time.time() * 1000)
        pos_ms = track.position * 1000.0
        if track.is_playing and track.updated_at > 0:
            pos_ms += max(0, now_ms - track.updated_at)
        pos_ms = min(pos_ms, track.duration * 1000.0)
        return int(now_ms - pos_ms)
