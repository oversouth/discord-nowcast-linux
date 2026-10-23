"""CLI entry point and runtime controller."""
import argparse
import os
import sys
import time
import signal
from nowcast.log import Logger, LOG_FILE
from nowcast.settings import Settings, CONFIG_PATH
from nowcast.engine import Engine
from nowcast.startup import LinuxStartup
from nowcast.yandex import YandexClient

VERSION = "2.0.0-linux"

def print_status(engine: Engine):
    print(f"Status: {engine.status}")
    print(f"Active sessions ({len(engine.sessions)}):")
    for s in engine.sessions:
        playing = "▶" if s.is_playing else "⏸"
        print(f"  {playing} [{s.source.name}] {s.artist} - {s.title} ({s.app})")

def setup_yandex(settings: Settings):
    print("=== Connect Yandex Music Account ===")
    print("To connect your Yandex Music account, enter your OAuth token.")
    print("How to get it:")
    print("1. Visit https://oauth.yandex.ru/authorize?response_type=token&client_id=23cabbbdc6cd418abb4b46c32ba3e112")
    print("   or extract it from cookies / Yandex Music web headers.")
    token = input("Enter Yandex OAuth Token: ").strip()
    if token:
        settings.yandex_token = token
        client = YandexClient(token)
        print("Testing token...")
        track = client.fetch_current_track()
        if track:
            print(f"Successfully connected! Current/Last track: {track.artist} - {track.title}")
        else:
            print("Token saved. (No current playing queue found right now).")
        settings.save()
        print(f"Configuration saved to {CONFIG_PATH}")
    else:
        print("Empty token, cancelled.")

def main():
    parser = argparse.ArgumentParser(description="discord-nowcast (Linux edition)")
    parser.add_argument("--version", action="version", version=f"discord-nowcast {VERSION}")
    parser.add_argument("--status", action="store_true", help="Print current status and active media sessions")
    parser.add_argument("--daemon", action="store_true", help="Run continuously in background/foreground daemon mode")
    parser.add_argument("--yandex-login", action="store_true", help="Interactive setup to connect Yandex Music account")
    parser.add_argument("--autostart-enable", action="store_true", help="Enable system autostart (XDG & systemd)")
    parser.add_argument("--autostart-disable", action="store_true", help="Disable system autostart")
    parser.add_argument("--log", action="store_true", help="Print log contents")

    args = parser.parse_args()

    settings = Settings.load()

    if args.log:
        if os.path.exists(LOG_FILE):
            with open(LOG_FILE, "r", encoding="utf-8") as f:
                print(f.read())
        else:
            print("No log file found.")
        return

    if args.yandex_login:
        setup_yandex(settings)
        return

    if args.autostart_enable:
        LinuxStartup.enable()
        print("Autostart enabled (desktop entry & systemd unit).")
        return

    if args.autostart_disable:
        LinuxStartup.disable()
        print("Autostart disabled.")
        return

    Logger.start()
    Logger.write(f"Starting discord-nowcast v{VERSION}")

    engine = Engine()

    def handle_sig(sig, frame):
        Logger.write("Stopping engine (signal received)...")
        engine.stop()
        sys.exit(0)

    signal.signal(signal.SIGINT, handle_sig)
    signal.signal(signal.SIGTERM, handle_sig)

    engine.start()

    if args.status:
        time.sleep(1.5)
        print_status(engine)
        engine.stop()
        return

    # Default: daemon / runner mode
    try:
        while True:
            time.sleep(1.0)
    except KeyboardInterrupt:
        handle_sig(None, None)

if __name__ == "__main__":
    main()
