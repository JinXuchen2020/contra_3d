// BuildScript.cs — Unity 批处理构建脚本
// 设计来源: src/adapter/engine/unity_runtime_verify.md
// 职责: 提供 BatchMode 构建入口，支持 Windows/Mac/Linux 多平台构建

using UnityEditor;
using UnityEngine;
using UnityEditor.Build.Reporting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// 构建脚本 — 由 OS runtime_verify 协议调用
/// 位于 Assets/Editor/ 目录确保 Unity 自动编译为 Editor 程序集
/// </summary>
public static class BuildScript
{
    private static string GetProjectPath()
    {
        return Directory.GetCurrentDirectory();
    }

    private static string[] GetEnabledScenes()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();
        
        if (scenes.Length == 0)
        {
            return new[] { "Assets/Scenes/Boot.unity" };
        }
        
        return scenes;
    }

    /// <summary>
    /// Windows 构建（Development 模式，用于测试验证）
    /// </summary>
    [MenuItem("Build/Build Windows")]
    public static void BuildWindows()
    {
        CleanBeeRspReferences();
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = GetEnabledScenes(),
            locationPathName = Path.Combine(GetProjectPath(), "Builds", "Windows", "contra_3d.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development | BuildOptions.AllowDebugging
        };

        BuildPlayer(options);
    }

    /// <summary>
    /// Windows Release 构建（Optimized 模式，用于发布）
    /// </summary>
    [MenuItem("Build/Build Windows Release")]
    public static void BuildWindowsRelease()
    {
        CleanBeeRspReferences();
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = GetEnabledScenes(),
            locationPathName = Path.Combine(GetProjectPath(), "Builds", "Windows", "contra_3d.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildPlayer(options);
    }

    /// <summary>
    /// Mac 构建
    /// </summary>
    [MenuItem("Build/Build Mac")]
    public static void BuildMac()
    {
        CleanBeeRspReferences();
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = GetEnabledScenes(),
            locationPathName = Path.Combine(GetProjectPath(), "Builds", "Mac", "contra_3d.app"),
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None
        };

        BuildPlayer(options);
    }

    /// <summary>
    /// Linux 构建
    /// </summary>
    [MenuItem("Build/Build Linux")]
    public static void BuildLinux()
    {
        CleanBeeRspReferences();
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = GetEnabledScenes(),
            locationPathName = Path.Combine(GetProjectPath(), "Builds", "Linux", "contra_3d.x86_64"),
            target = BuildTarget.StandaloneLinux64,
            options = BuildOptions.None
        };

        BuildPlayer(options);
    }

    private static void BuildPlayer(BuildPlayerOptions options)
    {
        var outputDir = Path.GetDirectoryName(options.locationPathName);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var report = BuildPipeline.BuildPlayer(options);
        
        Debug.Log("[BuildScript] Build completed!");
        Debug.Log("[BuildScript] Output: " + options.locationPathName);
        Debug.Log("[BuildScript] Status: " + report.summary.result);
        
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("[BuildScript] Build failed with " + report.summary.totalErrors + " errors");
        }
        else
        {
            Debug.Log("[BuildScript] Build succeeded!");
            Debug.Log("[BuildScript] Total time: " + report.summary.totalTime + " seconds");
            Debug.Log("[BuildScript] Total size: " + report.summary.totalSize + " bytes");
        }
    }

    /// <summary>
    /// Cleans Unity Bee .rsp files before compilation:
    /// 1. Removes Contra3D.Core.dll references from non-asmdef assemblies
    ///    to prevent CS0104 ambiguous Vector3 reference.
    /// 2. Removes Tests/ DLL and source references from non-Editor, non-test assemblies
    ///    to prevent CS0579 duplicate attribute errors.
    /// 3. Injects AotHelper stub into com.unity.services.core Editor rsp
    ///    to fix CS0103 "AotHelper does not exist" (known package bug).
    /// </summary>
    private static void CleanBeeRspReferences()
    {
        string artifactsDir = Path.Combine(Directory.GetCurrentDirectory(), "Library", "Bee", "artifacts");
        if (!Directory.Exists(artifactsDir)) return;

        // Assembly names produced by Contra3D asmdef files — these keep the Core DLL ref
        HashSet<string> contra3DAsmdefNames = new HashSet<string>
        {
            "Contra3D.Core",
            "Contra3D.Runtime",
            "Contra3D.Core.Playtest"
        };

        // Assembly names that are Editor assemblies — keep Tests refs
        HashSet<string> editorAssemblyNames = new HashSet<string>
        {
            "Assembly-CSharp-Editor",
            "UnityEditor.TestRunner",
            "UnityEngine.TestRunner"
        };

        // Assembly names that are test assemblies — keep Tests refs
        HashSet<string> testAssemblyNames = new HashSet<string>
        {
            "Contra3D.Core.Tests",
            "Unity.Collections.Tests.CoreCLR.InternalJobNestedPrivate",
            "Unity.Collections.Tests.CoreCLR.PrivateJobNested",
            "Unity.Collections.Tests.CoreCLR.ProtectedJobNested",
            "Unity.Collections.Tests.CoreCLR.PublicJobPrivateGeneric",
            "Unity.InputSystem.TestFramework"
        };

        int cleaned = 0;
        foreach (string rspFile in Directory.GetFiles(artifactsDir, "*.rsp", SearchOption.AllDirectories))
        {
            string content = File.ReadAllText(rspFile);
            string before = content;

            // Determine the assembly name from the rsp filename
            string rspBase = Path.GetFileNameWithoutExtension(rspFile);
            if (rspBase.EndsWith(".dll.mvfrm"))
                rspBase = rspBase.Substring(0, rspBase.Length - ".dll.mvfrm".Length);

            bool isContra3DAsmdef = contra3DAsmdefNames.Contains(rspBase);
            bool isEditorAssembly = editorAssemblyNames.Contains(rspBase);
            bool isTestAssembly = testAssemblyNames.Contains(rspBase);

            // Remove -r: lines referencing Tests/ directories from non-Editor, non-test assemblies
            if (!isEditorAssembly && !isTestAssembly)
            {
                content = Regex.Replace(content,
                    @"^-r:""[^""]*[/\\]Tests[/\\].*$\r?\n?",
                    string.Empty,
                    RegexOptions.Multiline);
            }

            // Remove source file lines referencing Tests/ directories from non-Editor, non-test assemblies
            if (!isEditorAssembly && !isTestAssembly)
            {
                content = Regex.Replace(content,
                    @"""Assets/Scripts/(Core/)?Tests/[^""]*\.cs""\r?\n?",
                    string.Empty,
                    RegexOptions.Multiline);
            }

            // Remove AssemblyInfo lines from test projects from non-Editor, non-test assemblies
            if (!isEditorAssembly && !isTestAssembly)
            {
                content = Regex.Replace(content,
                    @"""Assets/Scripts/(Core/)?Tests/[^""]*AssemblyInfo\.cs""\r?\n?",
                    string.Empty,
                    RegexOptions.Multiline);
            }

            // Remove Contra3D.Core.dll reference from non-asmdef assemblies
            if (!isContra3DAsmdef)
            {
                content = Regex.Replace(content,
                    @"^-r:""[^""]*Contra3D\.Core\.dll""\r?\n?",
                    string.Empty,
                    RegexOptions.Multiline);
            }

            if (content != before)
            {
                File.WriteAllText(rspFile, content);
                cleaned++;
            }
        }

        // Inject AotHelper stub into com.unity.services.core Editor rsp to fix CS0103.
        // The package's JsonHelpers.cs references AotHelper.EnsureType<T>() but the class
        // is missing from this version of the package (known Unity bug).
        string stubPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Editor", "AotHelperStub.cs");
        if (File.Exists(stubPath))
        {
            foreach (string rspFile in Directory.GetFiles(artifactsDir, "*.rsp", SearchOption.AllDirectories))
            {
                if (!rspFile.EndsWith(".dll.mvfrm.rsp") && !rspFile.EndsWith(".rsp2")) continue;
                string baseName = Path.GetFileNameWithoutExtension(rspFile);
                if (baseName.EndsWith(".dll.mvfrm"))
                    baseName = baseName.Substring(0, baseName.Length - ".dll.mvfrm".Length);
                if (!baseName.Contains("Unity.Services.Core.Environments.Editor")) continue;

                string content = File.ReadAllText(rspFile);
                string stubLine = "\"" + stubPath.Replace("\\", "/") + "\"";
                if (!content.Contains("AotHelperStub.cs"))
                {
                    // Insert stub source before the compiler flags section
                    int flagIndex = content.IndexOf("-langversion:");
                    if (flagIndex >= 0)
                    {
                        content = content.Insert(flagIndex, stubLine + "\n");
                        File.WriteAllText(rspFile, content);
                        Debug.Log("[BuildScript] Injected AotHelperStub.cs into " + Path.GetFileName(rspFile));
                        cleaned++;
                    }
                }
            }
        }

        if (cleaned > 0)
            Debug.Log("[BuildScript] Cleaned " + cleaned + " Bee rsp file(s).");
    }
}
