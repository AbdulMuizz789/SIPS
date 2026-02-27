using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class SUMOConnector : MonoBehaviour
{
    public int unityPort = 9000;
    private TcpListener _listener;
    private TcpClient _client;
    private NetworkStream _stream;

    void Start()
    {
        // 1) start listening on port 9000
        _listener = new TcpListener(IPAddress.Any, unityPort);
        _listener.Start();
        Debug.Log($"[SUMOConnector] Listening on port {unityPort} …");

        // 2) accept the incoming connection asynchronously
        _listener.BeginAcceptTcpClient(OnSUMOConnected, null);
    }

    private void OnSUMOConnected(IAsyncResult ar)
    {
        _client = _listener.EndAcceptTcpClient(ar);
        _stream = _client.GetStream();
        Debug.Log("[SUMOConnector] Python bridge connected!");
        // from here on you can read/write to _stream
    }

    public void Step()
    {
        // called once per frame (or in your SimulationController)
        // read messages from _stream or write data back
    }

    void OnApplicationQuit()
    {
        _stream?.Close();
        _client?.Close();
        _listener?.Stop();
    }
}
