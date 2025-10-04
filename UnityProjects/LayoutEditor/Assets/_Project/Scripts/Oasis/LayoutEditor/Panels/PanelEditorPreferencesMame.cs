using Oasis.Download;
using Oasis.LayoutEditor.Tools;
using Oasis.MAME;
using Oasis.NativeProgress;
using Oasis.UI;
using Oasis.UI.Fields;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Oasis.LayoutEditor.Panels
{
    public class PanelEditorPreferencesMame : PanelBase
    {
        public FieldInt MameVersion;
        public Button ButtonInstallSelectedVersion;

        protected override void AddListeners()
        {
            MameVersion.Input.OnValueChanged += OnMameVersionValueChanged;
            MameVersion.Input.OnValueSubmitted += OnMameVersionValueChanged;

            ButtonInstallSelectedVersion.onClick.AddListener(OnButtonInstallSelectedVersionClick);
        }

        protected override void RemoveListeners()
        {
            MameVersion.Input.OnValueChanged -= OnMameVersionValueChanged;
            MameVersion.Input.OnValueSubmitted -= OnMameVersionValueChanged;

            ButtonInstallSelectedVersion.onClick.RemoveListener(OnButtonInstallSelectedVersionClick);
        }

        protected override void Initialise()
        {
            if (_initialised)
            {
                return;
            }

            _initialised = true;
        }

        protected override void Populate()
        {
            MameVersion.Input.Text = Editor.Instance.Preferences.MameVersion.ToString();
        }

        private bool OnMameVersionValueChanged(BoundInputField source, string value)
        {
            Editor.Instance.Preferences.MameVersion = int.Parse(value);

            return true;
        }

        private async void OnButtonInstallSelectedVersionClick()
        {
            bool progressWindowCreated = false;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (NativeProgressWindow.EnsureWindowCreated(out string errorMessage))
            {
                progressWindowCreated = true;
            }
            else if (!string.IsNullOrEmpty(errorMessage))
            {
                Debug.LogError($"Failed to create native progress window: {errorMessage}");
            }
#endif

            try
            {
                await MameDownloader.Instance.DownloadAndExtractAsync(
                    Editor.Instance.Preferences.MameVersion,
                    stage =>
                    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                        if (!progressWindowCreated)
                        {
                            return;
                        }

                        switch (stage)
                        {
                            case MameDownloader.MameDownloadStage.Downloading:
                                NativeProgressWindow.UpdateContent(
                                    "Downloading MAME...",
                                    FormatBytesDownloadedLabel(0),
                                    false,
                                    0.25f);
                                break;
                            case MameDownloader.MameDownloadStage.Extracting:
                                NativeProgressWindow.UpdateContent("Extracting MAME...", "Extracting MAME... 0%", false, 0.5f);
                                break;
                            case MameDownloader.MameDownloadStage.InstallingPlugins:
                                NativeProgressWindow.UpdateContent("Copying Oasis Plugin...", "Install plugins...", false, 0.75f);
                                break;
                        }
#endif
                    },
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                    bytesDownloaded =>
                    {
                        if (!progressWindowCreated)
                        {
                            return;
                        }

                        NativeProgressWindow.UpdateContent(
                            "Downloading MAME...",
                            FormatBytesDownloadedLabel(bytesDownloaded),
                            false,
                            0.25f);
                    },
                    progress =>
                    {
                        if (!progressWindowCreated)
                        {
                            return;
                        }

                        float clamped = Mathf.Clamp01(progress);
                        int percentValue = Mathf.Clamp(Mathf.RoundToInt(clamped * 100f), 0, 100);

                        NativeProgressWindow.UpdateContent(
                            "Extracting MAME...",
                            $"Extracting MAME... {percentValue}%",
                            false,
                            null);

                        NativeProgressWindow.UpdateProgress(Mathf.Lerp(0.5f, 0.75f, clamped));
                    }
#else
                    null,
                    null
#endif
                    );
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to install selected MAME version: {exception}");
            }
            finally
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                if (progressWindowCreated)
                {
                    NativeProgressWindow.CloseWindow();
                }
#endif
            }
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private static string FormatBytesDownloadedLabel(long bytesDownloaded)
        {
            return $"{bytesDownloaded:N0} bytes downloaded";
        }
#endif
    }

}
