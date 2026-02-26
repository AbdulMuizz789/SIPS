using System.Collections.Generic;
using UnityEngine;

public class ParkingEnvironment : MonoBehaviour
{
    [System.Serializable]
    public class ParkingZoneMapping
    {
        public string sumoAreaID; // e.g., "parking_zone_a"
        public List<ParkingSpace> spaces; // Drag your prefabs here in the Inspector
    }

    public List<ParkingZoneMapping> zoneMappings;

    private Dictionary<string, List<ParkingSpace>> parkingLookup;

    void Start()
    {
        // Build the lookup dictionary for fast access
        parkingLookup = new Dictionary<string, List<ParkingSpace>>();
        foreach (var mapping in zoneMappings)
        {
            parkingLookup[mapping.sumoAreaID] = mapping.spaces;
        }
    }

    // Method used by the SUMO-Unity Bridge to find a specific spot
    public ParkingSpace GetSpaceByID(string sumoAreaID, int index)
    {
        if (parkingLookup.ContainsKey(sumoAreaID) && parkingLookup[sumoAreaID].Count > index)
        {
            return parkingLookup[sumoAreaID][index];
        }
        return null;
    }

    //public ParkingSpace GetAvailableSpace(Vector3 entryPoint)
    //{
    //    // Query available spaces
    //    // Return optimal space based on criteria
    //}
}