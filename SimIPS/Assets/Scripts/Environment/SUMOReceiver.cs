using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

public class SUMOReceiver : MonoBehaviour
{
    public int listenPort = 9000;
    public ParkingEnvironment parkingEnvironment;
    
    private Thread receiveThread;
    private TcpListener tcpListener;
    private TcpClient connectedClient;
    private NetworkStream stream;
    private bool running;
    
    void Start()
    {
        if (parkingEnvironment == null)
        {
            parkingEnvironment = FindObjectOfType<ParkingEnvironment>();
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
            
            byte[] buffer = new byte[4096];
            StringBuilder messageBuilder = new StringBuilder();
            
            while (running)
            {
                if (stream.DataAvailable)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        string chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        messageBuilder.Append(chunk);

                        // Process complete JSON messages (one per line)
                        string data = messageBuilder.ToString();
                        int newlineIndex;
                        while ((newlineIndex = data.IndexOf('\n')) >= 0)
                        {
                            string line = data.Substring(0, newlineIndex).Trim();
                            data = data.Substring(newlineIndex + 1);
                            if (!string.IsNullOrEmpty(line))
                            {
                                ProcessMessage(line);
                            }
                        }
                        messageBuilder.Clear();
                        messageBuilder.Append(data);
                    }
                }
                Thread.Sleep(10);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("SUMOReceiver thread error: " + e.Message);
        }
    }
    
    void ProcessMessage(string json)
    {
        try
        {
            // Simple JSON parsing (we assume small, simple objects)
            // You could use a proper library like JsonUtility for more complex messages
            if (json.Contains("\"action\""))
            {
                // Very basic extraction (for demonstration; replace with JsonUtility if needed)
                if (json.Contains("\"action\": \"park\""))
                {
                    string areaId = ExtractValue(json, "parking_area_id");
                    string indexStr = ExtractValue(json, "parking_index");
                    if (areaId != null && indexStr != null && int.TryParse(indexStr, out int index))
                    {
                        Debug.Log($"Parking event: area {areaId}, index {index}");
                        
                        // Queue the action for the main thread
                        MainThreadDispatcher.Enqueue(() => {
                            HandleParkingEvent(areaId, index, true);
                        });
                    }
                }
                else if (json.Contains("\"action\": \"unpark\""))
                {
                    string areaId = ExtractValue(json, "parking_area_id");
                    string indexStr = ExtractValue(json, "parking_index");
                    if (areaId != null && indexStr != null && int.TryParse(indexStr, out int index))
                    {
                        Debug.Log($"Unpark event: area {areaId}, index {index}");
                        
                        // Queue the action for the main thread
                        MainThreadDispatcher.Enqueue(() => {
                            HandleParkingEvent(areaId, index, false);
                        });
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Failed to process message: " + e.Message);
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
    
    // Helper to extract simple JSON values (only works for flat keys without nested objects)
    private string ExtractValue(string json, string key)
    {
        string searchKey = "\"" + key + "\":";
        int start = json.IndexOf(searchKey);
        if (start < 0) return null;
        start += searchKey.Length;
        int end = json.IndexOf(',', start);
        if (end < 0) end = json.IndexOf('}', start);
        if (end < 0) return null;
        string val = json.Substring(start, end - start).Trim();
        if (val.StartsWith("\"") && val.EndsWith("\""))
            val = val.Substring(1, val.Length - 2);
        return val;
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
