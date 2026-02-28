using UnityEngine;

public class ParkingManager : MonoBehaviour
{
    public Transform[] parkingSlots;

    public Transform GetFreeSlot()
    {
        foreach (Transform slot in parkingSlots)
        {
            if (slot.childCount == 0)
            {
                return slot;
            }
        }
        return null;
    }
}
