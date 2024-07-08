using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;
using UnityEngine;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public class ConfigureXcodeSettings : IPreprocessBuildWithReport
{
    static readonly string LicensePath = "Enterprise.license";                                   // in the Assets directory
    static readonly string EntitlementsXmlPath = "Editor/FilesToAdd/Entitlements.entitlements";  // in the package directory

    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report)
    {
        // Check if the license file exists
        string LicenseAbsolutePath = Path.Combine(Application.dataPath, LicensePath);
        if (!File.Exists(LicenseAbsolutePath))
        {
            Debug.LogError("License file not found. Please put the license file at the root of the Assets directory. The file path should be '/Assets/Enterprise.license'.");
            // Abort the build
            throw new BuildFailedException("License file not found.");
        }
    }

    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget buildTarget, string pathToBuiltProject)
    {
        if (buildTarget == BuildTarget.VisionOS)
        {
            string LicenseAbsolutePath = Path.Combine(Application.dataPath, LicensePath);
            AddFileToProject(LicenseAbsolutePath, pathToBuiltProject);
            string EntitlementsXmlAbsolutePath = Path.Combine(GetCurrentPackageAbsolutePath(), EntitlementsXmlPath);
            AddEntitlementsFile(EntitlementsXmlAbsolutePath, pathToBuiltProject);
            AddKeyValueToPlist("NSEnterpriseMCAMUsageDescription", "This app capture images from main camera", pathToBuiltProject);
            SetMinimumDeploymentVersion("XROS_DEPLOYMENT_TARGET", "2.0", pathToBuiltProject);
        }
    }

    /// <summary>
    /// Add the entitlements file to the Xcode project and set the CODE_SIGN_ENTITLEMENTS build setting.
    /// </summary>
    static void AddEntitlementsFile(string EntitlementsXmlAbsolutePath, string pathToBuiltProject)
    {
        // Get the Xcode project path (xcodeproj file)
        string xcodeprojPath = GetXcodeprojPath(pathToBuiltProject);

        // Read the Xcode project
        PBXProject project = new();
        project.ReadFromFile(xcodeprojPath);

        // Get the target GUID
        string targetGuid = project.GetUnityMainTargetGuid();
        string targetGuidFramework = project.GetUnityFrameworkTargetGuid();

        // Add the entitlements file to the project
        string entitlementsFileName = Path.GetFileName(EntitlementsXmlAbsolutePath);
        project.AddFileToBuild(targetGuid, project.AddFile(EntitlementsXmlAbsolutePath, "" + entitlementsFileName));

        // Set the entitlements file for the target
        project.SetBuildProperty(targetGuid, "CODE_SIGN_ENTITLEMENTS", "" + EntitlementsXmlAbsolutePath);

        // Save the modified project
        File.WriteAllText(xcodeprojPath, project.WriteToString());
    }

    /// <summary>
    /// Add a file to the Xcode project.
    /// </summary>
    static void AddFileToProject(string FileAbsolutePathToAdd, string pathToBuiltProject)
    {
        // Get the Xcode project path (xcodeproj file)
        string xcodeprojPath = GetXcodeprojPath(pathToBuiltProject);

        // Read the Xcode project
        PBXProject project = new();
        project.ReadFromFile(xcodeprojPath);

        // Get the target GUID
        string targetGuid = project.GetUnityMainTargetGuid();
        string targetGuidFramework = project.GetUnityFrameworkTargetGuid();

        // Add the file to the project
        string FileName = Path.GetFileName(FileAbsolutePathToAdd);
        project.AddFileToBuild(targetGuid, project.AddFile(FileAbsolutePathToAdd, "/" + FileName));

        // Save the modified project
        File.WriteAllText(xcodeprojPath, project.WriteToString());
    }

    /// <summary>
    /// Get the Xcode project path (xcodeproj file)
    /// </summary>
    static string GetXcodeprojPath(string pathToBuiltProject)
    {
        // Define the path to the Xcode project
        string xcodeprojPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        // Unityのバグ。"Unity-iPhone.xcodeproj"がpathで渡ってくるが、正しくは"Unity-VisionOS.xcodeproj"
        xcodeprojPath = xcodeprojPath.Replace("Unity-iPhone.xcodeproj", "Unity-VisionOS.xcodeproj");
        return xcodeprojPath;
    }

    static void SetMinimumDeploymentVersion(string DepleymentTargetString, string versionString, string pathToBuiltProject)
    {
        // Get the Xcode project path (xcodeproj file)
        string xcodeprojPath = GetXcodeprojPath(pathToBuiltProject);

        // Read the Xcode project
        PBXProject project = new();
        project.ReadFromFile(xcodeprojPath);

        // Get the target GUID
        string targetGuid = project.GetUnityMainTargetGuid();
        string targetGuidFramework = project.GetUnityFrameworkTargetGuid();
        string targetTestsGuid = project.TargetGuidByName("Unity-VisionOS Tests");

        // Set the minimum deployment target version
        project.SetBuildProperty(targetGuid, DepleymentTargetString, versionString);
        project.SetBuildProperty(targetGuidFramework, DepleymentTargetString, versionString);
        project.SetBuildProperty(targetTestsGuid, DepleymentTargetString, versionString);

        File.WriteAllText(xcodeprojPath, project.WriteToString());
    }

    /// <summary>
    /// Add key/value pair to info.plist 
    /// </summary>
    static void AddKeyValueToPlist(string key, string value, string pathToBuiltProject)
    {
        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        PlistElementDict rootDict = plist.root;

        if (!rootDict.values.ContainsKey(key))
        {
            rootDict.SetString(key, value);
        }
        plist.WriteToFile(plistPath);
    }

    /// <summary>
    /// Get the path of the package
    /// </summary>
    static string GetCurrentPackageAbsolutePath()
    {
        var MyPackageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(System.Reflection.MethodInfo.GetCurrentMethod().DeclaringType.Assembly);
        string path = MyPackageInfo.resolvedPath;

        return path;
    }
}
