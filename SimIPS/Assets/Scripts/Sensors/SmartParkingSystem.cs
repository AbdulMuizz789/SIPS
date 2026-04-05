using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class SmartParkingSystem : MonoBehaviour
{
    [Header("Settings")]
    public float captureInterval = 1.0f;
    public string pythonAddress = "tcp://localhost:5560";
    
    [Header("Cameras")]
    public List<ParkingCamera> parkingCameras = new List<ParkingCamera>();

    private Thread _communicationThread;
    private bool _isRunning = false;
    private ConcurrentQueue<byte[]> _imageQueue = new ConcurrentQueue<byte[]>();
    private ConcurrentQueue<string> _metadataQueue = new ConcurrentQueue<string>();

    void Start()
    {
        _isRunning = true;
        _communicationThread = new Thread(RunCommunication);
        _communicationThread.IsBackground = true;
        _communicationThread.Start();

        StartCoroutine(CaptureLoop());
    }

    IEnumerator CaptureLoop()
    {
        while (_isRunning)
        {
            foreach (var pc in parkingCameras)
            {
                if (pc == null) continue;

                byte[] jpgData = pc.CaptureFrame();
                
                // Prepare metadata
                var metadata = new {
                    cameraID = pc.cameraID,
                    timestamp = Time.time,
                    calibrationPoints = pc.GetPixelCalibrationPoints()
                };
                string jsonMetadata = JsonUtility.ToJson(metadata);

                _imageQueue.Enqueue(jpgData);
                _metadataQueue.Enqueue(jsonMetadata);
            }
            yield return new WaitForSeconds(captureInterval);
        }
    }

    void OnDestroy()
    {
        _isRunning = false;
        if (_communicationThread != null && _communicationThread.IsAlive)
        {
            _communicationThread.Join(1000);
        }
        NetMQConfig.Cleanup();
    }

    private void RunCommunication()
    {
        ForceDotNet.Force();
        using (var pushSocket = new PushSocket())
        {
            pushSocket.Connect(pythonAddress);
            Debug.Log($"SmartParkingSystem connected to {pythonAddress}");

            while (_isRunning)
            {
                if (_imageQueue.TryDequeue(out byte[] img) && _metadataQueue.TryDequeue(out string meta))
                {
                    // Send multipart message: [Metadata JSON, Image Bytes]
                    var msg = new NetMQMessage();
                    msg.Append(meta);
                    msg.Append(img);
                    pushSocket.SendMultipartMessage(msg);
                }
                else
                {
                    Thread.Sleep(10);
                }
            }
        }
    }
}

// Simple serializable struct for JsonUtility
[Serializable]
public class CameraMetadata
{
    public string cameraID;
    public float timestamp;
    public Vector2Int[] calibrationPoints;
}
