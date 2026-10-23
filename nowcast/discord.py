"""Discord IPC client over Linux Unix Domain Sockets (including Flatpak / Vesktop)."""
import json
import os
import struct
import socket
import glob
import uuid
from typing import Optional, Dict, Any, Tuple
from nowcast.log import Logger

HANDSHAKE = 0
FRAME = 1
CLOSE = 2
PING = 3
PONG = 4

class DiscordIPC:
    def __init__(self):
        self.sock: Optional[socket.socket] = None
        self.client_id: Optional[str] = None
        self.connected_path: Optional[str] = None

    @property
    def is_connected(self) -> bool:
        return self.sock is not None

    @property
    def is_alive(self) -> bool:
        if not self.sock:
            return False
        try:
            # Poll for EOF / disconnect
            self.sock.setblocking(False)
            data = self.sock.recv(1, socket.MSG_PEEK)
            self.sock.setblocking(True)
            if data == b"":
                return False
            return True
        except BlockingIOError:
            self.sock.setblocking(True)
            return True
        except Exception:
            return False

    def _discover_sockets(self):
        uid = os.getuid()
        xdg_run = os.environ.get("XDG_RUNTIME_DIR", f"/run/user/{uid}")
        
        candidates = [
            # Standard Discord / Linux locations
            xdg_run,
            f"/run/user/{uid}",
            os.environ.get("TMPDIR", ""),
            "/tmp",
            os.environ.get("TEMP", ""),
            # Flatpak / Vesktop / sandbox locations
            f"/run/user/{uid}/.flatpak/dev.vencord.Vesktop/xdg-run",
            f"/run/user/{uid}/app/dev.vencord.Vesktop",
            f"/run/user/{uid}/.flatpak/com.discordapp.Discord/xdg-run",
            f"/run/user/{uid}/app/com.discordapp.Discord",
        ]
        
        # Add dynamic glob discovery for flatpak sandbox dirs
        flatpak_runs = glob.glob(f"/run/user/{uid}/.flatpak/*/xdg-run")
        candidates.extend(flatpak_runs)

        found = []
        for base in candidates:
            if not base or not os.path.isdir(base):
                continue
            for i in range(10):
                path = os.path.join(base, f"discord-ipc-{i}")
                if os.path.exists(path) and path not in found:
                    found.append(path)
        return found

    def connect(self, client_id: str) -> bool:
        if self.sock:
            self.dispose()

        socket_paths = self._discover_sockets()

        for path in socket_paths:
            s = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
            s.settimeout(0.5)
            try:
                s.connect(path)
            except Exception:
                s.close()
                continue

            try:
                self._send_raw(s, HANDSHAKE, {"v": 1, "client_id": client_id})
                reply = self._read_raw(s)
                if reply and reply.get("evt") == "READY":
                    self.sock = s
                    self.client_id = client_id
                    self.connected_path = path
                    user = (reply.get("data") or {}).get("user", {})
                    username = user.get("username", "")
                    Logger.write(f"Connected to Discord/Vesktop via {path}" + (f" as {username}" if username else ""))
                    return True
                Logger.write(f"Handshake refused on {path}: {reply}")
            except Exception as e:
                Logger.write(f"Handshake error on {path}: {e}")
            s.close()

        return False

    def set_activity(self, activity: Optional[Dict[str, Any]]) -> Optional[str]:
        if not self.sock:
            return "Not connected"
        args: Dict[str, Any] = {"pid": os.getpid()}
        if activity is not None:
            args["activity"] = activity
        payload = {
            "cmd": "SET_ACTIVITY",
            "args": args,
            "nonce": str(uuid.uuid4())
        }
        try:
            self._send_raw(self.sock, FRAME, payload)
            reply = self._read_raw(self.sock)
            if reply and reply.get("evt") == "ERROR":
                data = reply.get("data") or {}
                msg = data.get("message", "Unknown error")
                Logger.write(f"Discord did not take status: {msg}")
                return msg
            return None
        except Exception as e:
            Logger.write(f"Discord communication lost during set_activity: {e}")
            self.dispose()
            return str(e)

    def dispose(self):
        if self.sock:
            try:
                self.sock.close()
            except Exception:
                pass
            self.sock = None
            self.client_id = None
            self.connected_path = None

    def _send_raw(self, sock: socket.socket, op: int, body: Any):
        data = json.dumps(body, separators=(",", ":")).encode("utf-8")
        header = struct.pack("<II", op, len(data))
        sock.sendall(header + data)

    def _read_raw(self, sock: socket.socket) -> Optional[Dict[str, Any]]:
        while True:
            header = self._read_bytes(sock, 8)
            if not header or len(header) < 8:
                return None
            op, length = struct.unpack("<II", header)
            payload_bytes = self._read_bytes(sock, length) if length > 0 else b"{}"
            data = json.loads(payload_bytes.decode("utf-8")) if payload_bytes else {}
            if op == PING:
                self._send_raw(sock, PONG, data)
                continue
            if op == CLOSE:
                raise ConnectionResetError("Discord closed connection: " + str(data))
            return data

    def _read_bytes(self, sock: socket.socket, count: int) -> bytes:
        buf = bytearray()
        while len(buf) < count:
            chunk = sock.recv(count - len(buf))
            if not chunk:
                raise ConnectionResetError("Discord socket closed")
            buf.extend(chunk)
        return bytes(buf)
