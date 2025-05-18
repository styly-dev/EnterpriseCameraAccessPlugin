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
};

import RealityKit
import MetalKit
import Accelerate

var currentTexture: MTLTexture?
let mtlDevice: MTLDevice = MTLCreateSystemDefaultDevice()!
var textureCache: CVMetalTextureCache! = nil
var commandQueue: MTLCommandQueue!
var pointer: UnsafeMutableRawPointer! = nil

var isRunning: Bool = false
let arKitSession = ARKitSession()

@_cdecl("startCapture")
public func startCampture() {
    print("############ START ############")
    isRunning = true
    Task {
        let formats = CameraVideoFormat.supportedVideoFormats(for: .main, cameraPositions: [.left])
        let status = await arKitSession.queryAuthorization(for: [.cameraAccess])
        print("Query Authorization Status :", status)
        let cameraFrameProvider = CameraFrameProvider()
        do {
            try await arKitSession.run([cameraFrameProvider])
        }
        catch {
            print("ARKit Session Faield:", error)
            return
        }
        print("Running ARKit Session.")
        for await cameraFrameUpdate in cameraFrameProvider.cameraFrameUpdates(for:  formats[0])! {
            if !isRunning { break }
            createTexture(cameraFrameUpdate.primarySample.pixelBuffer)
        }
    }
}

@_cdecl("stopCapture")
public func stopCapture() {
    print("############ STOP ##############")
    isRunning = false
    arKitSession.stop()
}

@_cdecl("getTexture")
public func getTexture() -> UnsafeMutableRawPointer? {
    return pointer
}

private func createTexture(_ pixelBuffer: CVPixelBuffer) {
    guard let pixelBufferBGRA: CVPixelBuffer = try? pixelBuffer.toBGRA() else { return }
    let width = CVPixelBufferGetWidth(pixelBufferBGRA)
    let height = CVPixelBufferGetHeight(pixelBufferBGRA)
    var cvTexture: CVMetalTexture?
    if textureCache == nil {
        CVMetalTextureCacheCreate(kCFAllocatorDefault, nil, mtlDevice, nil, &textureCache)
    }
    _ = CVMetalTextureCacheCreateTextureFromImage(kCFAllocatorDefault,
                                                  textureCache,
                                                  pixelBufferBGRA,
                                                  nil,
                                                  .bgra8Unorm_srgb,
                                                  width,
                                                  height,
                                                  0,
                                                  &cvTexture)
    guard let imageTexture = cvTexture else { return }
    let texture: MTLTexture = CVMetalTextureGetTexture(imageTexture)!
    if currentTexture == nil {
        let texdescriptor = MTLTextureDescriptor.texture2DDescriptor(pixelFormat: texture.pixelFormat,
                                                                     width: texture.width,
                                                                     height: texture.height,
                                                                     mipmapped: false)
        texdescriptor.usage = .unknown
        currentTexture = mtlDevice.makeTexture(descriptor: texdescriptor)
    }
    if commandQueue == nil {
        commandQueue = mtlDevice.makeCommandQueue()
    }
    let commandBuffer = commandQueue.makeCommandBuffer()!
    let blitEncoder = commandBuffer.makeBlitCommandEncoder()!
    blitEncoder.copy(from: texture,
                     sourceSlice: 0, sourceLevel: 0,
                     sourceOrigin: MTLOrigin(x: 0, y: 0, z: 0),
                     sourceSize: MTLSizeMake(texture.width, texture.height, texture.depth),
                     to: currentTexture!, destinationSlice: 0, destinationLevel: 0,
                     destinationOrigin: MTLOrigin(x: 0, y: 0, z: 0))
    blitEncoder.endEncoding()
    commandBuffer.commit()
    commandBuffer.waitUntilCompleted()
    if pointer == nil {
        pointer = Unmanaged.passUnretained(currentTexture!).toOpaque()
    }
}

extension CVPixelBuffer {
    public func toBGRA() throws -> CVPixelBuffer? {
        let pixelBuffer = self
        let pixelFormat = CVPixelBufferGetPixelFormatType(pixelBuffer)
        guard pixelFormat == kCVPixelFormatType_420YpCbCr8BiPlanarFullRange else { return pixelBuffer }
        let yImage: VImage = pixelBuffer.with({ VImage(pixelBuffer: $0, plane: 0) })!
        let cbcrImage: VImage = pixelBuffer.with({ VImage(pixelBuffer: $0, plane: 1) })!
        let outPixelBuffer = CVPixelBuffer.make(width: yImage.width, height: yImage.height, format: kCVPixelFormatType_32BGRA)!
        var argbImage = outPixelBuffer.with({ VImage(pixelBuffer: $0) })!
        try argbImage.draw(yBuffer: yImage.buffer, cbcrBuffer: cbcrImage.buffer)
        argbImage.permute(channelMap: [3, 2, 1, 0])
        return outPixelBuffer
    }
}

struct VImage {
    let width: Int
    let height: Int
    let bytesPerRow: Int
    var buffer: vImage_Buffer
    init?(pixelBuffer: CVPixelBuffer, plane: Int) {
        guard let rawBuffer = CVPixelBufferGetBaseAddressOfPlane(pixelBuffer, plane) else { return nil }
        self.width = CVPixelBufferGetWidthOfPlane(pixelBuffer, plane)
        self.height = CVPixelBufferGetHeightOfPlane(pixelBuffer, plane)
        self.bytesPerRow = CVPixelBufferGetBytesPerRowOfPlane(pixelBuffer, plane)
        self.buffer = vImage_Buffer(
            data: UnsafeMutableRawPointer(mutating: rawBuffer),
            height: vImagePixelCount(height),
            width: vImagePixelCount(width),
            rowBytes: bytesPerRow)
    }
    init?(pixelBuffer: CVPixelBuffer) {
        guard let rawBuffer = CVPixelBufferGetBaseAddress(pixelBuffer) else { return nil }
        self.width = CVPixelBufferGetWidth(pixelBuffer)
        self.height = CVPixelBufferGetHeight(pixelBuffer)
        self.bytesPerRow = CVPixelBufferGetBytesPerRow(pixelBuffer)
        self.buffer = vImage_Buffer(
            data: UnsafeMutableRawPointer(mutating: rawBuffer),
            height: vImagePixelCount(height),
            width: vImagePixelCount(width),
            rowBytes: bytesPerRow)
    }
    mutating func draw(yBuffer: vImage_Buffer, cbcrBuffer: vImage_Buffer) throws {
        try buffer.draw(yBuffer: yBuffer, cbcrBuffer: cbcrBuffer)
    }
    mutating func permute(channelMap: [UInt8]) {
        buffer.permute(channelMap: channelMap)
    }
}

extension CVPixelBuffer {
    func with<T>(_ closure: ((_ pixelBuffer: CVPixelBuffer) -> T)) -> T {
        CVPixelBufferLockBaseAddress(self, .readOnly)
        let result = closure(self)
        CVPixelBufferUnlockBaseAddress(self, .readOnly)
        return result
    }
    static func make(width: Int, height: Int, format: OSType) -> CVPixelBuffer? {
        var pixelBuffer: CVPixelBuffer? = nil
        CVPixelBufferCreate(kCFAllocatorDefault,
                            width,
                            height,
                            format,
                            [String(kCVPixelBufferIOSurfacePropertiesKey): [
                                "IOSurfaceOpenGLESFBOCompatibility": true,
                                "IOSurfaceOpenGLESTextureCompatibility": true,
                                "IOSurfaceCoreAnimationCompatibility": true,
                            ]] as CFDictionary,
                            &pixelBuffer)
        return pixelBuffer
    }
}

extension vImage_Buffer {
    mutating func draw(yBuffer: vImage_Buffer, cbcrBuffer: vImage_Buffer) throws {
        var yBuffer = yBuffer
        var cbcrBuffer = cbcrBuffer
        var conversionMatrix: vImage_YpCbCrToARGB = {
            var pixelRange = vImage_YpCbCrPixelRange(Yp_bias: 0, CbCr_bias: 128, YpRangeMax: 255, CbCrRangeMax: 255, YpMax: 255, YpMin: 1, CbCrMax: 255, CbCrMin: 0)
            var matrix = vImage_YpCbCrToARGB()
            vImageConvert_YpCbCrToARGB_GenerateConversion(kvImage_YpCbCrToARGBMatrix_ITU_R_709_2, &pixelRange, &matrix, kvImage420Yp8_CbCr8, kvImageARGB8888, UInt32(kvImageNoFlags))
            return matrix
        }()
        let error = vImageConvert_420Yp8_CbCr8ToARGB8888(&yBuffer, &cbcrBuffer, &self, &conversionMatrix, nil, 255, UInt32(kvImageNoFlags))
        if error != kvImageNoError {
            fatalError()
        }
    }
    mutating func permute(channelMap: [UInt8]) {
        vImagePermuteChannels_ARGB8888(&self, &self, channelMap, 0)
    }
}
