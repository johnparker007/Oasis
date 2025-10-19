using System;
using System.Collections;
using System.Collections.Generic;
using DynamicPanels;
using UnityEngine;
using UnityEngine.Events;
using Oasis.Layout;
using Oasis.LayoutEditor;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using Oasis.NativeProgress;
#endif

using Component = Oasis.Layout.Component;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace Oasis
{

    // TODO: Implement ISerializable for LayoutObject itself
    // Figure out how to trigger the nested serialization of the components
    
    public class LayoutObject : SerializableDictionary
    {
        // the data to be loaded/saved goes in this data class:
        [System.Serializable]
        public class LayoutData
        {
            public List<View> Views = new();
        }

        public void SetRepresentation(Dictionary<string, object> representation) {
            if ((string)representation["type"] != this.GetType().Name) {
                return;
            }
        }

        public Dictionary<string, object> GetRepresentation() {
            Dictionary<string, object> typeWrapper = new Dictionary<string, object>();
            typeWrapper["type"] = GetType().Name;
            typeWrapper["views"] = new Dictionary<string, object>();
            foreach (View view in Data.Views) {
                ((Dictionary<string, object>)typeWrapper["views"])[view.Name] = ((SerializableDictionary)view).GetRepresentation();
            }
            return typeWrapper;
        }


        public LayoutData Data = new LayoutData();

        public UnityEvent<Component, View> OnAddComponent = new();
        public UnityEvent<Component, View> OnRemoveComponent = new();
        public UnityEvent<View> OnAddView = new();
        public UnityEvent<View> OnRemoveView = new();

        public View BaseView
        {
            get
            {
                return GetView(ViewController.kBaseViewName);
            }
        }

        //private bool _changed = false;
        //private bool _dirty = false;

        public bool Dirty
        {
            get;
            set;
        }

        public View AddView(string name)
        {
            View view = new View();

            Data.Views.Add(view);

            view.Initialise(name);

            OnAddView?.Invoke(view);

            return view;
        }

        public bool TryAddViewQuad(View view, out ViewQuad createdViewQuad, string preferredBaseName = null)
        {
            createdViewQuad = null;
            if (view?.Data == null)
            {
                return false;
            }

            ViewQuad viewQuad = FindViewQuadWithoutName(view);
            if (viewQuad == null)
            {
                viewQuad = view.AddViewQuad();
            }

            if (viewQuad == null)
            {
                return false;
            }

            string viewQuadName = GetNextAvailableViewQuadName(preferredBaseName);
            viewQuad.Name = viewQuadName;
            view.TrySetActiveViewQuad(viewQuad);
            Dirty = true;
            view.OnChanged?.Invoke();
            createdViewQuad = viewQuad;
            return true;
        }

        private static ViewQuad FindViewQuadWithoutName(View view)
        {
            if (view == null)
            {
                return null;
            }

            IReadOnlyList<ViewQuad> viewQuads = view.ViewQuads;
            if (viewQuads == null)
            {
                return null;
            }

            for (int i = 0; i < viewQuads.Count; i++)
            {
                ViewQuad candidate = viewQuads[i];
                if (candidate != null && string.IsNullOrWhiteSpace(candidate.Name))
                {
                    return candidate;
                }
            }

            return null;
        }

        private string GetNextAvailableViewQuadName(string preferredBaseName)
        {
            const string kDefaultBaseName = "New ViewQuad";
            string trimmedBaseName = string.IsNullOrWhiteSpace(preferredBaseName)
                ? null
                : preferredBaseName.Trim();
            string baseName = string.IsNullOrEmpty(trimmedBaseName) ? kDefaultBaseName : trimmedBaseName;

            HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (View existingView in Data.Views)
            {
                if (existingView?.ViewQuads == null)
                {
                    continue;
                }

                foreach (ViewQuad existingQuad in existingView.ViewQuads)
                {
                    string existingName = existingQuad?.Name;
                    if (!string.IsNullOrWhiteSpace(existingName))
                    {
                        usedNames.Add(existingName);
                    }
                }
            }

            if (!usedNames.Contains(baseName))
            {
                return baseName;
            }

            int suffix = 2;
            while (true)
            {
                string candidate = $"{baseName} ({suffix})";
                if (!usedNames.Contains(candidate))
                {
                    return candidate;
                }

                suffix++;
            }
        }

        public void DeleteView(string name)
        {
            View view = GetView(name);
            DeleteView(view);
        }

        public void DeleteView(View view)
        {
            if (view != null)
            {
                OnRemoveView?.Invoke(view);
            }

            Data.Views.Remove(view);
        }

        public View GetView(string name)
        {
            return Data.Views.Find(x => x.Name == name);
        }

        public List<View> GetViews()
        {
            return Data.Views;
        }

        public Component GetComponentByGuid(string guid)
        {
            foreach(View view in Data.Views)
            {
                Component component = view.GetComponentByGuid(guid);
                if(component != null)
                {
                    return component;
                }
            }

            return null;
        }

        public void RemapLamps(string[] mfmeLampTable, string[] mameLampTable)
        {
            BaseView.RemapLamps(mfmeLampTable, mameLampTable);
        }

        public void OutputTransformedViewQuad()
        {
            bool progressWindowCreated = false;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (!NativeProgressWindow.EnsureWindowCreated(out string progressWindowError))
            {
                if (!string.IsNullOrEmpty(progressWindowError))
                {
                    Debug.LogWarning($"Unable to display progress window: {progressWindowError}");
                }
            }
            else
            {
                progressWindowCreated = true;
                NativeProgressWindow.UpdateContent("Creating View...", "Preparing view data...", false, 0.0f);
                NativeProgressWindow.UpdateProgress(0.0f);
            }
#endif

            void UpdateProgress(string message, float progress)
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                if (!progressWindowCreated)
                {
                    return;
                }

                float clampedProgress = Mathf.Clamp01(progress);
                NativeProgressWindow.UpdateContent("Creating View...", message ?? string.Empty, false, clampedProgress);
                NativeProgressWindow.UpdateProgress(clampedProgress);
#endif
            }

            Action<float> CreateTransformProgressHandler(float startProgress, float endProgress)
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                if (!progressWindowCreated)
                {
                    return null;
                }

                return transformProgress =>
                {
                    float clamped = Mathf.Clamp01(transformProgress);
                    int percent = Mathf.RoundToInt(clamped * 100f);
                    float overall = Mathf.Lerp(startProgress, endProgress, clamped);
                    string phaseMessage = $"Creating transformed image {percent}%";
                    NativeProgressWindow.UpdateContent("Creating View...", phaseMessage, false, overall);
                    NativeProgressWindow.UpdateProgress(overall);
                };
#else
                return null;
#endif
            }

            void CloseProgressWindow()
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                if (!progressWindowCreated)
                {
                    return;
                }

                NativeProgressWindow.CloseWindow();
#endif
            }

            try
            {
                const float PreparationProgress = 0.1f;
                const float CalculatingProgress = 0.2f;
                const float TransformStartProgress = 0.3f;
                const float TransformEndProgress = 0.75f;
                const float SavingProgress = 0.85f;
                const float FinalisingProgress = 0.95f;

                UpdateProgress("Preparing view data...", PreparationProgress);

                View baseView = BaseView;
                if (baseView == null)
                {
                    Debug.LogWarning("The loaded layout does not contain a base view.");
                    return;
                }

                ComponentBackground backgroundComponent = null;
                foreach (Component component in baseView.Data.Components)
                {
                    if (component is ComponentBackground candidate && candidate.OasisImage != null)
                    {
                        backgroundComponent = candidate;
                        break;
                    }
                }

                if (backgroundComponent == null || backgroundComponent.OasisImage == null)
                {
                    Debug.LogWarning("The base view does not contain a background image to transform.");
                    return;
                }

                Oasis.Graphics.OasisImage sourceImage = backgroundComponent.OasisImage;
                if (sourceImage.Width <= 0 || sourceImage.Height <= 0)
                {
                    Debug.LogWarning("The background image has invalid dimensions.");
                    return;
                }

                Vector2[] viewQuadPoints = baseView.ActiveViewQuad?.Points;
                if (viewQuadPoints == null || viewQuadPoints.Length < 4)
                {
                    Debug.LogWarning("The base view quad is not defined.");
                    return;
                }

                bool hasConfiguredPoint = false;
                foreach (Vector2 point in viewQuadPoints)
                {
                    if (!Mathf.Approximately(point.x, 0f) || !Mathf.Approximately(point.y, 0f))
                    {
                        hasConfiguredPoint = true;
                        break;
                    }
                }

                if (!hasConfiguredPoint)
                {
                    Debug.LogWarning("The base view quad has not been configured.");
                    return;
                }

                UpdateProgress("Calculating transform...", CalculatingProgress);

                // The OasisImage.Transform implementation expects the corner points to be supplied in the
                // same order as the working debug routine (bottom-left, bottom-right, top-right, top-left)
                // otherwise the output becomes flipped. Match that ordering when converting the base view quad.
                Vector2Int pointA = ConvertViewQuadPointToImagePoint(viewQuadPoints[(int)ViewQuad.PointTypes.BottomLeft], sourceImage);
                Vector2Int pointB = ConvertViewQuadPointToImagePoint(viewQuadPoints[(int)ViewQuad.PointTypes.BottomRight], sourceImage);
                Vector2Int pointC = ConvertViewQuadPointToImagePoint(viewQuadPoints[(int)ViewQuad.PointTypes.TopRight], sourceImage);
                Vector2Int pointD = ConvertViewQuadPointToImagePoint(viewQuadPoints[(int)ViewQuad.PointTypes.TopLeft], sourceImage);

                // TODO this aspect ratio calc is just placeholder - ultimately there will be a 'cabinet' loaded from the
                // cabinet editor, and the view quad will be linked to one of the cabinet panels, and then the aspect ratio
                // will come from that cabinet panel width/height

                // from original ArcadeSim Eclipse cabinet:
                float bottomPanelWidth = 0.63f;
                float bottomPanelHeight = 0.3457056f;

                float targetAspectRatio = bottomPanelWidth / bottomPanelHeight;

                UpdateProgress("Creating transformed image 0%", TransformStartProgress);
                var transformProgressHandler = CreateTransformProgressHandler(TransformStartProgress, TransformEndProgress);

                Oasis.Graphics.OasisImage transformedImage = Oasis.Graphics.OasisImage.Transform(
                    sourceImage,
                    pointA,
                    pointB,
                    pointC,
                    pointD,
                    targetAspectRatio,
                    transformProgressHandler);

                var projectsController = Editor.Instance?.ProjectsController;
                if (projectsController == null || string.IsNullOrEmpty(projectsController.ProjectAssetsPath))
                {
                    Debug.LogWarning("Project assets path is unavailable; unable to save the transformed ViewQuad image.");
                    return;
                }

                var tabController = Editor.Instance?.TabController;
                if (tabController == null)
                {
                    Debug.LogWarning("Tab controller is unavailable; unable to create a view for the transformed ViewQuad image.");
                    return;
                }

                string viewName = GetBaseViewQuadName();
                if (string.IsNullOrWhiteSpace(viewName))
                {
                    Debug.LogWarning("The base view quad does not have a name; unable to output the transformed ViewQuad image.");
                    return;
                }

                string viewFolderName = string.Concat("View", viewName);
                string relativeImagePath = Path.Combine(viewFolderName, "Background.png");
                relativeImagePath = relativeImagePath.Replace('\\', '/');

                View bottomView = GetView(viewName);
                if (bottomView == null)
                {
                    bottomView = AddView(viewName);
                }
                else
                {
                    bottomView.RemoveAllComponents();
                }

                if (!TryEnsureViewTab(bottomView, TabController.TabTypes.TestNewView))
                {
                    Debug.LogWarning($"Unable to display the '{viewName}' view tab for the transformed ViewQuad image.");
                    return;
                }

                ComponentBackground bottomBackground = new ComponentBackground
                {
                    Name = $"{viewName} Background",
                    Position = Vector2Int.zero,
                    Size = new Vector2Int(transformedImage.Width, transformedImage.Height),
                    OasisImage = transformedImage,
                    RelativeAssetPath = relativeImagePath,
                    Color = backgroundComponent.Color
                };

                UpdateProgress("Saving transformed image...", SavingProgress);

                Oasis.Graphics.ImageOperations.SaveToPNG(transformedImage, relativeImagePath);

                UpdateProgress("Finalising view...", FinalisingProgress);

                bottomView.AddComponent(bottomBackground);

                string absolutePath = Path.Combine(projectsController.ProjectAssetsPath, relativeImagePath);
                Debug.Log($"Saved transformed ViewQuad image to {absolutePath}");

                UpdateProgress("View created", 1.0f);
            }
            finally
            {
                CloseProgressWindow();
            }
        }

        public bool TryEnsureViewTab(View view, TabController.TabTypes tabType, Panel anchorPanel = null)
        {
            if (view == null)
            {
                return false;
            }

            var tabController = Editor.Instance?.TabController;
            if (tabController == null)
            {
                Debug.LogWarning("Tab controller is unavailable; unable to display layout views.");
                return false;
            }

            var panelTab = tabController.ShowTab(tabType, anchorPanel);
            var panel = panelTab?.Panel;

            EditorView editorView = panel != null
                ? panel.GetComponentInChildren<EditorView>(true)
                : ViewController.GetEditorView(view.Name);

            if (editorView == null)
            {
                Debug.LogWarning($"Unable to locate an EditorView for the '{view.Name}' view tab.");
                return false;
            }

            editorView.ViewName = view.Name;
            editorView.Initialise();

            if (panelTab != null)
            {
                panelTab.Label = view.Name;
            }

            return true;
        }

        public string GetBaseViewQuadName()
        {
            View baseView = BaseView;
            if (baseView == null || !baseView.HasViewQuad)
            {
                return string.Empty;
            }

            return baseView.ActiveViewQuad?.Name ?? string.Empty;
        }

        private static Vector2Int ConvertViewQuadPointToImagePoint(Vector2 layoutPoint, Oasis.Graphics.OasisImage sourceImage)
        {
            int x = Mathf.RoundToInt(layoutPoint.x);
            int y = Mathf.RoundToInt(layoutPoint.y);

            int clampedX = Mathf.Clamp(x, 0, Mathf.Max(0, sourceImage.Width - 1));
            int flippedY = sourceImage.Height - 1 - y;
            flippedY = Mathf.Clamp(flippedY, 0, Mathf.Max(0, sourceImage.Height - 1));

            return new Vector2Int(clampedX, flippedY);
        }
    }
}
