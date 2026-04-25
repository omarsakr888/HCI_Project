"""FormGuard BLE bridge configuration."""

# Substring match against advertised device name (case-insensitive).
TARGET_DEVICE_NAME = "Omar sakr's iPhone"

# Optional extra substrings to try if the full name never appears (Windows BLE often omits names).
TARGET_NAME_ALTERNATIVES = ("omar", "iphone")

# Bluetooth MAC address of the target phone (for matching when name is hidden)
TARGET_DEVICE_ADDRESS = "40:2D:AE:62:E1:00"

# Rough proximity gate (dBm). Tune for your space/hardware.
RSSI_NEAR_DBM = -60

HOST = "127.0.0.1"
PORT = 5050

# How many weak/absent polls before declaring loss (smooths BLE flapping).
LOST_DEBOUNCE_TICKS = 12

# Sleep between discovery passes (seconds).
SCAN_INTERVAL_SEC = 0.35

# bleak discover timeout (seconds).
DISCOVER_TIMEOUT_SEC = 2.5

# Alive heartbeat cadence while scanning.
HEARTBEAT_SEC = 5.0

# If bleak import fails: "exit" or "heartbeat".
BLE_MISSING_MODE = "heartbeat"

# Print bleak-missing warning this often when in heartbeat mode.
BLE_MISSING_WARNING_INTERVAL_SEC = 30.0

# Log a sample of discovered devices every N scans (0 to disable).
LOG_DEVICES_EVERY_N_SCANS = 5
