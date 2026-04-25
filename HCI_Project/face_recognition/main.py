import cv2
import numpy as np
from gaze_tracking import GazeTracking

# Screen dimensions (Change if your monitor is different)
SCREEN_W = 1920
SCREEN_H = 1080

# --- Sensitivity settings to reach the edges ---
SENSITIVITY_X = 3.0  
SENSITIVITY_Y = 4.0  

gaze = GazeTracking()
webcam = cv2.VideoCapture(0)

cv2.namedWindow("Gaze Tracker", cv2.WINDOW_NORMAL)
cv2.setWindowProperty("Gaze Tracker", cv2.WND_PROP_FULLSCREEN, cv2.WINDOW_FULLSCREEN)

print("Starting tracking... look around to test edges.")

# Smoothing variables
smooth_x, smooth_y = SCREEN_W // 2, SCREEN_H // 2

while True:
    _, frame = webcam.read()
    frame = cv2.flip(frame, 1) # Mirror the camera
    
    # Send the frame to the GitHub library to do the heavy lifting
    gaze.refresh(frame)

    # Create your dark grey canvas
    canvas = np.full((SCREEN_H, SCREEN_W, 3), 60, dtype=np.uint8)

    # Get the ratios from the GitHub library
    h_ratio = gaze.horizontal_ratio()
    v_ratio = gaze.vertical_ratio()

    if h_ratio is not None and v_ratio is not None:
        # The library considers 0.5 to be the center. 
        # We subtract 0.5 to get a direction (-0.5 to +0.5)
        dx = h_ratio - 0.5
        dy = v_ratio - 0.5
        
        # Calculate target screen pixels using our sensitivity multipliers
        target_x = int((SCREEN_W / 2) + (dx * SENSITIVITY_X * SCREEN_W))
        target_y = int((SCREEN_H / 2) + (dy * SENSITIVITY_Y * SCREEN_H))
        
        # Smooth the movement
        smooth_x = int(0.7 * smooth_x + 0.3 * target_x)
        smooth_y = int(0.7 * smooth_y + 0.3 * target_y)

    # Keep the dot safely on the screen
    smooth_x = max(25, min(SCREEN_W - 25, smooth_x))
    smooth_y = max(25, min(SCREEN_H - 25, smooth_y))

    # Draw your red dot with white border
    cv2.circle(canvas, (smooth_x, smooth_y), 25, (0, 0, 255), -1)
    cv2.circle(canvas, (smooth_x, smooth_y), 26, (200, 200, 200), 2)

    # --- Top Left UI (Using the library's built in visualization) ---
    annotated_frame = gaze.annotated_frame()
    if annotated_frame is not None:
        # Resize it to fit neatly in the corner like your image
        small_frame = cv2.resize(annotated_frame, (320, 240))
        canvas[10:250, 10:330] = small_frame

    cv2.imshow("Gaze Tracker", canvas)

    if cv2.waitKey(1) == ord('q'):
        break

webcam.release()
cv2.destroyAllWindows()