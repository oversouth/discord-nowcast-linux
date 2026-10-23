"""Linux systemd user service and autostart manager."""
import os
from nowcast.log import Logger

AUTOSTART_DIR = os.path.expanduser("~/.config/autostart")
DESKTOP_FILE = os.path.join(AUTOSTART_DIR, "discord-nowcast.desktop")

SYSTEMD_DIR = os.path.expanduser("~/.config/systemd/user")
SERVICE_FILE = os.path.join(SYSTEMD_DIR, "discord-nowcast.service")

DESKTOP_ENTRY_CONTENT = """[Desktop Entry]
Type=Application
Name=discord-nowcast
Comment=Show playing music in Discord status
Exec=discord-nowcast
Icon=discord-nowcast
Terminal=false
Categories=AudioVideo;Audio;
X-GNOME-Autostart-enabled=true
"""

SYSTEMD_SERVICE_CONTENT = """[Unit]
Description=discord-nowcast - Discord Rich Presence music status
After=graphical-session.target network.target

[Service]
Type=simple
ExecStart=%h/.local/bin/discord-nowcast
Restart=on-failure
RestartSec=5

[Install]
WantedBy=default.target
"""

class LinuxStartup:
    @staticmethod
    def is_autostart_enabled() -> bool:
        return os.path.exists(DESKTOP_FILE) or os.path.exists(SERVICE_FILE)

    @staticmethod
    def enable():
        try:
            os.makedirs(AUTOSTART_DIR, exist_ok=True)
            with open(DESKTOP_FILE, "w", encoding="utf-8") as f:
                f.write(DESKTOP_ENTRY_CONTENT)
            os.chmod(DESKTOP_FILE, 0o755)

            os.makedirs(SYSTEMD_DIR, exist_ok=True)
            with open(SERVICE_FILE, "w", encoding="utf-8") as f:
                f.write(SYSTEMD_SERVICE_CONTENT)
            Logger.write("Autostart entry and systemd service created")
        except Exception as e:
            Logger.write(f"Failed to enable autostart: {e}")

    @staticmethod
    def disable():
        try:
            if os.path.exists(DESKTOP_FILE):
                os.remove(DESKTOP_FILE)
            if os.path.exists(SERVICE_FILE):
                os.remove(SERVICE_FILE)
            Logger.write("Autostart entry and systemd service removed")
        except Exception as e:
            Logger.write(f"Failed to disable autostart: {e}")
