using System;
using System.Linq;
using OasisPlayer.Loading;
using OasisPlayer.RuntimeBuild;
using OasisPlayer.Settings;
using OasisPlayer.UI.Controllers;
using OasisPlayer.Startup;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StartupController : MonoBehaviour
{
    [SerializeField] private string[] editorDevelopmentArguments = { "--mode", "machine-preview", "--build", "" };
    private FatalErrorOverlay _errors;
    private static StartupController _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this; DontDestroyOnLoad(gameObject); _errors = gameObject.GetComponent<FatalErrorOverlay>() ?? gameObject.AddComponent<FatalErrorOverlay>();
        PlayerSettingsService.EnsureGlobal();
        if (gameObject.GetComponent<GraphicsSettingsApplier>() == null) gameObject.AddComponent<GraphicsSettingsApplier>();
        if (gameObject.GetComponent<PlayerUiRuntime>() == null) gameObject.AddComponent<PlayerUiRuntime>();
    }

    private async void Start()
    {
        try
        {
            var args = GetStartupArguments();
            if (!StartupOptionsParser.TryParse(args, out var options, out var error)) { _errors.Show(error); return; }
            Screen.SetResolution(options.Width, options.Height, options.Fullscreen);
            if (!RuntimeBuildLoader.TryLoad(options.BuildDirectory, out var build, out error)) { _errors.Show(error); return; }
            await SceneManager.LoadSceneAsync(options.SceneName, LoadSceneMode.Single);
            await new MachinePreviewLoader(new GltfFastCabinetModelLoader()).LoadAsync(build);
        }
        catch (Exception ex) { _errors.Show(ex.Message); }
    }

    private string[] GetStartupArguments()
    {
#if UNITY_EDITOR
        return editorDevelopmentArguments.Where(a => !string.IsNullOrWhiteSpace(a)).ToArray();
#else
        return Environment.GetCommandLineArgs().Skip(1).ToArray();
#endif
    }
}
