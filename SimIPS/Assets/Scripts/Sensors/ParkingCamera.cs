using UnityEngine;
using System;

[RequireComponent(typeof(Camera))]
public class ParkingCamera : MonoBehaviour
{
    public string cameraID;
    public RenderTexture renderTexture;
    
    [Header("Calibration Points (0-1 range)")]
    public Vector2[] calibrationPoints = new Vector2[4];
    
    private Camera cam;
    private Texture2D tex;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(640, 480, 24);
        }
        cam.targetTexture = renderTexture;
        tex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
    }

    public byte[] CaptureFrame()
    {
        RenderTexture.active = renderTexture;
        tex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        tex.Apply();
        return tex.EncodeToJPG();
    }

    // Helper to get pixel coordinates of calibration points
    public Vector2Int[] GetPixelCalibrationPoints()
    {
        Vector2Int[] pixelPoints = new Vector2Int[4];
        for (int i = 0; i < 4; i++)
        {
            pixelPoints[i] = new Vector2Int(
                Mathf.RoundToInt(calibrationPoints[i].x * renderTexture.width),
                Mathf.RoundToInt(calibrationPoints[i].y * renderTexture.height)
            );
        }
        return pixelPoints;
    }
}
