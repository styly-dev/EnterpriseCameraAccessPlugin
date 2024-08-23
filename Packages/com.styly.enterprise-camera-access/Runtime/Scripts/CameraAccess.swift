import ARKit
import AVFoundation
import Foundation
import SwiftUI
import UnityFramework

// Declared in C# as: static extern void GetMainCameraFrame(string name);
@_cdecl("StartVisionProMainCameraCapture")
func startVisionProMainCameraCapture() {
    print("############ GetMainCameraFrame")

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

    // Convert CVPixelBuffer to UIImage
    let ciImage = CIImage(cvPixelBuffer: pixelBuffer)
    let context = CIContext()
    guard let cgImage = context.createCGImage(ciImage, from: ciImage.extent) else { return }
    let uiImage = UIImage(cgImage: cgImage)

    // Convert UIImage to Data.
    guard let imageData = uiImage.jpegData(compressionQuality: 1.0) else {
        return
    }

    // Base64 encoding of Data.
    let base64String = imageData.base64EncodedString()

    CallCSharpCallbackOfCameraAccess(base64String)
}


typealias CallbackDelegateTypeOfCameraAccess = @convention(c) (UnsafePointer<CChar>) -> Void
var sCallbackDelegateOfCameraAccess: CallbackDelegateTypeOfCameraAccess? = nil

// Declared in C# as: static extern void SetNativeCallback(CallbackDelegate callback);
@_cdecl("SetNativeCallbackOfCameraAccess")
func setNativeCallbackOfCameraAccess(_ delegate: CallbackDelegateTypeOfCameraAccess)
{
    print("############ SET NATIVE CALLBACK")
    sCallbackDelegateOfCameraAccess = delegate
}

// This is a function for your own use from the enclosing Unity-VisionOS app, to call the delegate
// from your own windows/views (HelloWorldContentView uses this)
public func CallCSharpCallbackOfCameraAccess(_ str: String)
{
    if (sCallbackDelegateOfCameraAccess == nil) {
        return
    }

    str.withCString {
        sCallbackDelegateOfCameraAccess!($0)
    }
}