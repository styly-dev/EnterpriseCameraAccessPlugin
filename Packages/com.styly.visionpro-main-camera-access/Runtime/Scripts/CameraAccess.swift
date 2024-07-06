import ARKit
import AVFoundation
import Foundation
import SwiftUI
import UnityFramework

// Declared in C# as: static extern void GetMainCameraFrame(string name);
@_cdecl("GetMainCameraFrame")
func getMainCameraFrame(_ cname: UnsafePointer<CChar>) {

    let name = String(cString: cname)
    print("############ GetMainCameraFrame \(name)")

    Task {
        await startCameraFeed()
    }
}

// Start the main camera feed
var lastCalledTime: Date?
func startCameraFeed() async {
    let formats = CameraVideoFormat.supportedVideoFormats(for: .main, cameraPositions: [.left])
    let arKitSession = ARKitSession()
    let authResult = await arKitSession.queryAuthorization(for: [.cameraAccess])
    print(authResult)
    let cameraTracking = CameraFrameProvider()
    do { try await arKitSession.run([cameraTracking]) } catch { return }

    // Then receive the new camera frame:
    for await i in cameraTracking.cameraFrameUpdates(
        for: .supportedVideoFormats(for: .main, cameraPositions: [.left]).first!)!
    {
        let imageBuffer: CVPixelBuffer = i.primarySample.pixelBuffer
        let currentTime = Date()

        // Skip if the last call was less than X second ago
        let skipSeconds = 0.1
        if lastCalledTime == nil || currentTime.timeIntervalSince(lastCalledTime!) >= skipSeconds {
            sendPixelBufferToUnity(imageBuffer)
            lastCalledTime = currentTime
        }
    }
}

// Send the pixel buffer to Unity
func sendPixelBufferToUnity(_ pixelBuffer: CVPixelBuffer) {
    CVPixelBufferLockBaseAddress(pixelBuffer, .readOnly)

    // CVPixelBufferをUIImageに変換
    let ciImage = CIImage(cvPixelBuffer: pixelBuffer)
    let context = CIContext()
    guard let cgImage = context.createCGImage(ciImage, from: ciImage.extent) else { return }
    let uiImage = UIImage(cgImage: cgImage)

    // UIImageをDataに変換
    guard let imageData = uiImage.jpegData(compressionQuality: 1.0) else {
        return
    }

    // DataをBase64エンコード
    let base64String = imageData.base64EncodedString()

    CallCSharpCallback(base64String)
}


typealias CallbackDelegateType = @convention(c) (UnsafePointer<CChar>) -> Void

var sCallbackDelegate: CallbackDelegateType? = nil

// Declared in C# as: static extern void SetNativeCallback(CallbackDelegate callback);
@_cdecl("SetNativeCallback")
func setNativeCallback(_ delegate: CallbackDelegateType)
{
    print("############ SET NATIVE CALLBACK")
    sCallbackDelegate = delegate
}

// This is a function for your own use from the enclosing Unity-VisionOS app, to call the delegate
// from your own windows/views (HelloWorldContentView uses this)
public func CallCSharpCallback(_ str: String)
{
    if (sCallbackDelegate == nil) {
        return
    }

    str.withCString {
        sCallbackDelegate!($0)
    }
}