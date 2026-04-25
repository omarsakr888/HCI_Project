"""Background TCP client with queue, retries, and structured logging."""

from __future__ import annotations

import datetime as dt
import queue
import socket
import threading
import time
import traceback
from typing import Optional


def _ts() -> str:
    return dt.datetime.now().strftime("%Y-%m-%d %H:%M:%S")


def log(level: str, message: str) -> None:
    print(f"[{_ts()}] [{level}] [TCP] {message}", flush=True)


class TcpSender(threading.Thread):
    def __init__(self, host: str, port: int, stop: threading.Event) -> None:
        super().__init__(daemon=False, name="formguard-tcp")
        self._host = host
        self._port = port
        self._stop = stop
        self._queue: "queue.Queue[str]" = queue.Queue()
        self._socket: Optional[socket.socket] = None

    def send_line(self, line: str) -> None:
        text = (line or "").strip()
        if not text:
            return
        self._queue.put(text)

    def close(self) -> None:
        log("INFO", "close requested")
        sock = self._socket
        self._socket = None
        if sock is not None:
            try:
                sock.shutdown(socket.SHUT_RDWR)
            except OSError as exc:
                log("DEBUG", f"shutdown ignored: {exc}")
            try:
                sock.close()
            except OSError as exc:
                log("DEBUG", f"close ignored: {exc}")

    def run(self) -> None:
        log("INFO", f"sender started -> {self._host}:{self._port}")
        retry_delay = 1.0

        while not self._stop.is_set():
            try:
                if self._socket is None:
                    log("INFO", f"connecting to {self._host}:{self._port}")
                    self._socket = socket.create_connection((self._host, self._port), timeout=5.0)
                    self._socket.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
                    retry_delay = 1.0
                    log("INFO", "connected")

                while not self._stop.is_set():
                    try:
                        message = self._queue.get(timeout=0.25)
                    except queue.Empty:
                        continue

                    payload = (message + "\n").encode("utf-8")
                    self._socket.sendall(payload)
                    log("INFO", f"sent: {message!r}")

            except (OSError, ConnectionError, TimeoutError, BrokenPipeError) as exc:
                log("ERROR", f"socket error: {type(exc).__name__}: {exc}")
                log("ERROR", traceback.format_exc().strip())
                self._close_socket_only()
                if self._stop.is_set():
                    break
                log("WARN", f"retrying connection in {retry_delay:.1f}s")
                time.sleep(min(retry_delay, 30.0))
                retry_delay = min(retry_delay * 1.5, 30.0)
            except Exception as exc:
                log("ERROR", f"unexpected TCP thread error: {type(exc).__name__}: {exc}")
                log("ERROR", traceback.format_exc().strip())
                self._close_socket_only()
                if self._stop.is_set():
                    break
                time.sleep(1.0)
            finally:
                if self._stop.is_set():
                    self._close_socket_only()

        log("INFO", "sender exiting")

    def _close_socket_only(self) -> None:
        sock = self._socket
        self._socket = None
        if sock is not None:
            try:
                sock.close()
            except OSError:
                pass
