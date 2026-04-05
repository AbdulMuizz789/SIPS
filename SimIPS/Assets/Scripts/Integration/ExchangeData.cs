using AsyncIO;
using NetMQ;
using NetMQ.Sockets;
using System.Threading;
using UnityEngine;
using System;

[System.Serializable]
public class CommonMessage
{
    public string type;    // "command", "vehicles", or "trafficlights"
    public string command; // Used if type == "command"
}

public static class RecordingManager
{
    public static bool startRecordingFromZero = false;
    public static float recordingStartTime = 0f;
}

public class ExchangeData : MonoBehaviour
{
    private SimulationController _SimulationController;

    // Thread for background communication
    private Thread _communicationThread;
    private bool _isRunning = false;

    private readonly SemaphoreSlim _frameRendered = new SemaphoreSlim(0, 1);

    public void Start()
    {
        _SimulationController = GetComponent<SimulationController>();

        // Start the communication thread
        _isRunning = true;
        _communicationThread = new Thread(Run);
        _communicationThread.IsBackground = true; 
        _communicationThread.Start();
    }

    // Called by SimulationController at the end of Update(), after all
    // messages for this frame have been processed and scene state is settled.
    public void SignalFrameReady()
    {
        // Only release if not already signalled (avoid runaway accumulation)
        if (_frameRendered.CurrentCount == 0)
            _frameRendered.Release();
    }

    void OnDestroy()
    {
        // Stop the communication thread
        _isRunning = false;
        _frameRendered.Release(); // unblock thread so it can exit
        if (_communicationThread != null && _communicationThread.IsAlive)
        {
            _communicationThread.Join(2000);
        }

        NetMQConfig.Cleanup();
        Debug.Log("ExchangeData thread terminated gracefully.");
    }

    private void Run()
    {
        ForceDotNet.Force();

        try
        {
            using (var subSocket = new SubscriberSocket())
            using (var dealerSocket = new DealerSocket())
            using (var reqSocket = new RequestSocket())
            {
                // Connect to SUMO bridge's PUB socket
                subSocket.Connect("tcp://localhost:5556");
                subSocket.Subscribe("");
                subSocket.Options.ReceiveHighWatermark = 1000;

                // Connect to SUMO bridge's ROUTER socket
                dealerSocket.Connect("tcp://localhost:5557");
                dealerSocket.Options.SendHighWatermark = 1000;

                Thread.Sleep(200); // Brief pause to ensure connections are established

                // Connect to SUMO bridge's REP socket
                reqSocket.Connect("tcp://localhost:5558");

                // --- Initial Sync ---
                Debug.Log("Syncing with SUMO...");
                reqSocket.SendFrame("Ready");
                reqSocket.ReceiveFrameString();
                Debug.Log("Sync complete.");

                while (_isRunning)
                {
                    try
                    {
                        // --- Receive Data from SUMO (Drain PUB socket) ---
                        string sumoDataJson;
                        while (subSocket.TryReceiveFrameString(out sumoDataJson))
                        {
                            _SimulationController.EnqueueOnMainThread(sumoDataJson);
                        }

                        // --- Wait for the main thread to signal that the frame is rendered ---
                        _frameRendered.Wait();
                        if (!_isRunning) break;

                        // --- Request Next Step from SUMO ---
                        reqSocket.SendFrame("Ready");
                        reqSocket.ReceiveFrameString();

                        // --- Send Data to SUMO (Optional/Ego vehicle) ---
                        string vehicleDataJson = _SimulationController.GetVehicleDataJson();
                        dealerSocket.TrySendFrame(vehicleDataJson);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Exception in background thread loop: {ex.Message}\n{ex.StackTrace}");
                        _isRunning = false;
                        break;
                    }

                    // Sleep briefly to prevent 100% CPU usage
                    Thread.Sleep(1);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception in background thread: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            NetMQConfig.Cleanup();
            Debug.Log("ExchangeData thread terminated gracefully.");
        }
    }
}
