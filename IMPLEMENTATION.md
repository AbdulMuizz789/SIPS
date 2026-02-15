# IMPLEMENTATION
This document describes the detailed implementation of the Smart Parking Guidance and Information System, including environmental setup, module development, and integration procedures.
## I. Environmental Setup
### 1.1 Unity Configuration
#### Installation and Project Setup:

1. Unity Hub Installation: Download and install Unity Hub from the official Unity website. Unity Hub provides centralized management of Unity versions and projects.
2. Unity Editor Installation: Install Unity 2023.2 LTS or later through Unity Hub. Select the following additional modules during installation:

    - Windows Build Support (or appropriate platform)
    - Visual Studio Community (for C# development)
    - Documentation


3. Project Creation:

    - Create new 3D project with URP (Universal Render Pipeline) template
    - Project name: "SmartParkingGuidance"
    - Configure version control (Git integration)



#### Required Unity Packages:
##### Table 1: Unity Package Dependencies
| Package | Version | Purpose |
|---|---|---|
|ML-Agents|3.0.0|Reinforcement learning framework|
|ProBuilder|5.0+|3D modeling tools|
|Cinemachine|2.9+|Camera control system|
|Universal RP|14.0+|Rendering pipeline|
|Input System|1.5+|Modern input handling|
|TextMeshPro|3.0+|UI text rendering|

#### Installation via Package Manager:
```
Window → Package Manager → + → Add package from git URL
```
For ML-Agents:
```
com.unity.ml-agents@3.0.0
```
#### Project Structure:
Organize project assets as follows:
```
Assets/
├── Scenes/
│   ├── MainSimulation.unity
│   └── Training.unity
├── Scripts/
│   ├── Agents/
│   ├── Environment/
│   ├── Sensors/
│   └── Utilities/
├── Prefabs/
│   ├── Vehicles/
│   ├── ParkingSpaces/
│   └── Sensors/
├── Materials/
├── Models/
└── ML-Agents/
    └── Config/
```
#### Unity Settings Configuration:

1. Quality Settings (Edit → Project Settings → Quality):

    - Set appropriate quality preset for development machines
    - Enable VSync for consistent frame rates
    - Shadow quality: Medium


1. Physics Settings (Edit → Project Settings → Physics):

    - Default solver iterations: 10
    - Gravity: (0, -9.81, 0)
    - Layer collision matrix configuration for vehicles/environment


1. Time Settings:

    - Fixed Timestep: 0.02 (50 FPS physics)
    - Maximum Allowed Timestep: 0.1



### 1.2 Python and ML-Agents Setup
#### Python Environment Configuration:

1. Python Installation: Install Python 3.9 or 3.10. Verify installation:

    ```bash
    python --version
    ```

2. Virtual Environment Creation:

    ```bash
    python -m venv parking-ml-env
    ```

3. Activation:


    - Windows: parking-ml-env\Scripts\activate
    - macOS/Linux: source parking-ml-env/bin/activate

#### Required Python Libraries:
###### Table 2: Python Libraries and Versions
|Library|Version|Purpose|
|---|---|---|
|mlagents|1.0.0|Unity ML-Agents training|
|torch|2.0.1|Deep learning framework|
|torch-geometric|2.3.0|Graph neural networks|
|numpy|1.24.0|Numerical computations|
|matplotlib|3.7.0|Visualization|
|tensorboard|2.13.0|Training monitoring|
|pandas|2.0.0|Data analysis|
|traci|1.18.0|SUMO interface|

#### Installation Commands:
```bash
pip install mlagents==1.0.0
pip install torch==2.0.1
pip install torch-geometric
pip install numpy matplotlib tensorboard pandas
```
#### ML-Agents Configuration:
Create training configuration file: `config/parking_trainer.yaml`
```yaml
behaviors:
  ParkingAgent:
    trainer_type: ppo
    hyperparameters:
      batch_size: 1024
      buffer_size: 10240
      learning_rate: 3.0e-4
      beta: 5.0e-3
      epsilon: 0.2
      lambd: 0.95
      num_epoch: 3
      learning_rate_schedule: linear
    network_settings:
      normalize: true
      hidden_units: 256
      num_layers: 3
      vis_encode_type: simple
    reward_signals:
      extrinsic:
        gamma: 0.99
        strength: 1.0
    max_steps: 5000000
    time_horizon: 64
    summary_freq: 10000
```
#### Training Command:
```bash
mlagents-learn config/parking_trainer.yaml --run-id=parking_v1
```
### 1.3 SUMO Integration
#### SUMO Installation:

1. Download SUMO 1.18.0 or later from official repository
1. Install with default options
1. Add SUMO to system PATH environment variable
1. Verify installation:

    ```bash
    sumo --version
    ```
#### TraCI Python Setup:
TraCI (Traffic Control Interface) enables Python to control SUMO:
```bash
pip install traci
```
#### SUMO Network Configuration:
Create parking facility network file: `parking_network.net.xml`

This file defines the road network structure within and approaching the parking facility, including:

- Entry/exit ramps
- Internal lanes between parking zones
- Junction definitions
- Lane specifications (width, speed limits)

#### Vehicle Route Definition:
Create route file: `parking_routes.rou.xml`

Defines vehicle types and arrival patterns:
```xml
<routes>
    <vType id="sedan" accel="2.6" decel="4.5" sigma="0.5" 
           length="4.5" maxSpeed="15.0"/>
    <vType id="suv" accel="2.0" decel="4.0" sigma="0.5" 
           length="5.2" maxSpeed="15.0"/>
    
    <flow id="peak_hour" type="sedan" begin="0" end="3600" 
          probability="0.3" from="entry" to="exit"/>
</routes>
```
#### SUMO-Unity Bridge Implementation:
Python middleware script: `sumo_unity_bridge.py`
```python
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
```
## II. Module Description
### 2.1 3D Parking Environment
###### Figure 1: 3D Parking Facility Model
The parking facility is modeled as a multi-level structure with the following components:
#### Structural Elements:

1. Building Framework:

    - Created using Unity ProBuilder for rapid 3D modeling
    - Modular design allowing easy reconfiguration
    - Concrete pillars, floors, and ceiling structures
    - Realistic dimensions: Each level 3.5m height, parking spaces 2.5m × 5m


1. Parking Spaces:

    - Prefab-based design for consistency
    - Color-coded by type (standard: white, accessible: blue, EV: green)
    - Painted lane markings using custom shaders
    - Collider components for physics interaction


1. Traffic Lanes:

    - Unidirectional flow pattern to prevent gridlock
    - Lane width: 3.5m for two-way traffic zones
    - Directional arrows and signage
    - Speed bump models near pedestrian areas


1. Ramps and Transitions:

    - Spiral ramps connecting levels (gradient < 15%)
    - Smooth mesh generation for realistic vehicle movement
    - Guard rails and safety barriers



#### Lighting System:
Implemented using URP's lighting features:

- Point lights for parking space illumination
- Directional light for overall ambient lighting
- Light intensity variation to simulate different times of day
- Baked lightmaps for performance optimization

#### Material Setup:

- Concrete material with normal maps for texture detail
- Reflective floor shader for realistic appearance
- Emission materials for illuminated signage
- PBR (Physically Based Rendering) workflow

#### Sensor Placement:
Virtual sensors are positioned above each parking space:

- Raycast-based occupancy detection
- Camera sensors for AI-based vehicle detection
- Entry/exit gate sensors for traffic monitoring

#### Performance Optimization:

- Occlusion culling to hide non-visible geometry
- Level of Detail (LOD) groups for distant objects
- Static batching for non-moving environment elements
- Texture atlasing to reduce draw calls

#### C# Environment Controller Script:
```csharp
public class ParkingEnvironment : MonoBehaviour
{
    public int numLevels = 3;
    public int spacesPerLevel = 100;
    private Dictionary<string, ParkingSpace> parkingSpaces;
    private Graph facilitGraph;
    
    void Start()
    {
        InitializeParkingSpaces();
        BuildFacilityGraph();
    }
    
    private void InitializeParkingSpaces()
    {
        parkingSpaces = new Dictionary<string, ParkingSpace>();
        // Instantiate parking space prefabs
        // Configure positions and properties
    }
    
    public ParkingSpace GetAvailableSpace(Vector3 entryPoint)
    {
        // Query available spaces
        // Return optimal space based on criteria
    }
}
```
### 2.2 Vehicle Detection Module
###### Figure 2: Vehicle Detection Neural Network
The vehicle detection module identifies vehicle presence and characteristics using a convolutional neural network.
#### Network Architecture:
The detection network consists of:

1. Input Layer: 64×64 RGB images from virtual cameras
2. Convolutional Layers:

    - Conv1: 32 filters, 3×3 kernel, ReLU activation
    - Conv2: 64 filters, 3×3 kernel, ReLU activation
    - Conv3: 128 filters, 3×3 kernel, ReLU activation


3. Pooling Layers: 2×2 max pooling after each conv layer
4. Fully Connected Layers:

    - FC1: 512 neurons
    - FC2: 256 neurons


5. Output Layer:

    - Binary classification (occupied/vacant)
    - Vehicle type classification (sedan/SUV/compact)



#### Training Data Generation:
Synthetic training data is generated within Unity:

- Render parking spaces from sensor camera viewpoints
- Randomize vehicle positions, types, and lighting
- Add noise and occlusions for robustness
- Generate 50,000 training images with labels

#### PyTorch Implementation:
```python
import torch
import torch.nn as nn

class VehicleDetectionCNN(nn.Module):
    def __init__(self):
        super(VehicleDetectionCNN, self).__init__()
        self.conv1 = nn.Conv2d(3, 32, 3, padding=1)
        self.conv2 = nn.Conv2d(32, 64, 3, padding=1)
        self.conv3 = nn.Conv2d(64, 128, 3, padding=1)
        self.pool = nn.MaxPool2d(2, 2)
        self.fc1 = nn.Linear(128 * 8 * 8, 512)
        self.fc2 = nn.Linear(512, 256)
        self.fc_occupancy = nn.Linear(256, 2)  # occupied/vacant
        self.fc_type = nn.Linear(256, 3)  # sedan/SUV/compact
        
    def forward(self, x):
        x = self.pool(F.relu(self.conv1(x)))
        x = self.pool(F.relu(self.conv2(x)))
        x = self.pool(F.relu(self.conv3(x)))
        x = x.view(-1, 128 * 8 * 8)
        x = F.relu(self.fc1(x))
        x = F.relu(self.fc2(x))
        occupancy = self.fc_occupancy(x)
        vehicle_type = self.fc_type(x)
        return occupancy, vehicle_type
```
#### Integration with Unity:
Unity script calls the trained model for real-time detection:
```csharp
public class VehicleDetector : MonoBehaviour
{
    private TorchModel model;
    private Camera sensorCamera;
    
    public OccupancyStatus DetectVehicle()
    {
        RenderTexture rt = RenderFromSensor();
        Texture2D image = ConvertToTexture2D(rt);
        
        // Send to Python model via API
        var result = model.Predict(image);
        return result.isOccupied ? 
               OccupancyStatus.Occupied : 
               OccupancyStatus.Vacant;
    }
}
```
### 2.3 MARL-GCN Pathfinding Module
###### Figure 3: MARL-GCN Network Structure
The Multi-Agent Reinforcement Learning with Graph Convolutional Network is the core intelligence component for optimal parking allocation.
#### Graph Construction:
The parking facility is represented as a graph G = (V, E):

- Vertices (V): Parking spaces and navigation waypoints
- Edges (E): Physical connections (lanes, ramps)
- Node Features: [occupancy, space_type, accessibility, utilization_history]
- Edge Features: [distance, avg_travel_time, congestion_level]

#### Graph Convolution Operation:
```python
class GCNLayer(nn.Module):
    def __init__(self, in_features, out_features):
        super(GCNLayer, self).__init__()
        self.linear = nn.Linear(in_features, out_features)
        
    def forward(self, X, A):
        # X: node features, A: adjacency matrix
        # Normalize adjacency matrix
        D = torch.diag(torch.sum(A, dim=1))
        D_inv_sqrt = torch.pow(D, -0.5)
        A_norm = D_inv_sqrt @ A @ D_inv_sqrt
        
        # Graph convolution
        H = A_norm @ X
        H = self.linear(H)
        return F.relu(H)
```
#### Multi-Agent Architecture:
Each agent represents a zone or section of the parking facility:

- Agents observe local state (nearby space occupancy)
- Agents communicate via message passing through graph edges
- Centralized training, decentralized execution (CTDE) paradigm

#### MARL-GCN Model:
###### Table 3: MARL-GCN Hyperparameters
|Parameter|Value|Description|
|---|---|---|
|Learning Rate|0.0003|Adam optimizer rate|
|Discount Factor (γ)|0.99|Future reward weight|
|GCN Hidden Dims|[128, 64, 32]|Layer dimensions|
|Actor Network|[256, 128]|Policy network layers|
|Critic Network|[256, 128]|Value network layers|
|Batch Size|512|Training batch size|
|Replay Buffer|100000|Experience buffer size|

##### Implementation:
```python
class MARLGCN(nn.Module):
    def __init__(self, num_nodes, node_features, edge_features):
        super(MARLGCN, self).__init__()
        
        # GCN for state representation
        self.gcn1 = GCNLayer(node_features, 128)
        self.gcn2 = GCNLayer(128, 64)
        self.gcn3 = GCNLayer(64, 32)
        
        # Actor network (policy)
        self.actor = nn.Sequential(
            nn.Linear(32, 256),
            nn.ReLU(),
            nn.Linear(256, 128),
            nn.ReLU(),
            nn.Linear(128, num_nodes)  # action: select parking space
        )
        
        # Critic network (value function)
        self.critic = nn.Sequential(
            nn.Linear(32, 256),
            nn.ReLU(),
            nn.Linear(256, 128),
            nn.ReLU(),
            nn.Linear(128, 1)
        )
    
    def forward(self, node_features, adjacency):
        # Graph convolution
        h = self.gcn1(node_features, adjacency)
        h = self.gcn2(h, adjacency)
        h = self.gcn3(h, adjacency)
        
        # Policy and value
        action_probs = F.softmax(self.actor(h), dim=-1)
        value = self.critic(h)
        
        return action_probs, value
```
#### Reward Function:
The reward function balances multiple objectives:
```python
def compute_reward(vehicle, assigned_space, state):
    # Distance component (minimize travel distance)
    distance_reward = -distance(vehicle.entry_point, assigned_space.position) / MAX_DISTANCE
    
    # Time component (minimize search time)
    time_penalty = -vehicle.search_time / MAX_SEARCH_TIME
    
    # Utilization component (balance facility usage)
    zone_util = state.get_zone_utilization(assigned_space.zone)
    utilization_bonus = 0.2 if 0.6 < zone_util < 0.8 else -0.1
    
    # Success bonus (parking completed)
    completion_bonus = 1.0 if vehicle.successfully_parked else 0.0
    
    total_reward = (distance_reward + time_penalty + 
                   utilization_bonus + completion_bonus)
    return total_reward
```
#### Training Process:
The MARL-GCN model is trained using PPO (Proximal Policy Optimization):

1. Collect trajectories by running simulations
1. Compute advantages using Generalized Advantage Estimation (GAE)
1. Update policy network with clipped objective
1. Update value network with mean squared error
1. Repeat for multiple epochs

Training script execution:
```bash
python train_marl_gcn.py --config config/marl_config.yaml --episodes 10000
```
### 2.4 Simulation Controller
###### Figure 4: Simulation Controller Interface
The simulation controller orchestrates all system components and provides monitoring capabilities.
#### Core Controller Script:
```csharp
public class SimulationController : MonoBehaviour
{
    // Component references
    private ParkingEnvironment environment;
    private SUMOConnector sumoConnector;
    private MARLGCNAgent mlAgent;
    private MetricsCollector metricsCollector;
    
    // Simulation parameters
    public float simulationSpeed = 1.0f;
    public int maxVehicles = 50;
    
    // State tracking
    private List<Vehicle> activeVehicles;
    private float simulationTime;
    
    void Start()
    {
        InitializeComponents();
        StartCoroutine(SimulationLoop());
    }
    
    private void InitializeComponents()
    {
        environment = GetComponent<ParkingEnvironment>();
        sumoConnector = new SUMOConnector("parking.sumocfg");
        mlAgent = new MARLGCNAgent("models/trained_model.onnx");
        metricsCollector = new MetricsCollector();
        activeVehicles = new List<Vehicle>();
    }
    
    IEnumerator SimulationLoop()
    {
        while (true)
        {
            // Step SUMO simulation
            sumoConnector.Step();
            
            // Process new vehicle arrivals
            var newArrivals = sumoConnector.GetDepartedVehicles();
            foreach (var vehData in newArrivals)
            {
                SpawnVehicle(vehData);
            }
            
            // Update active vehicles
            UpdateVehicles();
            
            // Collect metrics
            metricsCollector.RecordFrame(simulationTime, activeVehicles);
            
            simulationTime += Time.deltaTime * simulationSpeed;
            yield return null;
        }
    }
    
    private void SpawnVehicle(VehicleData data)
    {
        GameObject vehiclePrefab = Resources.Load<GameObject>($"Vehicles/{data.type}");
        GameObject vehObj = Instantiate(vehiclePrefab, data.entryPoint, Quaternion.identity);
        
        Vehicle vehicle = vehObj.GetComponent<Vehicle>();
        vehicle.Initialize(data);
        
        // Get parking assignment from MARL-GCN
        ParkingSpace assignedSpace = mlAgent.AssignParkingSpace(vehicle, environment.GetState());
        vehicle.SetDestination(assignedSpace);
        
        activeVehicles.Add(vehicle);
    }
    
    private void UpdateVehicles()
    {
        for (int i = activeVehicles.Count - 1; i >= 0; i--)
        {
            Vehicle veh = activeVehicles[i];
            veh.UpdateNavigation();
            
            if (veh.HasCompleted())
            {
                metricsCollector.RecordCompletion(veh);
                Destroy(veh.gameObject);
                activeVehicles.RemoveAt(i);
            }
        }
    }
    
    public void SetSimulationSpeed(float speed)
    {
        simulationSpeed = Mathf.Clamp(speed, 0.1f, 10.0f);
        Time.timeScale = simulationSpeed;
    }
}
```
#### Vehicle Navigation Script:
```csharp
public class Vehicle : MonoBehaviour
{
    private ParkingSpace destination;
    private List<Vector3> navigationPath;
    private int currentWaypoint = 0;
    
    public float speed = 5.0f;
    public float rotationSpeed = 3.0f;
    
    private float entryTime;
    private float searchTime;
    private float totalDistance;
    
    public void SetDestination(ParkingSpace space)
    {
        destination = space;
        navigationPath = PathfindingUtility.FindPath(transform.position, space.position);
        entryTime = Time.time;
    }
    
    public void UpdateNavigation()
    {
        if (navigationPath == null || currentWaypoint >= navigationPath.Count)
            return;
        
        Vector3 targetPos = navigationPath[currentWaypoint];
        Vector3 direction = (targetPos - transform.position).normalized;
        
        // Rotate towards target
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 
                                             rotationSpeed * Time.deltaTime);
        
        // Move forward
        float step = speed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, step);
        totalDistance += step;
        
        // Check if waypoint reached
        if (Vector3.Distance(transform.position, targetPos) < 0.5f)
        {
            currentWaypoint++;
        }
        
        searchTime = Time.time - entryTime;
    }
    
    public bool HasCompleted()
    {
        return currentWaypoint >= navigationPath.Count && 
               Vector3.Distance(transform.position, destination.position) < 1.0f;
    }
    
    public VehicleMetrics GetMetrics()
    {
        return new VehicleMetrics
        {
            searchTime = this.searchTime,
            travelDistance = this.totalDistance,
            assignedSpace = destination.spaceID
        };
    }
}
```
#### User Interface:
The simulation controller includes a monitoring UI built with Unity's UI Toolkit:
##### UI Components:

- Real-time occupancy percentage display
- Active vehicle count
- Average search time meter
- Facility utilization heatmap
- Simulation speed control slider
- Start/Pause/Reset buttons
- Camera view controls (overview, follow vehicle, fixed positions)

#### Metrics Collection:
```csharp
public class MetricsCollector : MonoBehaviour
{
    private List<VehicleMetrics> completedVehicles;
    private StreamWriter csvWriter;
    
    void Start()
    {
        completedVehicles = new List<VehicleMetrics>();
        string filename = $"metrics_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        csvWriter = new StreamWriter(filename);
        csvWriter.WriteLine("VehicleID,EntryTime,SearchTime,Distance,AssignedSpace");
    }
    
    public void RecordCompletion(Vehicle vehicle)
    {
        VehicleMetrics metrics = vehicle.GetMetrics();
        completedVehicles.Add(metrics);
        
        csvWriter.WriteLine($"{vehicle.vehicleID},{metrics.entryTime}," +
                          $"{metrics.searchTime},{metrics.travelDistance}," +
                          $"{metrics.assignedSpace}");
        csvWriter.Flush();
    }
    
    public SimulationStatistics GetStatistics()
    {
        return new SimulationStatistics
        {
            totalVehicles = completedVehicles.Count,
            avgSearchTime = completedVehicles.Average(m => m.searchTime),
            avgDistance = completedVehicles.Average(m => m.travelDistance),
            maxSearchTime = completedVehicles.Max(m => m.searchTime),
            facilityUtilization = CalculateUtilization()
        };
    }
    
    void OnApplicationQuit()
    {
        csvWriter.Close();
    }
}
```
#### Integration Testing:
Before full simulation runs, integration tests verify component connectivity:
```csharp
[Test]
public void TestSUMOUnityIntegration()
{
    var connector = new SUMOConnector("test.sumocfg");
    connector.Step();
    var vehicles = connector.GetDepartedVehicles();
    Assert.IsNotNull(vehicles);
}

[Test]
public void TestMARLGCNInference()
{
    var agent = new MARLGCNAgent("models/test_model.onnx");
    var state = GenerateTestState();
    var assignment = agent.AssignParkingSpace(null, state);
    Assert.IsNotNull(assignment);
}
```
