using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

public class SUMOReceiver : MonoBehaviour
{
    public int listenPort = 9000;
    public ParkingEnvironment parkingEnvironment;
    public GameObject vehiclePrefab;

    private Thread receiveThread;
    private TcpListener tcpListener;
    private TcpClient connectedClient;
    private NetworkStream stream;
    private bool running;

    private ConcurrentQueue<string> dataQueue = new();
    private Dictionary<string, GameObject> activeVehicles = new();

    void Start()
    {
        if (parkingEnvironment == null)
        {
            parkingEnvironment = FindAnyObjectByType<ParkingEnvironment>();
        }
        StartListening();
    }
    
    void StartListening()
    {
        receiveThread = new Thread(new ThreadStart(ListenerThread));
        receiveThread.IsBackground = true;
        receiveThread.Start();
        running = true;
    }
    
    void ListenerThread()
    {
        try
        {
            tcpListener = new TcpListener(IPAddress.Any, listenPort);
            tcpListener.Start();
            Debug.Log("SUMOReceiver listening on port " + listenPort);
            
            connectedClient = tcpListener.AcceptTcpClient();
            stream = connectedClient.GetStream();
            
            byte[] buffer = new byte[8192];
            var reader = new System.IO.StreamReader(stream, Encoding.UTF8);

            while (running)
            {
                byte[] readySignal = Encoding.UTF8.GetBytes("Ready\n");
                stream.Write(readySignal, 0, readySignal.Length);

                string line = reader.ReadLine();
                if (line == null) break;

                dataQueue.Enqueue(line);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("SUMOReceiver thread error: " + e.Message);
        }
    }

    void Update()
    {
        // Process everything currently in the queue
        while (dataQueue.TryDequeue(out string jsonResponse))
        {
            ProcessMessage(jsonResponse);
        }
    }

    void ProcessMessage(string json)
    {
        try
        {
            Debug.Log("Received JSON: " + json);
            var vehicles = JsonConvert.DeserializeObject<Dictionary<string, VehicleUpdate>>(json);
            if (vehicles == null) return;

            foreach (var kvp in vehicles)
            {
                string id = kvp.Key;
                VehicleUpdate data = kvp.Value;

                switch (data.action)
                {
                    case "depart":
                        HandleDepart(id, data);
                        break;

                    case "update":
                        UpdateVehicleTransforms(id, data);
                        break;

                    case "park":
                        Debug.Log($"Vehicle {id} is parking in area {data.parking_area_id} at index {data.parking_index}");
                        // Logic: You might want to disable the SUMO-driven renderer and 
                        // enable your ML-Agent controller here for precision parking.
                        HandleParkingEvent(data.parking_area_id, data.parking_index, true);
                        break;

                    case "unpark":
                        Debug.Log($"Vehicle {id} has finished parking.");
                        HandleParkingEvent(data.parking_area_id, data.parking_index, false);
                        break;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Failed to process message: " + e.Message);
        }
    }

    void UpdateVehicleTransforms(string id, VehicleUpdate data)
    {
        if (activeVehicles.ContainsKey(id))
        {
            // Map SUMO (X, Y) to Unity (X, Z)
            Vector3 position = new Vector3((float)data.x, 0, (float)data.y);
            Quaternion rotation = Quaternion.Euler(0, (float)data.angle, 0);

            activeVehicles[id].transform.position = position;
            activeVehicles[id].transform.rotation = rotation;
        }
    }

    void HandleDepart(string id, VehicleUpdate data)
    {
        if (!activeVehicles.ContainsKey(id))
        {
            // Map SUMO (X, Y) to Unity (X, Z)
            Vector3 position = new Vector3((float)data.x, 0, (float)data.y);
            Quaternion rotation = Quaternion.Euler(0, (float)data.angle, 0);

            GameObject v = Instantiate(vehiclePrefab, position, rotation);
            v.name = id;
            activeVehicles.Add(id, v);
        }
    }
    
    void HandleParkingEvent(string areaId, int index, bool occupy)
    {
        if (parkingEnvironment != null)
        {
            var space = parkingEnvironment.GetSpaceByID(areaId, index);
            if (space != null)
            {
                var parkingSpace = space.GetComponent<ParkingSpace>();
                if (parkingSpace != null)
                {
                    // Directly set the isOccupied value without reflection
                    parkingSpace.isOccupied = occupy;
                    
                    // You can also add visual feedback
                    if (occupy)
                    {
                        // Visual indication for occupied space
                        Debug.Log($"Space {areaId}[{index}] is now occupied");
                    }
                    else
                    {
                        // Visual indication for free space
                        Debug.Log($"Space {areaId}[{index}] is now free");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"Could not find parking space {areaId}[{index}]");
            }
        }
    }
    
    void OnDestroy()
    {
        running = false;
        if (receiveThread != null && receiveThread.IsAlive)
            receiveThread.Abort();
        if (stream != null)
            stream.Close();
        if (connectedClient != null)
            connectedClient.Close();
        if (tcpListener != null)
            tcpListener.Stop();
    }
}

[Serializable]
public class VehicleUpdate
{
    public string action;
    public double x;
    public double y;
    public double angle;
    public string parking_area_id;
    public int parking_index;
}