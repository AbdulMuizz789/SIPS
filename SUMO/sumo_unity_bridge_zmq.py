import os
import sys
import json
import time
import zmq
import traci

class ParkingSpaceTracker:
    def __init__(self):
        # Hardcoded capacities based on parking.add.xml
        # pa_0: 4, pa_1: 4, pa_2: 3, pa_3: 2
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
            # If unknown area, default to some capacity or ignore
            self.capacities[parking_area_id] = 10
            self.available[parking_area_id] = list(range(10))
            
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

class SumoUnityBridgeZMQ:
    def __init__(self, sumo_cfg, step_length=0.1):
        self.sumo_cfg = sumo_cfg
        self.step_length = step_length
        self.tracker = ParkingSpaceTracker()
        
        # ZMQ Setup
        self.context = zmq.Context()
        # PUB socket for sending data to Unity
        self.pub_socket = self.context.socket(zmq.PUB)
        self.pub_socket.bind("tcp://*:5556")
        
        
        # ROUTER socket for receiving data from Unity (e.g. ego car updates)
        self.router_socket = self.context.socket(zmq.ROUTER)
        self.router_socket.bind("tcp://*:5557")

        # REP socket for synchronization (optional, e.g. wait for Unity to be ready)
        self.sync_socket = self.context.socket(zmq.REP)
        self.sync_socket.bind("tcp://*:5558")
        
        print("ZMQ Bridge initialized. PUB: 5556, ROUTER: 5557, REP: 5558")

    def start_sumo(self):
        # Start SUMO
        sumo_binary = "sumo-gui" # or "sumo"
        traci.start([sumo_binary, "-c", self.sumo_cfg, "--step-length", str(self.step_length)])
        print(f"SUMO started with config: {self.sumo_cfg}")

    def run(self):
        self.start_sumo()
        
        first_step = True
        unity_present = False
        
        poller = zmq.Poller()
        poller.register(self.sync_socket, zmq.POLLIN)
        
        print("Bridge loop started. Waiting for Unity (optional) or running independently...")
        
        try:
            while traci.simulation.getMinExpectedNumber() > 0:
                # 0. Sync with Unity
                # If Unity is present, wait up to 2 seconds for a signal.
                # If Unity is not present, check briefly (1ms) to see if it has arrived.
                timeout = 2000 if unity_present else 1
                socks = dict(poller.poll(timeout=timeout))
                
                if self.sync_socket in socks:
                    self.sync_socket.recv()
                    self.sync_socket.send(b"ack")
                    if not unity_present:
                        print("Unity connected. Switching to time-lock step mode.")
                        unity_present = True
                else:
                    if unity_present:
                        print("Unity disconnected or timed out. Reverting to independent mode.")
                        unity_present = False

                if unity_present and first_step:
                    # Send START_RECORDING command to Unity now that we know it's connected
                    self.send_command("START_RECORDING")
                    first_step = False

                # 1. Receive data from Unity (optional, e.g. ego vehicle)
                try:
                    # Non-blocking check for messages from Unity
                    while True:
                        # We use a try-except block for non-blocking recv
                        identity, message = self.router_socket.recv_multipart(flags=zmq.NOBLOCK)
                        # Process message if needed
                        pass
                except zmq.Again:
                    pass
                
                # 2. SUMO Simulation Step
                traci.simulationStep()
                
                # 3. Collect and Send Data
                self.send_vehicle_updates()
                self.send_traffic_light_updates()
                self.handle_parking_events()
                
                # Sleep to match simulation speed only if Unity is NOT controlling the clock
                if not unity_present:
                    time.sleep(self.step_length)
                
        except Exception as e:
            if "connection closed" in str(e).lower() or "not connected" in str(e).lower():
                print("SUMO simulation closed or connection lost.")
            else:
                print(f"Error in bridge loop: {e}")
        finally:
            print("Cleaning up bridge...")
            try:
                self.send_command("STOP_RECORDING")
            except:
                pass
            
            try:
                traci.close()
            except:
                pass
                
            self.pub_socket.close(linger=0)
            self.router_socket.close(linger=0)
            self.sync_socket.close(linger=0)
            self.context.term()
            print("Bridge terminated.")

    def send_command(self, command_str):
        msg = {
            "type": "command",
            "command": command_str
        }
        self.pub_socket.send_string(json.dumps(msg))

    def send_vehicle_updates(self):
        vehicles = []
        for veh_id in traci.vehicle.getIDList():
            pos = traci.vehicle.getPosition(veh_id)
            angle = traci.vehicle.getAngle(veh_id)
            veh_type = traci.vehicle.getTypeID(veh_id)
            speed = traci.vehicle.getSpeed(veh_id)
            
            # Map SUMO (X, Y) to Unity (X, Z). 
            # SUMO angle is degrees clockwise from North (0).
            # SimulationController.cs does: new Vector3(pos[0], pos[2], pos[1]) 
            # and new Rotation(0, angle - 90, 0).
            # Wait, SUMO getPosition returns (x, y). 
            # If we send [x, y, 0], SimulationController will use x as X, 0 as Z, y as Y.
            # That's probably not what we want if we want flat ground.
            # Usually SUMO (x, y) -> Unity (x, z).
            
            # Let's check SimulationController.cs again:
            # Vector3 newPosition = new Vector3((float)vehicle.position[0], (float)vehicle.position[2], (float)vehicle.position[1]);
            # So if we send [x, 0, y], Unity gets X=x, Y=y, Z=0. Still weird.
            # If we send [x, y, 0], Unity gets X=x, Y=0, Z=y. PERFECT!
            
            vehicles.append({
                "vehicle_id": veh_id,
                "position": [pos[0], pos[1], 0], # x, y, z
                "angle": angle,
                "type": veh_type,
                "long_speed": speed,
                "vert_speed": 0,
                "lat_speed": 0
            })
            
        if vehicles:
            msg = {
                "type": "vehicles",
                "vehicles": vehicles
            }
            self.pub_socket.send_string(json.dumps(msg))

    def send_traffic_light_updates(self):
        lights = []
        for tl_id in traci.trafficlight.getIDList():
            state = traci.trafficlight.getRedYellowGreenState(tl_id)
            lights.append({
                "junction_id": tl_id,
                "state": state
            })
        
        if lights:
            msg = {
                "type": "trafficlights",
                "lights": lights
            }
            self.pub_socket.send_string(json.dumps(msg))

    def handle_parking_events(self):
        # Parked
        parked_ids = traci.simulation.getParkingStartingVehiclesIDList()
        for veh_id in parked_ids:
            stop_params = traci.vehicle.getStops(veh_id)
            if stop_params:
                parking_area_id = stop_params[0].stoppingPlaceID
                index = self.tracker.reserve_space(veh_id, parking_area_id)
                if index is not None:
                    msg = {
                        "type": "parking",
                        "vehicle_id": veh_id,
                        "action": "park",
                        "parking_area_id": parking_area_id,
                        "parking_index": index
                    }
                    self.pub_socket.send_string(json.dumps(msg))
        
        # Unparked
        ending_ids = traci.simulation.getStopEndingVehiclesIDList()
        for veh_id in ending_ids:
            if veh_id in self.tracker.vehicle_parking:
                area_id, index = self.tracker.free_space(veh_id)
                msg = {
                    "type": "parking",
                    "vehicle_id": veh_id,
                    "action": "unpark",
                    "parking_area_id": area_id,
                    "parking_index": index
                }
                self.pub_socket.send_string(json.dumps(msg))

if __name__ == "__main__":
    cfg = "parking_sumo.sumocfg"
    if len(sys.argv) > 1:
        cfg = sys.argv[1]
    
    bridge = SumoUnityBridgeZMQ(cfg)
    bridge.run()
