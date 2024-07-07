using System.Collections;
using AOT;
using UnityEngine;
using System.Runtime.InteropServices;
using UnityEngine.UI;

public class VisionProCameraAccessManager : MonoBehaviour
{
    public static VisionProCameraAccessManager Instance { get; private set; }
    public Material PreviewMaterial;

    [Tooltip("WebCam will be used for Editor mode or smartphone. Default camera is used if this field is empty.")]
    public string WebCamDeviceName = "";

    private WebCamTexture webCamTexture;
    private Texture2D tmpTexture = null;
    private string tempBase64String = null;
    private float skipSeconds = 0.1f;

    /// <summary>
    /// Get Vision Pro main camera image as texture2D.
    /// </summary>
    /// <returns></returns>
    public Texture2D GetMainCameraTexture2D()
    {
        if (IsVisionOs())
        {
            return Base64ToTexture2D(tempBase64String);
        }
        else
        {
            return tmpTexture;
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this.gameObject); }
        else { Instance = this; DontDestroyOnLoad(this.gameObject); }
    }

    void OnEnable()
    {
#if UNITY_VISIONOS && !UNITY_EDITOR
                SetNativeCallbackOfCameraAccess(CallbackFromNative);
#endif
    }

    void Start()
    {

#if UNITY_VISIONOS && !UNITY_EDITOR
        // Start the main camera capture
        StartVisionProMainCameraCapture();

        // Apply to material continuously
        StartCoroutine(ApplyVisionProCameraCaptureToMaterialContinuously());

        // Skip the following process
        return;
#endif

#if UNITY_EDITOR
        StartWebCam(WebCamDeviceName);
#elif UNITY_IOS
        StartCoroutine(RequestCameraPermission_iOS());
#elif UNITY_ANDROID
        StartCoroutine(RequestCameraPermission_Android());
#else
        StartWebCam(WebCamDeviceName);
#endif
    }

    void OnDisable()
    {
        if (IsVisionOs())
        {
            SetNativeCallbackOfCameraAccess(null);
        }
        else
        {
            if (webCamTexture != null) { webCamTexture.Stop(); }
        }
    }

    IEnumerator RequestCameraPermission_iOS()
    {
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            StartWebCam(WebCamDeviceName);
        }
        else
        {
            Debug.Log("Permission denied.");
        }
    }

    IEnumerator RequestCameraPermission_Android()
    {
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            Application.RequestUserAuthorization(UserAuthorization.WebCam);
            yield return new WaitForSeconds(1); // 権限要求の結果を待つ
        }

        if (Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            StartWebCam(WebCamDeviceName);
        }
        else
        {
            Debug.Log("Permission denied");
        }
    }

    void Update()
    {
        // Apply WebCamTexture to material
        ApplyWebcamTextureToMaterial(PreviewMaterial, webCamTexture);
    }

    void StartWebCam(string deviceName)
    {
        webCamTexture = new WebCamTexture(deviceName);
        webCamTexture.Play();
    }

    // Call function continuously
    IEnumerator ApplyVisionProCameraCaptureToMaterialContinuously()
    {
        while (true)
        {
            yield return new WaitForSeconds(skipSeconds);
            ApplyBase64StringToMaterial(PreviewMaterial, tempBase64String);
        }
    }

    void ApplyWebcamTextureToMaterial(Material material, WebCamTexture webCamTexture)
    {
        if (webCamTexture == null) { return; }
        if (material == null) { return; }
        if (webCamTexture.width <= 16) { return; }
        if (webCamTexture.isPlaying == false) { return; }
        if (tmpTexture == null) { tmpTexture = new Texture2D(webCamTexture.width, webCamTexture.height); }
        tmpTexture.SetPixels(webCamTexture.GetPixels());
        tmpTexture.Apply();
        material.mainTexture = tmpTexture;
    }

    void ApplyBase64StringToMaterial(Material material, string base64String)
    {
        if (base64String == null) { return; }
        Texture2D tempTexture = Base64ToTexture2D(base64String);
        material.mainTexture = tempTexture;
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

    bool IsVisionOs()
    {
#if UNITY_VISIONOS && !UNITY_EDITOR
        return true;
#endif
        return false;
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
    static extern void StartVisionProMainCameraCapture();
#else
    static void SetNativeCallbackOfCameraAccess(CallbackDelegate callback) { }
    static void StartVisionProMainCameraCapture() { }
#endif

}
