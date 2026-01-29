# Frequently Asked Questions (FAQ)

## visionOS Camera Access Requirements

### Q: On visionOS, if I do not have an Enterprise developer account, can I still use basic QR code/barcode scanning on a local test device only?

**Short Answer:** No, not with this plugin. This plugin specifically uses the visionOS Enterprise APIs which **require an Enterprise developer account** and Apple's approval, even for local testing.

**Detailed Answer:**

#### What This Plugin Requires

This plugin (`EnterpriseCameraAccessPlugin`) uses the visionOS **main camera access Enterprise API** (`com.apple.developer.arkit.main-camera-access.allow` entitlement). This API has strict requirements:

1. **Enterprise Developer Account** - You must either:
   - Be enrolled in the Apple Developer Enterprise Program, OR
   - Be an Account Holder in the standard Apple Developer Program registered as an **organization** (not available for individuals)

2. **Apple Approval** - You must:
   - Request the entitlement from Apple through their official portal
   - Receive approval and an `Enterprise.license` file from Apple
   - Have a valid business use case that meets Apple's enterprise criteria

3. **Distribution Restrictions** - Apps using this API:
   - Cannot be distributed on the public App Store
   - Must be distributed privately (in-house or through Apple Business Manager)
   - Require enterprise approval even for local testing via Xcode

#### Why You Need Enterprise Access for This Plugin

The visionOS main camera Enterprise API provides **direct access to the main camera feed** with high-quality video frames. This is what enables:
- Real-time camera image capture
- High-resolution video frames
- Integration with Unity and other development frameworks

This level of camera access is restricted by Apple to prevent privacy concerns and is only available to approved enterprise applications.

#### Alternatives for Standard Developer Accounts

If you **do not have an Enterprise developer account** and only need basic QR code/barcode scanning without spatial tracking, you have the following options:

##### Option 1: Use AVFoundation (2D Camera Scanning)
For **standard developer accounts**, you can use traditional iOS/visionOS camera APIs:

- **AVFoundation** with `AVCaptureMetadataOutput` for barcode detection
- **Vision Framework** with `VNDetectBarcodesRequest` for advanced scanning
- Works in a 2D camera view (not spatially aware in 3D space)
- **Does NOT require Enterprise entitlements**
- Only requires `NSCameraUsageDescription` in Info.plist

This approach gives you:
- ✅ QR code/barcode content (string value)
- ✅ Works with standard Apple Developer account
- ✅ Can be tested locally via Xcode
- ✅ Can be distributed on App Store
- ❌ No spatial tracking or 3D positioning
- ❌ No access to main camera (uses standard camera APIs)
- ❌ Not compatible with this Unity plugin

##### Option 2: Enterprise Spatial Barcode Scanning
If you need **spatial barcode scanning** (barcodes tracked in 3D space):

- Requires the **spatial barcode scanning entitlement** (separate from main camera access)
- Also requires Enterprise developer account and Apple approval
- Provides 3D position and orientation of barcodes in space
- Requires Enterprise APIs

#### Summary Table

| Feature | This Plugin (Enterprise API) | AVFoundation (Standard) |
| ------- | --------------------------- | ----------------------- |
| **Account Required** | Enterprise or Organization | Any Apple Developer Account |
| **Apple Approval** | Yes (entitlement request) | No |
| **License File** | Yes (Enterprise.license) | No |
| **Main Camera Access** | ✅ Yes | ❌ No |
| **QR/Barcode Decoding** | ✅ Yes (via camera frames) | ✅ Yes (native API) |
| **Spatial Tracking** | ❌ No (separate entitlement) | ❌ No |
| **Local Testing** | ✅ Yes (with approval) | ✅ Yes |
| **App Store Distribution** | ❌ No | ✅ Yes |
| **Unity Integration** | ✅ Yes (this plugin) | ⚠️ Custom implementation needed |

#### Recommendations

**If you only need to decode barcode content (string value):**
- Consider using AVFoundation or Vision Framework with a standard developer account
- This does not require Enterprise access
- Implement a native visionOS barcode scanner using standard APIs
- This will NOT work with this Unity plugin (you'd need to write native Swift code or find/create a different Unity plugin)

**If you need main camera access for other purposes:**
- Apply for the Enterprise entitlement through Apple's developer portal
- Prepare a business use case explaining why you need main camera access
- Note that approval can take several days to a week

**If you specifically need this Unity plugin:**
- You must obtain Enterprise entitlements and the Enterprise.license file
- This requirement applies even for local testing
- There is no way to bypass this requirement

## Additional Resources

- [Apple: Accessing the main camera](https://developer.apple.com/documentation/visionOS/accessing-the-main-camera)
- [Apple: Building spatial experiences for business apps with Enterprise APIs](https://developer.apple.com/documentation/visionOS/building-spatial-experiences-for-business-apps-with-enterprise-apis)
- [Apple: Request main camera access entitlement](https://developer.apple.com/contact/request/visionos-main-camera-access)
- [AVFoundation barcode scanning tutorial](https://developer.apple.com/documentation/avfoundation/avcapturemetadataoutput)
- [Vision Framework barcode detection](https://developer.apple.com/documentation/vision/vndetectbarcodesrequest)

---

*Last updated: January 2026*
