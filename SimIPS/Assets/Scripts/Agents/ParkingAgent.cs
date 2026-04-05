using System;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
//using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

public class ParkingAgent : Agent
{
    [SerializeField] private float moveSpeed = 5f;
    public InputAction throttleAction;
    public InputAction steerAction;

    private CarControl carControl;

    private void Start()
    {
        // Get the CarControl component
        carControl = transform.parent.GetComponent<CarControl>();
        carControl.maxSpeed = moveSpeed;

        // Initialize the Input Actions
        throttleAction = InputSystem.actions.FindAction("Throttle");
        steerAction = InputSystem.actions.FindAction("Steer");
    }

    // Enable the actions when the agent is enabled
    private void OnEnable() 
    {
        base.OnEnable();
        throttleAction.Enable();
        steerAction.Enable();
    }

    // Disable the actions when the agent is disabled
    private void OnDisable()
    {
        throttleAction.Disable();
        steerAction.Disable();
    }

    //public override void OnEpisodeBegin()
    //{
    //    // Reset car position and velocity here
    //}

    //public override void CollectObservations(VectorSensor sensor)
    //{
    //    // Tell the brain where the target parking spot is
    //    // sensor.AddObservation(transform.localPosition); 
    //}

    public override void OnActionReceived(ActionBuffers actions)
    {
        // actions.ContinuousActions[0] = Steering (-1 to 1)
        // actions.ContinuousActions[1] = Throttle (-1 to 1)
        float steer = actions.ContinuousActions[0];
        float throttle = actions.ContinuousActions[1];

        // Move the car
        carControl.SetInput(throttle, steer);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;

        // Get the current value from the Input Actions
        float moveInput = throttleAction.ReadValue<float>();
        float steerInput = steerAction.ReadValue<float>();

        // Mapping WASD/Arrows to ML-Agent Actions
        continuousActions[0] = steerInput; // A/D or Left/Right
        continuousActions[1] = moveInput;   // W/S or Up/Down
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("ParkingSpace"))
        {
            Debug.Log("Goal Reached!");
        }
    }
}