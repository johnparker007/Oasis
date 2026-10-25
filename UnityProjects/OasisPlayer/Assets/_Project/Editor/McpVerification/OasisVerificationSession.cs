using System;
using System.IO;
using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;

namespace OasisPlayer.Editor.Verification
{
    /// <summary>Opt-in batch bridge lifecycle, rearmed after Play Mode domain reloads.</summary>
    [InitializeOnLoad]
    public static class OasisVerificationSession
    {
        private const string Prefix = "Oasis.Verification.";
        static OasisVerificationSession()
        {
            if (!Application.isBatchMode || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OASIS_VERIFY_SESSION_DIR"))) return;
            EditorApplication.update += CheckStop;
            EditorApplication.quitting += RestorePreferences;
        }

        public static async void Begin()
        {
            var output = Environment.GetEnvironmentVariable("OASIS_VERIFY_SESSION_DIR");
            if (!Application.isBatchMode || string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("Explicit batch session directory required.");
            Directory.CreateDirectory(output);
            if (!SessionState.GetBool(Prefix + "Started", false))
            {
                SessionState.SetBool(Prefix + "HadHttp", EditorPrefs.HasKey("MCPForUnity.UseHttpTransport"));
                SessionState.SetBool(Prefix + "Http", EditorPrefs.GetBool("MCPForUnity.UseHttpTransport", true));
                SessionState.SetBool(Prefix + "HadUrl", EditorPrefs.HasKey("MCPForUnity.HttpUrl"));
                SessionState.SetString(Prefix + "Url", EditorPrefs.GetString("MCPForUnity.HttpUrl", ""));
                SessionState.SetBool(Prefix + "Started", true);
            }
            EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", true);
            EditorPrefs.SetString("MCPForUnity.HttpUrl", "http://127.0.0.1:8080");
            try
            {
                var connected = await MCPServiceLocator.Bridge.StartAsync();
                File.WriteAllText(Path.Combine(output, connected ? "connected.txt" : "failed.txt"), "Bridge connected=" + connected);
            }
            catch (Exception exception) { File.WriteAllText(Path.Combine(output, "failed.txt"), exception.ToString()); }
        }

        private static void CheckStop()
        {
            var output = Environment.GetEnvironmentVariable("OASIS_VERIFY_SESSION_DIR");
            if (File.Exists(Path.Combine(output, "stop.txt")) || EditorApplication.timeSinceStartup > 1200) EditorApplication.Exit(0);
        }

        private static void RestorePreferences()
        {
            if (!SessionState.GetBool(Prefix + "Started", false)) return;
            if (SessionState.GetBool(Prefix + "HadHttp", false)) EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", SessionState.GetBool(Prefix + "Http", true));
            else EditorPrefs.DeleteKey("MCPForUnity.UseHttpTransport");
            if (SessionState.GetBool(Prefix + "HadUrl", false)) EditorPrefs.SetString("MCPForUnity.HttpUrl", SessionState.GetString(Prefix + "Url", ""));
            else EditorPrefs.DeleteKey("MCPForUnity.HttpUrl");
        }
    }
}
