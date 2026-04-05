import zmq
import cv2
import numpy as np
import json
from ultralytics import YOLO
import heapq

class SmartParkingVision:
    def __init__(self, weights='yolov8n.pt', port=5560):
        # Load YOLO model
        self.model = YOLO(weights)
        
        # ZMQ Context
        self.context = zmq.Context()
        self.pull_socket = self.context.socket(zmq.PULL)
        self.pull_socket.bind(f"tcp://*:{port}")
        
        # IPM Target Size
        self.target_width = 800
        self.target_height = 800
        
        # Graph for Dijkstra
        self.graph = {} # {node_id: {neighbor_id: weight}}

        # Global Occupancy Map (Fused View)
        self.fused_map = np.zeros((self.target_height, self.target_width, 3), dtype=np.uint8)
        self.camera_views = {} # {cameraID: last_warped_frame}
        
        print(f"Smart Parking Vision started. Listening on port {port}...")

    def get_perspective_matrix(self, src_points):
        """
        src_points: 4 points in image (pixel coords)
        returns transformation matrix
        """
        # Target points: assuming a square/rectangle area in bird's eye view
        # We can map the 4 points to a 200x200 area for example
        dst_points = np.float32([
            [0, 0],
            [self.target_width, 0],
            [self.target_width, self.target_height],
            [0, self.target_height]
        ])
        src_points = np.float32(src_points)
        return cv2.getPerspectiveTransform(src_points, dst_points)

    def fuse_views(self, cameraID, warped_frame):
        """
        Update the global bird's eye view with the latest warped frame from a camera.
        """
        self.camera_views[cameraID] = warped_frame
        # Simple fusion: overlaying. More advanced would use a lookup table as per paper.
        # Here we just combine all available views
        fused = np.zeros_like(self.fused_map)
        for vid, img in self.camera_views.items():
            # Basic additive blend or masking could be used here
            mask = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY) > 0
            fused[mask] = img[mask]
        self.fused_map = fused
        
        cv2.imshow("Fused Bird's Eye View", self.fused_map)
        cv2.waitKey(1)

    def process_frame(self, metadata, image_bytes):
        # Decode image
        nparr = np.frombuffer(image_bytes, np.uint8)
        frame = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
        
        # 1. Vehicle Detection
        results = self.model(frame, verbose=False)
        detections = []
        for r in results:
            boxes = r.boxes
            for box in boxes:
                # Class 2 is car, 7 is truck, 5 is bus in COCO
                cls = int(box.cls[0])
                if cls in [2, 5, 7]:
                    b = box.xyxy[0].cpu().numpy() # [x1, y1, x2, y2]
                    detections.append(b)

        # 2. IPM (Perspective Mapping)
        src_pts = metadata.get('calibrationPoints')
        if src_pts and len(src_pts) == 4:
            # Reformat points from [{"x":.., "y":..}, ...] to [[x,y], ...]
            pts = np.array([[p['x'], p['y']] for p in src_pts])
            M = self.get_perspective_matrix(pts)
            
            # Warp the whole frame for fusion
            warped_frame = cv2.warpPerspective(frame, M, (self.target_width, self.target_height))
            self.fuse_views(metadata['cameraID'], warped_frame)

            # Map detections to virtual coordinates
            virtual_positions = []
            for det in detections:
                # Center bottom of the car is usually best for ground position
                center_x = (det[0] + det[2]) / 2
                bottom_y = det[3]
                
                # Transform point
                point = np.array([[[center_x, bottom_y]]], dtype=np.float32)
                transformed = cv2.perspectiveTransform(point, M)
                virtual_positions.append(transformed[0][0])
                
            return virtual_positions
        return []

    def dijkstra(self, start, end):
        queue = [(0, start, [])]
        seen = set()
        while queue:
            (cost, node, path) = heapq.heappop(queue)
            if node not in seen:
                path = path + [node]
                seen.add(node)
                if node == end:
                    return (cost, path)
                for neighbor, weight in self.graph.get(node, {}).items():
                    heapq.heappush(queue, (cost + weight, neighbor, path))
        return float("inf"), []

    def extract_parking_spaces(self, fused_image):
        """
        Uses Canny Edge detection and Hough lines to find parking spaces
        as described in the paper.
        """
        # 1. Blur and Canny
        blurred = cv2.GaussianBlur(fused_image, (5, 5), 0)
        edges = cv2.Canny(blurred, 150, 250)
        
        # 2. Dilation
        kernel = np.ones((3,3), np.uint8)
        dilated = cv2.dilate(edges, kernel, iterations=1)
        
        # 3. Hough Lines
        lines = cv2.HoughLinesP(dilated, 1, np.pi/180, threshold=50, minLineLength=50, maxLineGap=10)
        
        # 4. Filter and Group Lines (simplified)
        # In a real scenario, we would group parallel lines to form rectangles
        return lines

    def run(self):
        while True:
            try:
                # Receive multipart [Metadata, Image]
                meta_json = self.pull_socket.recv_string()
                img_bytes = self.pull_socket.recv()
                
                metadata = json.loads(meta_json)
                v_positions = self.process_frame(metadata, img_bytes)
                
                if v_positions:
                    print(f"Camera {metadata['cameraID']} detected {len(v_positions)} vehicles.")
                    # In a real implementation, we'd update a global occupancy map here
                    
            except KeyboardInterrupt:
                break
            except Exception as e:
                print(f"Error: {e}")

if __name__ == "__main__":
    vision = SmartParkingVision()
    vision.run()
