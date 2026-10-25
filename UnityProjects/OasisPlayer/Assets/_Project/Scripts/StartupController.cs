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
    private MachinePreviewLoader _loader;
    private ArcadeSession _arcade;
    private GameObject _roomHost;

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
            if (options.Mode == StartupMode.Arcade)
            {
                await SceneManager.LoadSceneAsync(options.SceneName, LoadSceneMode.Single);
                _arcade = new ArcadeSession();
                await _arcade.LoadAsync(options.ArcadeManifest);
                _roomHost = new GameObject("Playable arcade room");
                var room = _roomHost.AddComponent<PlayableMachineRoom>();
                room.InitializeArcade(_arcade.Stations, Camera.main, _arcade.Layout);
                room.UiIsOpen = () => PlayerUiRuntime.IsOpen;
                Debug.Log("Oasis arcade ready: " + _arcade.Stations.Count + " machines");
            }
            else
            {
                if (!RuntimeBuildLoader.TryLoad(options.BuildDirectory, out var build, out error)) { _errors.Show(error); return; }
                await SceneManager.LoadSceneAsync(options.SceneName, LoadSceneMode.Single);
                _loader = new MachinePreviewLoader(new GltfFastCabinetModelLoader());
                var machine = await _loader.LoadAsync(build);
                _roomHost = new GameObject("Playable preview room");
                var room = _roomHost.AddComponent<PlayableMachineRoom>();
                var selected = string.IsNullOrEmpty(options.Interaction) ? build.Machine.interaction.kind : options.Interaction;
                room.Initialize(machine, Camera.main, selected == "pool" || selected == "Pool" ? MachineInteractionProfile.Pool : MachineInteractionProfile.Buttons);
                room.UiIsOpen = () => PlayerUiRuntime.IsOpen;
                Debug.Log("Oasis playable preview ready: " + selected);
            }
        }
        catch (Exception ex)
        {
            if (_roomHost != null) Destroy(_roomHost);
            if (_loader != null) _loader.Unload();
            if (_arcade != null) _arcade.Dispose();
            _errors.Show(ex.Message);
        }
    }

    private void OnDestroy()
    {
        if (_instance != this) return;
        if (_loader != null) _loader.Unload();
        if (_arcade != null) _arcade.Dispose();
        _instance = null;
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
