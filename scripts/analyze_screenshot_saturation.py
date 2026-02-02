import sys
import os
from PIL import Image
import numpy as np

try:
    import cv2
    OPENCV_AVAILABLE = True
except ImportError:
    OPENCV_AVAILABLE = False

def analyze_image(image_path):
    print(f"Analyzing: {image_path}")
    
    try:
        pil_img = Image.open(image_path).convert('RGB')
    except Exception as e:
        print(f"Failed to open image: {e}")
        sys.exit(1)

    width, height = pil_img.size
    img_array = np.array(pil_img)
    
    # ROI: Center horizontal, avoiding top/bottom UI
    y_start = int(height * 0.25)
    y_end = int(height * 0.75)
    x_start = int(width * 0.1) 
    x_end = int(width * 0.9)
    
    roi = img_array[y_start:y_end, x_start:x_end]
    
    bottle_interiors = []

    if OPENCV_AVAILABLE:
        # Detect "bright" regions using thresholding
        gray = cv2.cvtColor(roi, cv2.COLOR_RGB2GRAY)
        
        # Threshold to find bright areas (likely glass/liquid/highlight)
        # Using 200 as threshold for "bright"
        _, thresh = cv2.threshold(gray, 200, 255, cv2.THRESH_BINARY)
        
        contours, _ = cv2.findContours(thresh, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        
        for cnt in contours:
            x, y, w, h = cv2.boundingRect(cnt)
            aspect_ratio = h / float(w)
            area = w * h
            
            roi_area = roi.shape[0] * roi.shape[1]
            
            # Bottles are vertical: Aspect ratio > 1.2 roughly
            # Not tiny noise: area > 0.5% of ROI
            if aspect_ratio > 1.2 and area > (roi_area * 0.005):
                # Crop inward to get the "interior" avoiding edge highlights
                margin_x = int(w * 0.25)
                margin_y = int(h * 0.25)
                
                if w > 2*margin_x and h > 2*margin_y:
                    interior = roi[y+margin_y : y+h-margin_y, x+margin_x : x+w-margin_x]
                    bottle_interiors.append(interior)
    
    s_values = []
    
    if bottle_interiors:
        print(f"Detected {len(bottle_interiors)} bottle-like regions.")
        for crop in bottle_interiors:
            # Convert crop to HSV using PIL logic (consistent 0-255)
            hsv_crop = Image.fromarray(crop).convert('HSV')
            hsv_arr = np.array(hsv_crop)
            s = hsv_arr[:,:,1].flatten()
            s_values.extend(s)
    else:
        print("Warning: Could not detect specific bottle contours. analyzing all bright pixels in ROI.")
        hsv_roi = Image.fromarray(roi).convert('HSV')
        hsv_data = np.array(hsv_roi)
        v_data = hsv_data[:,:,2]
        bright_mask = v_data > 180
        
        if np.sum(bright_mask) == 0:
             print("No bright pixels found.")
             sys.exit(1)
             
        s_values = hsv_data[:,:,1][bright_mask]
        
    s_values = np.array(s_values)
    if len(s_values) == 0:
        print("No valid pixels to analyze.")
        sys.exit(1)

    avg_saturation = np.mean(s_values)
    saturation_pct = avg_saturation / 255.0
    
    print(f"Average Saturation (0-1): {saturation_pct:.4f}")
    
    # Thresholds
    # "Vivid" -> High Saturation.
    # "Washed out" -> Low Saturation.
    SATURATION_THRESHOLD = 0.25 
    
    if saturation_pct < SATURATION_THRESHOLD:
        print(f"FAIL: Saturation {saturation_pct:.2f} is below threshold {SATURATION_THRESHOLD}")
        return False
    else:
        print(f"PASS: Saturation {saturation_pct:.2f} is satisfactory.")
        return True

if __name__ == "__main__":
    if len(sys.argv) < 2:
        image_path = "doc/play-store-assets/screenshots/phone/screenshot-01-launch.png"
    else:
        image_path = sys.argv[1]
    
    image_path = os.path.abspath(image_path)
    if not os.path.exists(image_path):
        print(f"File not found: {image_path}")
        sys.exit(1)
        
    result = analyze_image(image_path)
    if not result:
        sys.exit(1)
    sys.exit(0)
