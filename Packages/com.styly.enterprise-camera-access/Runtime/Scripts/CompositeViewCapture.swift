import ReplayKit
import AVFoundation
import MetalKit
import Accelerate
import Foundation

// Composite View Capture using ReplayKit
// This captures both the physical passthrough and digital content in real-time

var compositeCurrentTexture: MTLTexture?
let compositeMtlDevice: MTLDevice = MTLCreateSystemDefaultDevice()!
var compositeTextureCache: CVMetalTextureCache! = nil
var compositeCommandQueue: MTLCommandQueue!
var compositePointer: UnsafeMutableRawPointer! = nil
var isCompositeRunning: Bool = false

@_cdecl("startCompositeCapture")
public func startCompositeCapture() {
    print("############ START COMPOSITE CAPTURE ############")
    isCompositeRunning = true

    let screenRecorder = RPScreenRecorder.shared()

    // Disable microphone to avoid audio capture (as per requirements)
    screenRecorder.isMicrophoneEnabled = false
    screenRecorder.isCameraEnabled = false

    // Start capture with handler for each frame
    screenRecorder.startCapture(handler: { (sampleBuffer, bufferType, error) in
        if let error = error {
            print("Composite capture error: \(error.localizedDescription)")
            return
        }

        // Only process video frames
        guard bufferType == .video else { return }
        guard isCompositeRunning else { return }

        // Get the pixel buffer from the sample buffer
        guard let pixelBuffer = CMSampleBufferGetImageBuffer(sampleBuffer) else {
            print("Failed to get pixel buffer from sample buffer")
            return
        }

        createCompositeTexture(pixelBuffer)
    }, completionHandler: { error in
        if let error = error {
            print("Failed to start composite capture: \(error.localizedDescription)")
            isCompositeRunning = false
        } else {
            print("Composite capture started successfully")
        }
    })
}

@_cdecl("stopCompositeCapture")
public func stopCompositeCapture() {
    print("############ STOP COMPOSITE CAPTURE ##############")
    isCompositeRunning = false

    let screenRecorder = RPScreenRecorder.shared()
    screenRecorder.stopCapture { error in
        if let error = error {
            print("Failed to stop composite capture: \(error.localizedDescription)")
        } else {
            print("Composite capture stopped successfully")
        }
    }
}

@_cdecl("getCompositeTexture")
public func getCompositeTexture() -> UnsafeMutableRawPointer? {
    return compositePointer
}

@_cdecl("isCompositeAvailable")
public func isCompositeAvailable() -> Bool {
    return RPScreenRecorder.shared().isAvailable
}

private func createCompositeTexture(_ pixelBuffer: CVPixelBuffer) {
    guard let pixelBufferBGRA: CVPixelBuffer = try? pixelBuffer.toCompositeBGRA() else { return }
    let width = CVPixelBufferGetWidth(pixelBufferBGRA)
    let height = CVPixelBufferGetHeight(pixelBufferBGRA)
    var cvTexture: CVMetalTexture?

    if compositeTextureCache == nil {
        CVMetalTextureCacheCreate(kCFAllocatorDefault, nil, compositeMtlDevice, nil, &compositeTextureCache)
    }

    let textureStatus = CVMetalTextureCacheCreateTextureFromImage(kCFAllocatorDefault,
                                                  compositeTextureCache,
                                                  pixelBufferBGRA,
                                                  nil,
                                                  .bgra8Unorm_srgb,
                                                  width,
                                                  height,
                                                  0,
                                                  &cvTexture)
    guard textureStatus == kCVReturnSuccess,
          let imageTexture = cvTexture,
          let texture = CVMetalTextureGetTexture(imageTexture) else { return }

    if compositeCurrentTexture == nil {
        let texdescriptor = MTLTextureDescriptor.texture2DDescriptor(pixelFormat: texture.pixelFormat,
                                                                     width: texture.width,
                                                                     height: texture.height,
                                                                     mipmapped: false)
        texdescriptor.usage = [.shaderRead]
        compositeCurrentTexture = compositeMtlDevice.makeTexture(descriptor: texdescriptor)
    }

    if compositeCommandQueue == nil {
        compositeCommandQueue = compositeMtlDevice.makeCommandQueue()
    }

    guard let commandBuffer = compositeCommandQueue.makeCommandBuffer(),
          let blitEncoder = commandBuffer.makeBlitCommandEncoder(),
          let destTexture = compositeCurrentTexture else { return }
    blitEncoder.copy(from: texture,
                     sourceSlice: 0, sourceLevel: 0,
                     sourceOrigin: MTLOrigin(x: 0, y: 0, z: 0),
                     sourceSize: MTLSizeMake(texture.width, texture.height, texture.depth),
                     to: destTexture, destinationSlice: 0, destinationLevel: 0,
                     destinationOrigin: MTLOrigin(x: 0, y: 0, z: 0))
    blitEncoder.endEncoding()
    commandBuffer.commit()
    commandBuffer.waitUntilCompleted()

    if compositePointer == nil {
        compositePointer = Unmanaged.passUnretained(compositeCurrentTexture!).toOpaque()
    }
}

extension CVPixelBuffer {
    public func toCompositeBGRA() throws -> CVPixelBuffer? {
        let pixelBuffer = self
        let pixelFormat = CVPixelBufferGetPixelFormatType(pixelBuffer)

        // ReplayKit typically provides BGRA format already, but handle YUV if needed
        if pixelFormat == kCVPixelFormatType_32BGRA {
            return pixelBuffer
        }

        guard pixelFormat == kCVPixelFormatType_420YpCbCr8BiPlanarFullRange else {
            print("toCompositeBGRA: Unsupported pixel format: \(pixelFormat). Expected BGRA or 420YpCbCr8BiPlanarFullRange.")
            return nil
        }

        let yImage: CompositeVImage = pixelBuffer.withComposite({ CompositeVImage(pixelBuffer: $0, plane: 0) })!
        let cbcrImage: CompositeVImage = pixelBuffer.withComposite({ CompositeVImage(pixelBuffer: $0, plane: 1) })!
        let outPixelBuffer = CVPixelBuffer.makeComposite(width: yImage.width, height: yImage.height, format: kCVPixelFormatType_32BGRA)!
        var argbImage = outPixelBuffer.withComposite({ CompositeVImage(pixelBuffer: $0) })!
        try argbImage.drawComposite(yBuffer: yImage.buffer, cbcrBuffer: cbcrImage.buffer)
        argbImage.permuteComposite(channelMap: [3, 2, 1, 0])
        return outPixelBuffer
    }
}

struct CompositeVImage {
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

    mutating func drawComposite(yBuffer: vImage_Buffer, cbcrBuffer: vImage_Buffer) throws {
        try buffer.drawComposite(yBuffer: yBuffer, cbcrBuffer: cbcrBuffer)
    }

    mutating func permuteComposite(channelMap: [UInt8]) {
        buffer.permuteComposite(channelMap: channelMap)
    }
}

extension CVPixelBuffer {
    func withComposite<T>(_ closure: ((_ pixelBuffer: CVPixelBuffer) -> T)) -> T {
        CVPixelBufferLockBaseAddress(self, .readOnly)
        let result = closure(self)
        CVPixelBufferUnlockBaseAddress(self, .readOnly)
        return result
    }

    static func makeComposite(width: Int, height: Int, format: OSType) -> CVPixelBuffer? {
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
    mutating func drawComposite(yBuffer: vImage_Buffer, cbcrBuffer: vImage_Buffer) throws {
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
            print("vImage conversion error: \(error)")
            return
        }
    }

    mutating func permuteComposite(channelMap: [UInt8]) {
        vImagePermuteChannels_ARGB8888(&self, &self, channelMap, 0)
    }
}
