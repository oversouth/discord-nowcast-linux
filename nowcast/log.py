"""Logging subsystem for discord-nowcast."""
import os
import sys
from datetime import datetime

LOG_DIR = os.path.expanduser("~/.config/discord-nowcast")
LOG_FILE = os.path.join(LOG_DIR, "log.txt")

class Logger:
    @staticmethod
    def start():
        os.makedirs(LOG_DIR, exist_ok=True)
        try:
            with open(LOG_FILE, "w", encoding="utf-8") as f:
                f.write("")
        except OSError:
            pass

    @staticmethod
    def write(text: str):
        try:
            os.makedirs(LOG_DIR, exist_ok=True)
            if os.path.exists(LOG_FILE) and os.path.getsize(LOG_FILE) > 1024 * 1024:
                with open(LOG_FILE, "w", encoding="utf-8") as f:
                    f.write("")
            ts = datetime.now().strftime("%H:%M:%S")
            line = f"{ts} {text}\n"
            with open(LOG_FILE, "a", encoding="utf-8") as f:
                f.write(line)
            sys.stdout.write(line)
            sys.stdout.flush()
        except Exception:
            pass
