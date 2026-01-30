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

    [Tooltip("Enable composite view capture (passthrough + digital content) instead of physical camera only")]
    public bool UseCompositeCapture = false;

    private WebCamTexture webCamTexture;
    private Texture2D tmpTexture = null;
    private string tempBase64String = null;
    private float skipSeconds = 0.1f;
#if UNITY_VISIONOS && !UNITY_EDITOR
    private bool _hasSetTexture = false;
    private Texture2D _texture;
    private RenderTexture _renderTexture;
    private IntPtr _texturePtr;
    private int _width = 1920;
    private int _height = 1080;

    // Composite capture
    private bool _hasSetCompositeTexture = false;
    private Texture2D _compositeTexture;
    private RenderTexture _compositeRenderTexture;
    private IntPtr _compositeTexturePtr;
    private int _compositeWidth = 1920;
    private int _compositeHeight = 1080;
#endif

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
        // No setup required for native texture capture
#endif
    }

    void Start()
    {

#if USE_PICOXR && UNITY_ANDROID && !UNITY_EDITOR
        PicoStart();
        return;
#endif

#if UNITY_VISIONOS && !UNITY_EDITOR
        if (UseCompositeCapture)
        {
            // Initialize composite capture
            _compositeRenderTexture = new RenderTexture(_compositeWidth, _compositeHeight, 1, RenderTextureFormat.ARGB32);
            _compositeRenderTexture.enableRandomWrite = true;
            _compositeRenderTexture.Create();
            PreviewMaterial.mainTexture = _compositeRenderTexture;
            startCompositeCapture();
        }
        else
        {
            // Initialize physical camera capture
            _renderTexture = new RenderTexture(_width, _height, 1, RenderTextureFormat.ARGB32);
            _renderTexture.enableRandomWrite = true;
            _renderTexture.Create();
            PreviewMaterial.mainTexture = _renderTexture;
            startCapture();
        }
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
        if (UseCompositeCapture)
        {
            stopCompositeCapture();
        }
        else
        {
            stopCapture();
        }
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
#if UNITY_VISIONOS && !UNITY_EDITOR
        if (UseCompositeCapture)
        {
            if (_hasSetCompositeTexture)
            {
                UpdateCompositeTexture();
            }
            else
            {
                TryGetCompositeTexture();
            }
        }
        else
        {
            if (_hasSetTexture)
            {
                UpdateTexture();
            }
            else
            {
                TryGetTexture();
            }
        }
#else
        // Apply WebCamTexture to material
        ApplyWebcamTextureToMaterial(PreviewMaterial, webCamTexture);

#if USE_PICOXR && UNITY_ANDROID && !UNITY_EDITOR
        ApplyPicoFrameToMaterial(PreviewMaterial);
#endif
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
    private void TryGetTexture()
    {
        IntPtr texturePtr = getTexture();
        if (texturePtr == IntPtr.Zero) return;

        _texturePtr = texturePtr;

        if (_texture != null)
        {
            UnityEngine.Object.Destroy(_texture);
        }

        _texture = Texture2D.CreateExternalTexture(_width, _height, TextureFormat.BGRA32, false, false, _texturePtr);
        _texture.UpdateExternalTexture(_texturePtr);
        
        // スケールとオフセットを使用して上下反転を行う
        // scale.y を -1 にすることで上下反転、offset.y を 1 にすることで位置を調整
        Vector2 scale = new Vector2(1, -1);
        Vector2 offset = new Vector2(0, 1);
        
        Graphics.Blit(_texture, _renderTexture, scale, offset);
        PreviewMaterial.mainTexture = _renderTexture;

        _hasSetTexture = true;
    }

    private void UpdateTexture()
    {
        // スケールとオフセットを使用して上下反転を行う
        Vector2 scale = new Vector2(1, -1);
        Vector2 offset = new Vector2(0, 1);

        Graphics.Blit(_texture, _renderTexture, scale, offset);
        Unity.PolySpatial.PolySpatialObjectUtils.MarkDirty(_renderTexture);
    }

    private void TryGetCompositeTexture()
    {
        IntPtr texturePtr = getCompositeTexture();
        if (texturePtr == IntPtr.Zero) return;

        _compositeTexturePtr = texturePtr;

        if (_compositeTexture != null)
        {
            UnityEngine.Object.Destroy(_compositeTexture);
        }

        _compositeTexture = Texture2D.CreateExternalTexture(_compositeWidth, _compositeHeight, TextureFormat.BGRA32, false, false, _compositeTexturePtr);
        _compositeTexture.UpdateExternalTexture(_compositeTexturePtr);

        // スケールとオフセットを使用して上下反転を行う
        Vector2 scale = new Vector2(1, -1);
        Vector2 offset = new Vector2(0, 1);

        Graphics.Blit(_compositeTexture, _compositeRenderTexture, scale, offset);
        PreviewMaterial.mainTexture = _compositeRenderTexture;

        _hasSetCompositeTexture = true;
    }

    private void UpdateCompositeTexture()
    {
        // スケールとオフセットを使用して上下反転を行う
        Vector2 scale = new Vector2(1, -1);
        Vector2 offset = new Vector2(0, 1);

        Graphics.Blit(_compositeTexture, _compositeRenderTexture, scale, offset);
        Unity.PolySpatial.PolySpatialObjectUtils.MarkDirty(_compositeRenderTexture);
    }

    /// <summary>
    /// Get composite view texture (passthrough + digital content)
    /// </summary>
    public Texture2D GetCompositeTexture2D()
    {
#if UNITY_VISIONOS && !UNITY_EDITOR
        return _compositeTexture;
#else
        return null;
#endif
    }
#endif

#if UNITY_VISIONOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern void SetNativeCallbackOfCameraAccess(CallbackDelegate callback);
    [DllImport("__Internal")]
    static extern void StartVisionProMainCameraCapture();
    [DllImport("__Internal")]
    static extern void startCapture();
    [DllImport("__Internal")]
    static extern void stopCapture();
    [DllImport("__Internal")]
    static extern IntPtr getTexture();
    [DllImport("__Internal")]
    static extern void startCompositeCapture();
    [DllImport("__Internal")]
    static extern void stopCompositeCapture();
    [DllImport("__Internal")]
    static extern IntPtr getCompositeTexture();
    [DllImport("__Internal")]
    static extern bool isCompositeAvailable();
#else
    static void SetNativeCallbackOfCameraAccess(CallbackDelegate callback) { }
    static void StartVisionProMainCameraCapture() { }
    static void startCapture() { }
    static void stopCapture() { }
    static IntPtr getTexture() { return IntPtr.Zero; }
    static void startCompositeCapture() { }
    static void stopCompositeCapture() { }
    static IntPtr getCompositeTexture() { return IntPtr.Zero; }
    static bool isCompositeAvailable() { return false; }
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
