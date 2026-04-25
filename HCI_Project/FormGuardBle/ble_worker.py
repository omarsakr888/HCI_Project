"""BLE proximity monitoring using bleak on Windows (discover loop + resilient logging)."""

from __future__ import annotations

import asyncio
import datetime as dt
import sys
import threading
import time
import traceback
from typing import Callable, List, Optional, Tuple

import config

try:
    from bleak import BleakScanner
except ImportError as e:
    BleakScanner = None  # type: ignore[assignment]
    _BLEAK_IMPORT_ERROR: Optional[Exception] = e
else:
    _BLEAK_IMPORT_ERROR = None


def _ts() -> str:
    return dt.datetime.now().strftime("%Y-%m-%d %H:%M:%S")


def log(level: str, message: str) -> None:
    print(f"[{_ts()}] [{level}] {message}", flush=True)


def get_ble_import_status() -> Tuple[bool, Optional[str]]:
    ok = BleakScanner is not None and _BLEAK_IMPORT_ERROR is None
    return ok, None if ok else str(_BLEAK_IMPORT_ERROR)


def install_hint() -> str:
    return "Activate your Python 3.12 venv, then run: python -m pip install bleak"


def check_platform() -> None:
    if sys.platform == "win32":
        log("INFO", "platform=Windows (Bleak uses WinRT Bluetooth LE stack)")


def _name_matches(target: str, name: str, alternatives: Tuple[str, ...]) -> bool:
    normalized_name = (name or "").strip().lower()
    if not normalized_name:
        return False

    if target in normalized_name:
        return True

    for alt in alternatives:
        if alt and alt.lower() in normalized_name:
            return True

    return False


def _best_rssi_for_device(device) -> Optional[int]:
    rssi_value = getattr(device, "rssi", None)
    if rssi_value is None:
        return None

    try:
        return int(rssi_value)
    except (TypeError, ValueError):
        return None


async def _scan_loop(send_line: Callable[[str], None], stop: threading.Event) -> None:
    target = config.TARGET_DEVICE_NAME.lower().strip()
    target_label = config.TARGET_DEVICE_NAME.strip() or "Target phone"
    alternatives = tuple(alt.strip() for alt in config.TARGET_NAME_ALTERNATIVES if str(alt).strip())
    target_address = getattr(config, "TARGET_DEVICE_ADDRESS", "").upper()

    was_near = False
    loss_ticks = 0
    scan_index = 0
    last_heartbeat = 0.0

    send_line("SCANNING")
    log("INFO", "queued SCANNING to TCP")

    while not stop.is_set():
        now = time.monotonic()
        if now - last_heartbeat >= config.HEARTBEAT_SEC:
            log("INFO", "SCANNING")
            last_heartbeat = now

        devices: List = []
        try:
            devices = await BleakScanner.discover(timeout=config.DISCOVER_TIMEOUT_SEC)  # type: ignore[union-attr]
        except Exception as exc:
            log("ERROR", f"BleakScanner.discover failed: {type(exc).__name__}: {exc}")
            log("ERROR", traceback.format_exc().strip())
            log("WARN", "Bluetooth unavailable or permission denied; keeping worker alive and retrying.")
            send_line("SCANNING")
            await asyncio.sleep(2.0)
            continue

        scan_index += 1
        if config.LOG_DEVICES_EVERY_N_SCANS and scan_index % config.LOG_DEVICES_EVERY_N_SCANS == 0:
            log("INFO", f"discover found {len(devices)} device(s)")
            for device in devices[:25]:
                name = (device.name or "") if hasattr(device, "name") else ""
                address = getattr(device, "address", "?")
                rssi = _best_rssi_for_device(device)
                log("DEBUG", f"device addr={address!r} name={name!r} rssi={rssi}")

        match = None
        best_rssi = -200
        nearest_named_device = None
        nearest_named_rssi = -200
        for device in devices:
            name = (device.name or "") if hasattr(device, "name") else ""
            address = getattr(device, "address", "").upper()
            rssi = _best_rssi_for_device(device)

            if name:
                score_for_any = rssi if rssi is not None else -120
                if score_for_any > nearest_named_rssi:
                    nearest_named_rssi = score_for_any
                    nearest_named_device = device

            name_matches = _name_matches(target, name, alternatives)
            addr_matches = (target_address and address == target_address)

            if not (name_matches or addr_matches):
                continue

            score = rssi if rssi is not None else -120
            if score > best_rssi:
                best_rssi = score
                match = device

        if match is not None:
            # If target matched by name/address, always publish configured target label.
            send_line(f"DETECTED_PHONE:{target_label}")
        elif nearest_named_device is not None:
            detected_name = getattr(nearest_named_device, "name", "") or "Unknown phone"
            send_line(f"DETECTED_PHONE:{detected_name}")

        if match is not None:   # ignore RSSI for now
            loss_ticks = 0
            send_line(f"ATHLETE_NAME:{target_label}")
            if not was_near:
                signal_text = f"{best_rssi} dBm"
                send_line(f"SIGNAL:{signal_text}")
                send_line("PHONE_NEAR")
                log("INFO", "PHONE_NEAR")
                log("INFO", f"matched device addr={getattr(match, 'address', '?')!r} rssi={signal_text}")
            was_near = True
        else:
            loss_ticks += 1
            if was_near and loss_ticks >= config.LOST_DEBOUNCE_TICKS:
                was_near = False
                send_line("DEVICE_LOST")
                log("WARN", "DEVICE_LOST")

        await asyncio.sleep(config.SCAN_INTERVAL_SEC)


def _heartbeat_only_loop(send_line: Callable[[str], None], stop: threading.Event, reason: str) -> None:
    log("ERROR", f"bleak import failed: {reason}")
    log("ERROR", "BLE proximity detection is unavailable in this run.")
    log("ERROR", install_hint())

    warn_interval = max(5.0, float(config.BLE_MISSING_WARNING_INTERVAL_SEC))
    last_warning = 0.0

    send_line("SCANNING")
    send_line("DEVICE_LOST")

    while not stop.is_set():
        send_line("SCANNING")
        log("WARN", "SCANNING (heartbeat mode; no real BLE detection)")

        now = time.monotonic()
        if now - last_warning >= warn_interval:
            log("WARN", "bleak is still missing; PHONE_NEAR cannot be emitted.")
            log("WARN", install_hint())
            last_warning = now

        time.sleep(max(1.0, float(config.HEARTBEAT_SEC)))


def run_in_thread(send_line: Callable[[str], None], stop: threading.Event) -> None:
    bleak_ok, bleak_error = get_ble_import_status()
    if not bleak_ok:
        mode = str(config.BLE_MISSING_MODE).strip().lower()
        if mode == "exit":
            log("ERROR", f"bleak import failed: {bleak_error}")
            log("ERROR", install_hint())
            send_line("SCANNING")
            send_line("DEVICE_LOST")
            stop.set()
            return

        _heartbeat_only_loop(send_line, stop, bleak_error or "unknown import error")
        return

    log(
        "INFO",
        f"starting BLE scan loop for target={config.TARGET_DEVICE_NAME!r} threshold>{config.RSSI_NEAR_DBM} dBm",
    )
    while not stop.is_set():
        try:
            asyncio.run(_scan_loop(send_line, stop))
            break
        except Exception as exc:
            log("ERROR", f"fatal BLE loop exception: {type(exc).__name__}: {exc}")
            log("ERROR", traceback.format_exc().strip())
            log("WARN", "recovering BLE worker in 2.0s")
            time.sleep(2.0)