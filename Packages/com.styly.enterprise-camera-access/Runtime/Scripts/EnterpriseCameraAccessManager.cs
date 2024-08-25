using System.Collections;
using AOT;
using UnityEngine;
using System.Runtime.InteropServices;
using UnityEngine.UI;
using System;


#if USE_PICOXR && UNITY_ANDROID && !UNITY_EDITOR
using Unity.XR.PICO.TOBSupport;
using Unity.XR.PXR;
#endif

public class EnterpriseCameraAccessManager : MonoBehaviour
{
    public static EnterpriseCameraAccessManager Instance { get; private set; }
    public Material PreviewMaterial;

    [Tooltip("WebCam will be used for Editor mode or Smartphone. Default camera is used if this field is empty.")]
    public string WebCamDeviceName = "";

    private WebCamTexture webCamTexture;
    private Texture2D tmpTexture = null;
    private string tempBase64String = null;
    private float skipSeconds = 0.1f;

#if USE_PICOXR && UNITY_ANDROID && !UNITY_EDITOR
    private int PicoImageWidth = 1164;
    private int PicoImageHeight = 874;
#endif

    /// <summary>
    /// Get Vision Pro main camera image as texture2D.
    /// </summary>
    /// <returns></returns>
    public Texture2D GetMainCameraTexture2D()
    {
#if UNITY_VISIONOS && !UNITY_EDITOR
        Base64ToTexture2D(tmpTexture, tempBase64String);
#endif
        return tmpTexture;
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

#if USE_PICOXR && UNITY_ANDROID && !UNITY_EDITOR
        PicoStart();
        return;
#endif

#if UNITY_VISIONOS && !UNITY_EDITOR
        // Start the main camera capture
        StartVisionProMainCameraCapture();

        // Apply to material continuously
        StartCoroutine(ApplyVisionProCameraCaptureToMaterialContinuously());

        // Create a tempTexture and assign it to the material
        tmpTexture = new Texture2D(256, 256);
        Debug.Log("tmpTextureWidth: " + tmpTexture.width + " tmpTextureHeight: " + tmpTexture.height);
        PreviewMaterial.mainTexture = tmpTexture;

        // Skip the following process, WebCamTexture is not used
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
#if USE_PICOXR && UNITY_ANDROID && !UNITY_EDITOR
        OnPicoDisable();
        return;
#endif

#if UNITY_VISIONOS && !UNITY_EDITOR
        SetNativeCallbackOfCameraAccess(null);
        return;
#endif
        if (webCamTexture != null) { webCamTexture.Stop(); }
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
            yield return new WaitForSeconds(1); // Wait for the result of the authorization request
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

#if USE_PICOXR && UNITY_ANDROID && !UNITY_EDITOR
        ApplyPicoFrameToMaterial(PreviewMaterial);
#endif
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

        // Overwrite the tmpTexture (material.mainTexture) with the base64String
        Base64ToTexture2D(tmpTexture, base64String);
    }

    // Convert Base64String to Texture2D
    void Base64ToTexture2D(Texture2D tex, string base64)
    {
        try
        {
            byte[] imageBytes = System.Convert.FromBase64String(base64);

            // tmpTexture に画像を読み込む
            bool loadSuccess = tex.LoadImage(imageBytes);

            if (!loadSuccess)
            {
                Debug.LogError("Failed to load image from byte array.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to convert base64 string to texture2D: {ex.Message}");
        }
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


#if USE_PICOXR && UNITY_ANDROID && !UNITY_EDITOR
    // Code for PicoXR

    private void PicoStart()
    {
        PXR_Boundary.EnableSeeThroughManual(true);
        PXR_Enterprise.InitEnterpriseService();
        PXR_Enterprise.BindEnterpriseService();

        tmpTexture = new Texture2D(PicoImageWidth, PicoImageHeight, TextureFormat.RGB24, false, false);
        OpenVSTCamera();
    }

    void OnApplicationPause(bool pause)
    {
        if (!pause)
        {
            PXR_Boundary.EnableSeeThroughManual(true);
        }
    }

    private void OnApplicationQuit()
    {
        CloseVSTCamera();
    }

    private void OnPicoDisable()
    {
        tmpTexture = null;
        PXR_Enterprise.CloseVSTCamera();
    }

    public void OpenVSTCamera()
    {
        bool result = PXR_Enterprise.OpenVSTCamera();
        Debug.Log("Open VST Camera" + result);
    }

    public void CloseVSTCamera()
    {
        bool result = PXR_Enterprise.CloseVSTCamera();
        Debug.Log("Close VST Camera" + result);
    }

    void ApplyPicoFrameToMaterial(Material material)
    {
        try
        {
            // Acquire the camera frame from PicoXR
            PXR_Enterprise.AcquireVSTCameraFrameAntiDistortion(PicoImageWidth, PicoImageHeight, out Frame frame);
            tmpTexture.LoadRawTextureData(frame.data, (int)frame.datasize);
            tmpTexture.Apply();

            // Flip the image vertically
            Color[] pixels = tmpTexture.GetPixels();
            Color[] flippedPixels = new Color[pixels.Length];
            int width = tmpTexture.width;
            int height = tmpTexture.height;
            for (int y = 0; y < height; y++) { for (int x = 0; x < width; x++) { flippedPixels[x + y * width] = pixels[x + (height - y - 1) * width]; } }
            tmpTexture.SetPixels(flippedPixels);
            tmpTexture.Apply();

            // Apply to material
            material.mainTexture = tmpTexture;
        }
        catch (Exception e)
        {
            Debug.LogFormat("e={0}", e);
            throw;
        }
    }
#endif
}
