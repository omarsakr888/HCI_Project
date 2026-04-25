"""FormGuard BLE -> TCP bridge entrypoint."""

from __future__ import annotations

import datetime as dt
import signal
import sys
import threading
import time
import traceback

import config
from ble_worker import check_platform, get_ble_import_status, install_hint, run_in_thread
from tcp_sender import TcpSender


def _ts() -> str:
    return dt.datetime.now().strftime("%Y-%m-%d %H:%M:%S")


def log(level: str, message: str) -> None:
    print(f"[{_ts()}] [{level}] [MAIN] {message}", flush=True)


def _install_signals(stop: threading.Event) -> None:
    def handle(*_args) -> None:
        log("INFO", "signal received -> stopping")
        stop.set()

    signal.signal(signal.SIGINT, handle)
    if sys.platform == "win32":
        signal.signal(signal.SIGBREAK, handle)


def main() -> int:
    log("INFO", "FormGuard BLE bridge starting")
    log("INFO", f"Python: {sys.version.split()[0]} | executable: {sys.executable}")
    log("INFO", f"TCP target: {config.HOST}:{config.PORT}")
    log("INFO", f"Target device name: {config.TARGET_DEVICE_NAME!r}")
    log("INFO", f"RSSI threshold: > {config.RSSI_NEAR_DBM} dBm")

    check_platform()
    bleak_ok, bleak_error = get_ble_import_status()
    if not bleak_ok:
        mode = str(config.BLE_MISSING_MODE).strip().lower()
        log("ERROR", f"bleak import failed: {bleak_error}")
        log("ERROR", install_hint())
        if mode == "exit":
            log("ERROR", "BLE_MISSING_MODE=exit -> exiting now")
            return 2
        log("WARN", "BLE_MISSING_MODE=heartbeat -> continuing without real BLE detection")

    stop = threading.Event()
    _install_signals(stop)

    sender = TcpSender(config.HOST, config.PORT, stop)
    sender.start()
    log("INFO", "TCP sender thread started")

    ble = threading.Thread(
        target=_ble_runner,
        args=(sender.send_line, stop),
        name="formguard-ble",
        daemon=False,
    )
    ble.start()
    log("INFO", "BLE worker thread started")

    try:
        while not stop.is_set():
            time.sleep(0.25)
    except KeyboardInterrupt:
        log("INFO", "KeyboardInterrupt")
        stop.set()
    finally:
        stop.set()
        log("INFO", "shutdown initiated")
        sender.close()
        ble.join(timeout=10.0)
        sender.join(timeout=8.0)
        log("INFO", "exit")

    return 0


def _ble_runner(send_line, stop: threading.Event) -> None:
    try:
        run_in_thread(send_line, stop)
    except Exception as exc:
        log("ERROR", f"BLE runner crashed: {type(exc).__name__}: {exc}")
        log("ERROR", traceback.format_exc().strip())


if __name__ == "__main__":
    raise SystemExit(main())
