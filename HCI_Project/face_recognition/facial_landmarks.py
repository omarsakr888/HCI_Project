import cv2
import dlib
import os

# Load detector and predictor
detector = dlib.get_frontal_face_detector()
# Make sure the .dat file is in the same folder, or adjust the path
predictor_path = "shape_predictor_68_face_landmarks.dat"
if not os.path.exists(predictor_path):
    print(f"ERROR: {predictor_path} not found. Download it first.")
    exit(1)

predictor = dlib.shape_predictor(predictor_path)

# Open webcam
cap = cv2.VideoCapture(0)
print("Live facial landmark detection. Press 'q' to quit.")

while True:
    ret, frame = cap.read()
    if not ret:
        break

    gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)

    # Detect faces
    faces = detector(gray)

    for face in faces:
        # Draw bounding box
        x1, y1, x2, y2 = face.left(), face.top(), face.right(), face.bottom()
        cv2.rectangle(frame, (x1, y1), (x2, y2), (0, 255, 0), 2)

        # Get landmarks
        shape = predictor(gray, face)

        # Draw all 68 points
        for n in range(68):
            x = shape.part(n).x
            y = shape.part(n).y
            cv2.circle(frame, (x, y), 1, (0, 255, 0), -1)

    cv2.imshow("Facial Landmarks (dlib 68 points)", frame)

    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

cap.release()
cv2.destroyAllWindows()