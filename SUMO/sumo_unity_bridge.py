import traci
import socket
import json

class SUMOUnityBridge:
    def __init__(self, sumo_config, unity_port=9000):
        self.sumo_config = sumo_config
        self.unity_port = unity_port
        self.socket = None
        
    def start_sumo(self):
        traci.start(["sumo-gui", "-c", self.sumo_config])
        
    def connect_unity(self):
        self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.socket.connect(('localhost', self.unity_port))
        
    def step(self):
        traci.simulationStep()
        departed = traci.simulation.getDepartedIDList()
        
        for veh_id in departed:
            vehicle_data = {
                'id': veh_id,
                'type': traci.vehicle.getTypeID(veh_id),
                'position': traci.vehicle.getPosition(veh_id)
            }
            self.send_to_unity(vehicle_data)