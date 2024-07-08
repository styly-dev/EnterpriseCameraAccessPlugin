# Unity Enterprise Camera Access Plugin for Vision Pro and Pico 4 Enterprise

## Introduction
![CameraRecording2](https://github.com/styly-dev/EnterpriseCameraAccessPlugin/assets/387880/1a2cd74a-6096-4200-85ff-30daaa707d03)

Apple and Pico recently released a camera access feature for enterprise usage. This Unity plugin makes it easier for developers to use the feature on Vision Pro and Pico 4 Enterprise devices.

The Unity package solves two problems:
- The Apple Enterprise API does not provide a Unity plugin; it is designed for Swift development.
- Compatibility across multiple devices.

## Features

- Easy to use plugin. You don't need to write even one line of code to get camera images. 
- Camera image can be obtained using the same script `Enterprise Camera Access Manager` for Vision Pro, Pico 4 Enterprise and Webcam in the Editor mode.
- Just set a material on the manager script. The material will be updated with the camera image.
- `GetMainCameraTexture2D()` returns the camera image as Texture2D.

<img width="696" alt="EnterpriseCameraAccessManager" src="https://github.com/styly-dev/EnterpriseCameraAccessPlugin/assets/387880/e4e237b3-89dd-414f-aa95-10824b2eaeda">

## Installation and setup

### Requirements

- Unity 2022.3.X
- Unity Pro license

Requirements for Vision Pro

- Vision Pro device
- visionOS 2.0 (Beta)
- Xcode 16.0 (Beta)
- [Enterprise APIs for visionOS entitlements](https://developer.apple.com/documentation/visionOS/building-spatial-experiences-for-business-apps-with-enterprise-apis#Request-the-entitlements)
- `Enterprise.license` file issued from Apple

Requirements for Pico 4
- Pico 4 Enterprise device
- Sign up for [Pico testing program](https://github.com/picoxr/GetCameraFrame)
- Authorized Package name

### Installation

#### Easiest way
Clone this repository and open the project.

#### Adding the package to your project

```
# Install openupm-cli
npm install -g openupm-cli

# Go to your unity project directory
cd YOUR_UNITY_PROJECT_DIR

# Install package: com.styly.webrequest-visualscripting-nodes
openupm add om.styly.com.styly.enterprise-camera-access
```

### Setup
#### Vision Pro

- Locate `Enterprise.license` file at the root of `/Assets` directory. 
- Set `Project Settings` - `Player` - `Camera Usage Description`
- Check `Project Settings` - `XR Plug-in Management` - `visionOS section` - `Initialize XR on Startup` and `Plug-in Providers` - `Apple visionOS`
- Set `Mixed Reality` mode in `Project Settings` - `XR Plug-in Management` - `Apple visionOS`

#### Pico 4 Enterprise

- Check `Project Settings` - `XR Plug-in Management` - `Android section` - `Initialize XR on Startup` and `Plug-in Providers` - `PICO`
- Set authorized package name at `Project Settings` - `Player` - `Identification` - `Package Name`
- Set `Android 10.0 (API level 29)` or higher at `Project Settings` - `Minimum API Level`
- PICO Unity Integration SDK will be automatically installed when you import Pico Samples.

### Usage

Import samples from Package Manager.

<img width="934" alt="ImportSamples" src="https://github.com/styly-dev/EnterpriseCameraAccessPlugin/assets/387880/d00efba3-b7e8-49d8-b63c-7766a1e34b95">

