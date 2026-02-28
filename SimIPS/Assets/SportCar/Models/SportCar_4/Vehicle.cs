using UnityEngine;
using UnityEngine.AI;

public class Vehicle : MonoBehaviour
{
    private NavMeshAgent agent;
    private ParkingManager parkingManager;
    private Transform assignedSlot;
    private bool isParked = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        parkingManager = FindObjectOfType<ParkingManager>();

        if (parkingManager != null)
        {
            assignedSlot = parkingManager.GetFreeSlot();

            if (assignedSlot != null)
            {
                agent.SetDestination(assignedSlot.position);
            }
        }
    }

    void Update()
    {
        if (!isParked && assignedSlot != null)
        {
            if (!agent.pathPending && agent.remainingDistance <= 0.5f)
            {
                ParkCar();
            }
        }
    }

    void ParkCar()
    {
        agent.isStopped = true;

        transform.position = assignedSlot.position;
        transform.rotation = assignedSlot.rotation;

        transform.SetParent(assignedSlot);
        isParked = true;
    }
}