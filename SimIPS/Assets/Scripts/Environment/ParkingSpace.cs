using UnityEngine;

public enum ParkingType
{
    Standard,   // White
    Accessible, // Blue
    EV          // Green
}

public class ParkingSpace : MonoBehaviour
{
    [Header("Configuration")]
    public string spaceID;
    public ParkingType type = ParkingType.Standard;

    [Header("Status")]
    public bool isOccupied = false;

    // Sets the visual material based on type (optional helper)
    public void SetType(ParkingType newType)
    {
        type = newType;
        // Logic to change material colour would go here
    }
}