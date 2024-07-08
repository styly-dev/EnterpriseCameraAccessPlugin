using UnityEngine;
using UnityEditor;

/// <summary>
/// Install Pico Unity Integration SDK package if not installed.
/// </summary>
public class Install_PicoUnityIntegrationSDK : MonoBehaviour
{
    [InitializeOnLoadMethod]
    static void Install_PicoUnityIntegrationSDK_Package()
    {
#if !USE_PICOXR
        string GitURL = "https://github.com/picoxr/GetCameraFrame.git?path=PICO%20Unity%20Integration%20SDK_250";
        var request = UnityEditor.PackageManager.Client.Add(GitURL);
        while (!request.IsCompleted) { }
        if (request.Error != null) { Debug.LogError(request.Error.message); }
#endif
    }
}
