using System.Collections;
using AOT;
using UnityEngine;
using System.Runtime.InteropServices;

public class VisionProCameraAccessManager : MonoBehaviour
{
    public static VisionProCameraAccessManager Instance { get; private set; }
    public Material PreviewMaterial;
    string tempBase64String = null;
    float skipSeconds = 0.1f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this.gameObject); }
        else { Instance = this; DontDestroyOnLoad(this.gameObject); }
    }

    void Start()
    {
        // Start the main camera capture
        StartMainCameraCapture();

        // Call ApplyBase64StringToMaterial function continuously
        StartCoroutine(CallFunctionContinuously());
    }

    // Call ApplyBase64StringToMaterial function continuously
    IEnumerator CallFunctionContinuously()
    {
        while (true)
        {
            yield return new WaitForSeconds(skipSeconds);
            ApplyBase64StringToMaterial();
        }
    }

    // Apply Base64String to Material
    void ApplyBase64StringToMaterial()
    {
        if (tempBase64String == null) { return; }

        if (PreviewMaterial.mainTexture != null)
        {
            Texture2D oldTexture = (Texture2D)PreviewMaterial.mainTexture;
            Object.Destroy(oldTexture);
        }

        Texture2D tempTexture = Base64ToTexture2D(tempBase64String);
        PreviewMaterial.mainTexture = tempTexture;
    }

    void OnEnable()
    {
        SetNativeCallbackOfCameraAccess(CallbackFromNative);
    }

    void OnDisable()
    {
        SetNativeCallbackOfCameraAccess(null);
    }

    public Texture2D GetMainCameraTexture()
    {
        return PreviewMaterial.mainTexture as Texture2D;
    }

    // Convert Base64String to Texture2D
    static Texture2D Base64ToTexture2D(string base64)
    {
        try
        {
            byte[] imageBytes = System.Convert.FromBase64String(base64);
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(imageBytes)) { return texture; }
            else { Debug.LogError("Failed to load image from byte array."); return null; }
        }
        catch { Debug.LogError("Failed to convert base64 string to texture2D."); }
        return null;
    }

    delegate void CallbackDelegate(string command);
    [MonoPInvokeCallback(typeof(CallbackDelegate))]
    static void CallbackFromNative(string command)
    {
        Instance.tempBase64String = command;
    }

#if UNITY_VISIONOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern void SetNativeCallbackOfCameraAccess(CallbackDelegate callback);
    [DllImport("__Internal")]
    static extern void StartMainCameraCapture();
#else
    static void SetNativeCallbackOfCameraAccess(CallbackDelegate callback) { }
    static void StartMainCameraCapture() { }
#endif

}
