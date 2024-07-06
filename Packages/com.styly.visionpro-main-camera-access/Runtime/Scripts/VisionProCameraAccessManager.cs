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
        // カメラ取得を開始
        StartMainCameraCapture();

        // Coroutineを開始する
        StartCoroutine(CallFunctionContinuously());
    }

    // skipSeconds 秒おきに連続実行する関数
    IEnumerator CallFunctionContinuously()
    {
        while (true)
        {
            yield return new WaitForSeconds(skipSeconds);
            ApplyBase64StringToMaterial();
        }
    }

    // Base64StringをTextureに変換してMaterialに適用する
    void ApplyBase64StringToMaterial()
    {
        if (tempBase64String == null) { return; }

        // 古いテクスチャを解放
        if (PreviewMaterial.mainTexture != null)
        {
            Texture2D oldTexture = (Texture2D)PreviewMaterial.mainTexture;
            Object.Destroy(oldTexture);
        }

        // 新しいテクスチャを生成
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

    public Texture2D GetCameraFrameTexture()
    {
        return PreviewMaterial.mainTexture as Texture2D;
    }

    // Base64StringをTexture2Dに変換する
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
    // This attribute is required for methods that are going to be called from native code via a function pointer.
    [MonoPInvokeCallback(typeof(CallbackDelegate))]
    static void CallbackFromNative(string command)
    {
        // Debug.Log("Callback from native: " + command.Length);
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
