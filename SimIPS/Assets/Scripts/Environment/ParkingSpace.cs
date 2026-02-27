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

    [Header("Visuals")]
    [SerializeField] private Material occupiedMaterial;
    [SerializeField] private Material availableMaterial;
    [SerializeField] private Renderer parkingRenderer;

    // Sets the visual material based on type (optional helper)
    public void SetType(ParkingType newType)
    {
        type = newType;
        // Logic to change material colour would go here
    }

    void Start()
    {
        if (parkingRenderer == null)
        {
            parkingRenderer = GetComponent<Renderer>();
        }
        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        if (parkingRenderer != null && occupiedMaterial != null && availableMaterial != null)
        {
            parkingRenderer.material = isOccupied ? occupiedMaterial : availableMaterial;
        }
    }

    void Update()
    {
        // Ensure visuals stay updated if isOccupied is changed directly
        if (parkingRenderer != null)
        {
            Material currentMaterial = parkingRenderer.material;
            Material expectedMaterial = isOccupied ? occupiedMaterial : availableMaterial;
            
            if (currentMaterial != expectedMaterial && expectedMaterial != null)
            {
                parkingRenderer.material = expectedMaterial;
            }
        }
    }
}
