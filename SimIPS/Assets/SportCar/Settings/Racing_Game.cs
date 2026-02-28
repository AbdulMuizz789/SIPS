#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class Startup
{
    static Startup()    
    {
        EditorPrefs.SetInt("showCounts_sportcarcgb12", EditorPrefs.GetInt("showCounts_sportcarcgb12") + 1);

        if (EditorPrefs.GetInt("showCounts_sportcarcgb12") == 1)       
        {
            Application.OpenURL("https://assetstore.unity.com/packages/slug/362252");
            // System.IO.File.Delete("Assets/SportCar/Racing_Game.cs");
        }
    }     
}
#endif
