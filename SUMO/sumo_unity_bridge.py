import traci
import socket
import json

class ParkingSpaceTracker:
    def __init__(self):
        # Hardcoded capacities based on parking.add.xml
        self.capacities = {
            'pa_0': 4,
            'pa_1': 4,
            'pa_2': 3,
            'pa_3': 2
        }
        # Available spaces per area: list of indices
        self.available = {area_id: list(range(num)) 
                          for area_id, num in self.capacities.items()}
        # Map vehicle_id -> (area_id, index)
        self.vehicle_parking = {}
    
    def reserve_space(self, vehicle_id, parking_area_id):
        """Try to allocate a space in the given area for the vehicle."""
        if parking_area_id not in self.available:
            return None
        if not self.available[parking_area_id]:
            return None
        index = self.available[parking_area_id].pop(0)
        self.vehicle_parking[vehicle_id] = (parking_area_id, index)
        return index
    
    def free_space(self, vehicle_id):
        """Release the space occupied by the vehicle."""
        if vehicle_id not in self.vehicle_parking:
            return None
        area_id, index = self.vehicle_parking.pop(vehicle_id)
        self.available[area_id].append(index)
        self.available[area_id].sort()
        return (area_id, index)

class SUMOUnityBridge:
    def __init__(self, sumo_config, unity_port=9000):
        self.sumo_config = sumo_config
        self.unity_port = unity_port
        self.socket = None
        self.tracker = ParkingSpaceTracker()
        
    def start_sumo(self):
        traci.start(["sumo-gui", "-c", self.sumo_config])
        
    def connect_unity(self):
        self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.socket.connect(('localhost', self.unity_port))

    def send_to_unity(self, data):
        """Send a JSON-serialised message to Unity over the socket."""
        msg = json.dumps(data)
        self.socket.sendall(msg.encode('utf-8') + '\n'.encode('utf-8'))  # Add newline as message delimiter
        
    def step(self):
        traci.simulationStep()
        
        # Handle departed vehicles (new arrivals)
        departed = traci.simulation.getDepartedIDList()
        for veh_id in departed:
            vehicle_data = {
                'id': veh_id,
                'type': traci.vehicle.getTypeID(veh_id),
                'position': traci.vehicle.getPosition(veh_id),
                'action': 'depart'
            }
            self.send_to_unity(vehicle_data)
        
        # Handle vehicles that have just parked
        parked_vehicles = traci.simulation.getParkingStartingVehiclesIDList()
        for veh_id in parked_vehicles:
            stop_params = traci.vehicle.getStops(veh_id)
            if not stop_params:
                continue
            parking_area_id = stop_params[0].stoppingPlaceID
            # Reserve a specific space index
            index = self.tracker.reserve_space(veh_id, parking_area_id)
            if index is None:
                # No space available (should not happen with correct capacities)
                continue
            vehicle_data = {
                'id': veh_id,
                'action': 'park',
                'parking_area_id': parking_area_id,
                'parking_index': index
            }
            print(f"Vehicle {veh_id} parked in {parking_area_id} at index {index}")
            self.send_to_unity(vehicle_data)
        
        # Handle vehicles that have ended parking (departed from parking area)
        # Note: getStopEndingVehiclesIDList includes any type of stop, not only parking.
        # We'll need to check if the stop was a parking area.
        ending_vehicles = traci.simulation.getStopEndingVehiclesIDList()
        for veh_id in ending_vehicles:
            # Check if this vehicle was previously parked by us
            if veh_id in self.tracker.vehicle_parking:
                area_id, index = self.tracker.free_space(veh_id)
                vehicle_data = {
                    'id': veh_id,
                    'action': 'unpark',
                    'parking_area_id': area_id,
                    'parking_index': index
                }
                self.send_to_unity(vehicle_data)

if __name__ == "__main__":
    import sys
    config = sys.argv[1] if len(sys.argv) > 1 else "parking_sumo.sumocfg"
    port = int(sys.argv[2]) if len(sys.argv) > 2 else 9000
    bridge = SUMOUnityBridge(config, unity_port=port)
    bridge.start_sumo()
    bridge.connect_unity()
    try:
        while True:
            bridge.step()
    except KeyboardInterrupt:
        print("Simulation stopped by user.")
    finally:
        traci.close()
        if bridge.socket:
            bridge.socket.close()
