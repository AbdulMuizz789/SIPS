using UnityEngine;
using System.Collections.Generic;
using System;

public class MainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> executionQueue = new Queue<Action>();
    private static MainThreadDispatcher instance = null;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    void Update()
    {
        lock (executionQueue)
        {
            while (executionQueue.Count > 0)
            {
                Action action = executionQueue.Dequeue();
                try
                {
                    action?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error executing action on main thread: {e.Message}");
                }
            }
        }
    }

    public static void Enqueue(Action action)
    {
        if (action == null)
            return;

        lock (executionQueue)
        {
            executionQueue.Enqueue(action);
        }
    }

    public static bool Exists()
    {
        return instance != null;
    }

    public static MainThreadDispatcher Instance()
    {
        if (!Exists())
        {
            throw new Exception("MainThreadDispatcher could not find instance. Please add MainThreadDispatcher to a GameObject in your scene.");
        }
        return instance;
    }
}
