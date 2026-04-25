import socket
import threading
import json
import base64
import numpy as np
import face_recognition
import cv2
import os
import sys
import time
import traceback

# ---------- Configuration ----------
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
DB_FILE = os.path.join(SCRIPT_DIR, "face_db.json")
HOST = "127.0.0.1"
PORT = 5000
TOLERANCE = 0.55

# ---------- Database Helpers ----------
def load_db():
    if os.path.exists(DB_FILE):
        try:
            with open(DB_FILE, 'r', encoding='utf-8') as f:
                data = json.load(f)
            return data if isinstance(data, dict) else {}
        except Exception:
            print(f"[face_server] failed to load db from {DB_FILE}")
            traceback.print_exc()
    return {}

def save_db(db):
    try:
        with open(DB_FILE, 'w', encoding='utf-8') as f:
            json.dump(db, f, indent=2, ensure_ascii=False)
    except Exception:
        print(f"[face_server] failed to save db to {DB_FILE}")
        traceback.print_exc()

def generate_new_name(db):
    existing = [k for k in db.keys() if k.startswith("Person_")]
    if not existing:
        return "Person_001"
    nums = []
    for k in existing:
        parts = k.split('_')
        if len(parts) == 2 and parts[1].isdigit():
            nums.append(int(parts[1]))
    next_num = max(nums) + 1 if nums else 1
    return f"Person_{next_num:03d}"

def encode_frame_to_b64(frame):
    ret, buf = cv2.imencode('.jpg', frame)
    if not ret:
        return None
    return base64.b64encode(buf).decode('utf-8')

def send_json(conn, payload):
    msg = json.dumps(payload, separators=(',', ':')) + '\n'
    try:
        conn.sendall(msg.encode('utf-8'))
        return True
    except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError, OSError) as ex:
        print(f"[face_server] client disconnected while sending: {ex}")
        return False
    except Exception:
        print("[face_server] unexpected send error")
        traceback.print_exc()
        return False

# ---------- Streaming Face Operations ----------
def handle_register(conn):
    cap = None
    try:
        db = load_db()
        known_encodings = [np.array(db[name]) for name in db]

        cap = cv2.VideoCapture(0)
        if not cap.isOpened():
            send_json(conn, {"status": "failed", "error": "Camera unavailable"})
            return
        time.sleep(0.5)

        max_attempts = 120
        attempt = 0
        result = None

        while attempt < max_attempts:
            ret, frame = cap.read()
            if not ret:
                result = {"status": "failed", "error": "Camera read failed"}
                break

            b64 = encode_frame_to_b64(frame)
            if b64 and not send_json(conn, {"frame": b64}):
                return

            rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            face_locations = face_recognition.face_locations(rgb_frame)

            if face_locations:
                largest = max(face_locations, key=lambda rect: (rect[2]-rect[0])*(rect[1]-rect[3]))
                encodings = face_recognition.face_encodings(rgb_frame, [largest])
                if encodings:
                    encoding = encodings[0]

                    if known_encodings:
                        matches = face_recognition.compare_faces(known_encodings, encoding, tolerance=TOLERANCE)
                        if any(matches):
                            result = {"status": "failed", "error": "User already exists"}
                            break

                    name = generate_new_name(db)
                    db[name] = encoding.tolist()
                    save_db(db)
                    result = {"status": "success", "name": name}
                    break

            attempt += 1

        if result is None:
            result = {"status": "failed", "error": "No face detected or encoding failed"}
        send_json(conn, result)
    except Exception as ex:
        print(f"[face_server] register error: {ex}")
        traceback.print_exc()
        send_json(conn, {"status": "failed", "error": f"register error: {ex}"})
    finally:
        if cap is not None:
            cap.release()
        cv2.destroyAllWindows()

def handle_verify(conn):
    cap = None
    try:
        db = load_db()
        if not db:
            send_json(conn, {"status": "failed", "error": "No registered faces"})
            return

        known_names = list(db.keys())
        known_encodings = [np.array(db[name]) for name in known_names]

        cap = cv2.VideoCapture(0)
        if not cap.isOpened():
            send_json(conn, {"status": "failed", "error": "Camera unavailable"})
            return
        time.sleep(0.5)

        max_attempts = 120
        attempt = 0
        result = None

        while attempt < max_attempts:
            ret, frame = cap.read()
            if not ret:
                result = {"status": "failed", "error": "Camera read failed"}
                break

            b64 = encode_frame_to_b64(frame)
            if b64 and not send_json(conn, {"frame": b64}):
                return

            rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            face_locations = face_recognition.face_locations(rgb_frame)

            if face_locations:
                largest = max(face_locations, key=lambda rect: (rect[2]-rect[0])*(rect[1]-rect[3]))
                encodings = face_recognition.face_encodings(rgb_frame, [largest])
                if encodings:
                    encoding = encodings[0]
                    matches = face_recognition.compare_faces(known_encodings, encoding, tolerance=TOLERANCE)
                    face_distances = face_recognition.face_distance(known_encodings, encoding)
                    best_idx = int(np.argmin(face_distances))

                    if matches[best_idx]:
                        name = known_names[best_idx]
                        result = {"status": "success", "name": name}
                    else:
                        result = {"status": "failed", "error": "User not found"}
                    break

            attempt += 1

        if result is None:
            result = {"status": "failed", "error": "No face detected"}
        send_json(conn, result)
    except Exception as ex:
        print(f"[face_server] verify error: {ex}")
        traceback.print_exc()
        send_json(conn, {"status": "failed", "error": f"verify error: {ex}"})
    finally:
        if cap is not None:
            cap.release()
        cv2.destroyAllWindows()

# ---------- Client Handler ----------
def handle_client(conn, addr):
    print(f"Connected by {addr}")
    try:
        file_in = conn.makefile("r", encoding="utf-8", newline="\n")
        data = file_in.readline()
        if not data:
            print(f"[face_server] empty request from {addr}")
            return
        data = data.strip()
        print(f"Received command: {data}")

        # Try to parse as JSON
        action = None
        try:
            request = json.loads(data)
            action = request.get("action", "").lower()
        except:
            # Fallback to plain text
            action = data.lower()

        if action == "register":
            handle_register(conn)
        elif action == "verify" or action == "login":   # also accept 'login'
            handle_verify(conn)
        else:
            send_json(conn, {"status": "failed", "error": f"Unknown action: {action}"})
    except Exception as e:
        print(f"Error handling {addr}: {e}")
        traceback.print_exc()
        send_json(conn, {"status": "failed", "error": str(e)})
    finally:
        try:
            conn.shutdown(socket.SHUT_RDWR)
        except Exception:
            pass
        conn.close()
        print(f"Connection with {addr} closed.")

# ---------- Main Server Loop ----------
if __name__ == "__main__":
    port = PORT
    if len(sys.argv) > 1:
        try:
            port = int(sys.argv[1])
        except:
            pass
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server_socket:
        server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        server_socket.bind((HOST, port))
        server_socket.listen(5)
        print(f"Face server streaming on {HOST}:{port}")
        while True:
            try:
                conn, addr = server_socket.accept()
                client_thread = threading.Thread(target=handle_client, args=(conn, addr), daemon=True)
                client_thread.start()
            except Exception as ex:
                print(f"[face_server] accept loop error: {ex}")
                traceback.print_exc()
                time.sleep(0.2)