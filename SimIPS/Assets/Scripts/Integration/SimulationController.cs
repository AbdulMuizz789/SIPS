using AsyncIO;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;

public class SimulationController : MonoBehaviour
{
    private ExchangeData _ExchangeData;
    private GameObject vehiclePrefab;
    private Dictionary<string, GameObject> vehicleObjects = new Dictionary<string, GameObject>();
    private string vehicleDataJson = "{}";
    private object vehicleDataLock = new object();
    
    private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

    private StreamWriter writer;

    [Header("Unity Step Length (seconds)")]
    public float unityStepLength = 0.10f;

    private float fixedTimeAccum = 0f; // Accumulator for FixedUpdate logging

    // New variables for timestamp offset
    private bool firstTimestampLogged = false;
    private float firstLoggedTime = 0f;

    [Header("Add all Junction GameObjects")]
    public GameObject junctions;           // drag ‘Junctions’ root here
    private readonly Dictionary<string, GameObject> junctionCache = new();

    [Header("Parking Environment")]
    public ParkingEnvironment parkingEnvironment;

    [Serializable]
    public class Vehicle
    {
        public string vehicle_id;
        public double[] position;
        public double angle;
        public string type;
        public float long_speed;
        public float vert_speed;
        public float lat_speed;
    }

    [Serializable]
    private class VehicleWrapper
    {
        public Vehicle[] vehicles;
    }

    [Serializable]
    public class ParkingMessage
    {
        public string vehicle_id;
        public string action; // "park", "unpark"
        public string parking_area_id;
        public int parking_index;
    }

    [Serializable]
    public class TrafficLight
    {
        public string junction_id;
        public string state;
    }

    [Serializable]
    private class TrafficLightsWrapper
    {
        public TrafficLight[] lights;
    }

    [System.Serializable]
    public class CarModel
    {
        public string sumoVehicleType;
        public GameObject unityVehiclePrefab;
    }

    [Header("Add Unity Vehicle Prefab (3DModel) according to Sumo Vehicle Type")]
    public List<CarModel> carModelsList = new List<CarModel>();

    /// cache last seen state per junction
    private Dictionary<string, string> _lastTlState = new();

    private static string LocateOrCreateResultsFolder()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        DirectoryInfo dir = new DirectoryInfo(projectRoot);

        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "Results");
            if (Directory.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        string fallback = Path.Combine(projectRoot, "Results");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private void Start()
    {
        if (parkingEnvironment == null)
        {
            parkingEnvironment = FindAnyObjectByType<ParkingEnvironment>();
        }

        // Try to load a default vehicle prefab if list is empty or as fallback
        vehiclePrefab = Resources.Load("EloraGold") as GameObject;

        _ExchangeData = GetComponent<ExchangeData>();
        if (_ExchangeData == null)
        {
            _ExchangeData = gameObject.AddComponent<ExchangeData>();
        }

        string sumoDataDir = LocateOrCreateResultsFolder();
        string logPath = Path.Combine(sumoDataDir, "vehicle_data_report.txt");
        writer = new StreamWriter(logPath, append: false, Encoding.UTF8);
        writer.WriteLine("timestep_time;vehicle_id;vehicle_x;vehicle_y;vehicle_z");
    }

    void Update()
    {
        try
        {
            // For now, we'll just send an empty list if needed.
            lock (vehicleDataLock)
            {
                vehicleDataJson = "{\"vehicles\":[]}";
            }

            while (mainThreadActions.TryDequeue(out var action))
            {
                action();
            }

            _ExchangeData.SignalFrameReady();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception in Update(): {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void FixedUpdate()
    {
        if (!RecordingManager.startRecordingFromZero)
        {
            return;
        }

        fixedTimeAccum += Time.fixedDeltaTime;
        if (fixedTimeAccum >= unityStepLength - 0.002)
        {
            float currentTime = Time.fixedTime;

            if (!firstTimestampLogged)
            {
                firstLoggedTime = currentTime;
                firstTimestampLogged = true;
            }

            float logTime = currentTime - firstLoggedTime;
            LogVehicleData(logTime);
            fixedTimeAccum = 0f;
        }
    }

    private void LogVehicleData(float relativeLogTime)
    {
        foreach (var kvp in vehicleObjects)
        {
            string vehicleId = kvp.Key;
            GameObject vehicleObj = kvp.Value;
            Vector3 pos = vehicleObj.transform.position;
            writer.WriteLine($"{relativeLogTime:F3};{vehicleId};{pos.x:F2};{pos.y:F2};{pos.z:F2}");
        }
    }

    private void OnDestroy()
    {
        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer = null;
        }
    }

    public void EnqueueMainThreadAction(Action action)
    {
        mainThreadActions.Enqueue(action);
    }

    public string GetVehicleDataJson()
    {
        lock (vehicleDataLock)
        {
            return vehicleDataJson;
        }
    }

    public void HandleMessage(string message)
    {
        CommonMessage common = JsonUtility.FromJson<CommonMessage>(message);

        if (common == null || string.IsNullOrEmpty(common.type))
        {
            Debug.LogError("Received message with no type field or invalid JSON.");
            return;
        }

        if (common.type == "command")
        {
            if (common.command == "START_RECORDING")
            {
                RecordingManager.startRecordingFromZero = true;
                RecordingManager.recordingStartTime = Time.time;
                Debug.Log("Received START_RECORDING command from SUMO.");
                firstTimestampLogged = false;
                firstLoggedTime = 0f;
            }
            else if (common.command == "STOP_RECORDING")
            {
                RecordingManager.startRecordingFromZero = false;
                Debug.Log("Received STOP_RECORDING command from SUMO.");

                // Remove all vehicles
                foreach (var vid in vehicleObjects.Keys.ToList())
                {
                    GameObject obj = vehicleObjects[vid];
                    Destroy(obj);
                }
                vehicleObjects.Clear();
            }
            return;
        }
        else if (common.type == "vehicles")
        {
            VehicleWrapper wrapper = JsonUtility.FromJson<VehicleWrapper>(message);
            Vehicle[] vehicleArray = wrapper.vehicles;
            List<Vehicle> vehiclesData = vehicleArray != null ? vehicleArray.ToList() : new List<Vehicle>();

            HashSet<string> incomingVehicleIds = new HashSet<string>(vehiclesData.Select(v => v.vehicle_id));
            var vehiclesToRemove = vehicleObjects.Keys.Where(id => !incomingVehicleIds.Contains(id)).ToList();

            foreach (var id in vehiclesToRemove)
            {
                GameObject vehicleToDestroy = vehicleObjects[id];
                GameObject.Destroy(vehicleToDestroy);
                vehicleObjects.Remove(id);
            }

            foreach (var vehicle in vehiclesData)
            {
                Vector3 newPosition = new Vector3((float)vehicle.position[0], (float)vehicle.position[2], (float)vehicle.position[1]);
                Quaternion newRotation = Quaternion.Euler(0, (float)vehicle.angle, 0);
                float vehicleSpeed = vehicle.long_speed;
                float vehiclevertical_speed = vehicle.vert_speed;
                float vehiclelateral_speed = vehicle.lat_speed;

                if (vehicleObjects.ContainsKey(vehicle.vehicle_id))
                {
                    GameObject existingVehicle = vehicleObjects[vehicle.vehicle_id];
                    VehicleController vehicleController = existingVehicle.GetComponent<VehicleController>();
                    if (vehicleController != null)
                    {
                        vehicleController.UpdateTarget(newPosition, newRotation, vehicleSpeed, vehiclevertical_speed, vehiclelateral_speed);
                    }
                }
                else
                {
                    GameObject prefabToInstantiate = vehiclePrefab;
                    foreach (CarModel carModel in carModelsList)
                    {
                        if (carModel.sumoVehicleType == vehicle.type)
                        {
                            prefabToInstantiate = carModel.unityVehiclePrefab;
                            break;
                        }
                    }

                    if (prefabToInstantiate == null)
                    {
                        // Fallback to a simple cube if no prefab is found
                        prefabToInstantiate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        prefabToInstantiate.transform.localScale = new Vector3(2, 1.5f, 4);
                    }

                    GameObject newVehicle = GameObject.Instantiate(prefabToInstantiate, newPosition, newRotation);
                    newVehicle.name = vehicle.vehicle_id;
                    VehicleController vc = newVehicle.GetComponent<VehicleController>();
                    if (vc == null)
                    {
                        vc = newVehicle.AddComponent<VehicleController>();
                    }

                    vc.UpdateTarget(newPosition, newRotation, vehicleSpeed, vehiclevertical_speed, vehiclelateral_speed);
                    vehicleObjects.Add(vehicle.vehicle_id, newVehicle);
                }
            }
        }
        else if (common.type == "trafficlights")
        {
            var wrapper = JsonUtility.FromJson<TrafficLightsWrapper>(message);
            if (wrapper != null && wrapper.lights != null)
            {
                foreach (var tl in wrapper.lights)
                {
                    if (!_lastTlState.TryGetValue(tl.junction_id, out var prev) || prev != tl.state)
                    {
                        ChangeTrafficStatus(tl.junction_id, tl.state);
                        _lastTlState[tl.junction_id] = tl.state;
                    }
                }
            }
        }
        else if (common.type == "parking")
        {
            var parkMsg = JsonUtility.FromJson<ParkingMessage>(message);
            if (parkMsg != null)
            {
                HandleParkingEvent(parkMsg.parking_area_id, parkMsg.parking_index, parkMsg.action == "park");
            }
        }
    }

    private void HandleParkingEvent(string areaId, int index, bool occupy)
    {
        if (parkingEnvironment != null)
        {
            var space = parkingEnvironment.GetSpaceByID(areaId, index);
            if (space != null)
            {
                // Set the isOccupied value
                space.isOccupied = occupy;
                Debug.Log($"Space {areaId}[{index}] is now {(occupy ? "occupied" : "free")}");
            }
            else
            {
                Debug.LogWarning($"Could not find parking space {areaId}[{index}]");
            }
        }
    }

    public void EnqueueOnMainThread(string message)
    {
        EnqueueMainThreadAction(() => HandleMessage(message));
    }

    private void ChangeTrafficStatus(string junctionID, string state)
    {
        if (junctions == null) return;

        if (!junctionCache.TryGetValue(junctionID, out GameObject junctionGO))
        {
            var t = junctions.transform.Find(junctionID);
            if (t == null) { return; }
            junctionGO = t.gameObject;
            junctionCache[junctionID] = junctionGO;
        }

        for (int i = 0; i < state.Length; i++)
        {
            var headTransform = junctionGO.transform.Find($"Head{i}");
            if (headTransform == null) continue;
            SetSignalState(state[i], headTransform.gameObject);
        }
    }

    private void SetSignalState(char c, GameObject head)
    {
        var green = FindChildRecursive(head.transform, "green_light");
        var yellow = FindChildRecursive(head.transform, "yellow_light");
        var red = FindChildRecursive(head.transform, "red_light");
        if (green) green.SetActive(c == 'G' || c == 'g');
        if (yellow) yellow.SetActive(c == 'y' || c == 'Y');
        if (red) red.SetActive(!(c == 'G' || c == 'g' || c == 'y' || c == 'Y'));
    }

    private GameObject FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child.gameObject;
            var found = FindChildRecursive(child, name);
            if (found) return found;
        }
        return null;
    }
}
