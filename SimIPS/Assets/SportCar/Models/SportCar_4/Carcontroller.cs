using UnityEngine;
using UnityEngine.AI;

public class CarController : MonoBehaviour
{
    public ParkingManager parkingManager;
    private NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        Transform freeSlot = parkingManager.GetFreeSlot();

        if (freeSlot != null)
        {
            agent.SetDestination(freeSlot.position);
        }
    }
}