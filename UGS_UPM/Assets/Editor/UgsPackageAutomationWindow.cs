using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class UgsPackageAutomationWindow : EditorWindow
{
    private string sourcePath = "Assets/UGS";
    private string packageRoot = "LocalPackages/com.robocare.ugs";
    private string version = "";
    private string remote = "origin";
    private string branch = "";
    private bool createRelease;

    [MenuItem("Tools/UGS/Package Automation")]
    public static void Open()
    {
        GetWindow<UgsPackageAutomationWindow>("UGS Package");
    }

    private void OnGUI()
    {
        GUILayout.Label("Package Paths", EditorStyles.boldLabel);
        sourcePath = EditorGUILayout.TextField("Source", sourcePath);
        packageRoot = EditorGUILayout.TextField("Package Root", packageRoot);
        version = EditorGUILayout.TextField("Version", version);

        GUILayout.Space(8f);
        GUILayout.Label("GitHub Publish", EditorStyles.boldLabel);
        remote = EditorGUILayout.TextField("Remote", remote);
        branch = EditorGUILayout.TextField("Branch (optional)", branch);
        createRelease = EditorGUILayout.Toggle("Create GH Release", createRelease);

        GUILayout.Space(10f);
        if (GUILayout.Button("Sync Runtime"))
        {
            RunRepackage(pack: false);
        }

        if (GUILayout.Button("Pack TGZ"))
        {
            RunRepackage(pack: true);
        }

        if (GUILayout.Button("Publish GitHub"))
        {
            RunPublish();
        }
    }

    private void RunRepackage(bool pack)
    {
        string scriptPath = GetScriptPath("tools/repackage-ugs.ps1");
        string args = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" -Source \"{sourcePath}\" -PackageRoot \"{packageRoot}\"";
        if (!string.IsNullOrWhiteSpace(version))
        {
            args += $" -Version \"{version}\"";
        }
        if (pack)
        {
            args += " -Pack";
        }

        RunPowerShell(args);
    }

    private void RunPublish()
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidOperationException("Version is required for Publish GitHub. Example: 1.0.2");
        }

        string scriptPath = GetScriptPath("tools/publish-ugs-upm.ps1");
        string args = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" -Source \"{sourcePath}\" -PackageRoot \"{packageRoot}\" -Remote \"{remote}\" -Version \"{version}\"";
        if (!string.IsNullOrWhiteSpace(branch))
        {
            args += $" -Branch \"{branch}\"";
        }
        if (createRelease)
        {
            args += " -CreateRelease";
        }

        RunPowerShell(args);
    }

    private static string GetScriptPath(string relativePath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? throw new InvalidOperationException("Project root not found.");
        string fullPath = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Script not found", fullPath);
        }

        return fullPath;
    }

private static void RunPowerShell(string arguments)
    {
#if UNITY_EDITOR_WIN
        const int timeoutMs = 10 * 60 * 1000;
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? throw new InvalidOperationException("Project root not found.");
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = arguments,
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start powershell process.");
        }

        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                stdoutBuilder.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                stderrBuilder.AppendLine(e.Data);
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit(timeoutMs))
        {
            try
            {
                process.Kill();
            }
            catch
            {
                // Ignore kill failures.
            }

            throw new TimeoutException($"PowerShell timed out after {timeoutMs / 1000} seconds.");
        }

        process.WaitForExit();
        string stdout = stdoutBuilder.ToString();
        string stderr = stderrBuilder.ToString();

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            UnityEngine.Debug.Log(stdout);
        }
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            UnityEngine.Debug.LogError(stderr);
        }

        if (process.ExitCode != 0)
        {
            string errPreview = string.IsNullOrWhiteSpace(stderr) ? "(no stderr)" : stderr.Trim();
            if (errPreview.Length > 2000)
            {
                errPreview = errPreview.Substring(errPreview.Length - 2000);
            }

            throw new Exception($"PowerShell failed with exit code {process.ExitCode}\n{errPreview}");
        }

        AssetDatabase.Refresh();
        UnityEngine.Debug.Log("UGS package automation completed.");
#else
        throw new PlatformNotSupportedException("This tool currently supports Unity Editor on Windows only.");
#endif
    }
}
