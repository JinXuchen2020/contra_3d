// BuildScript.cs — Unity 批处理构建脚本
// 设计来源: src/adapter/engine/unity_runtime_verify.md
// 职责: 提供 BatchMode 构建入口，支持 Windows/Mac/Linux 多平台构建

using UnityEditor;
using UnityEngine;
using System.IO;

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
            // 默认返回 Boot.unity
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
        // 确保输出目录存在
        var outputDir = Path.GetDirectoryName(options.locationPathName);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var result = BuildPipeline.BuildPlayer(options);
        
        if (result.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException($"Build failed: {result.summary.details}");
        }

        Debug.Log($"[BuildScript] Build succeeded: {options.locationPathName}");
        Debug.Log($"[BuildScript] Total time: {result.totalTime}");
        Debug.Log($"[BuildScript] Total size: {result.totalSize} bytes");
    }
}
