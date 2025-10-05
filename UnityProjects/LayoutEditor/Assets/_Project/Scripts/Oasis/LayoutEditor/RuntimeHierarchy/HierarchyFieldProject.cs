using System.Reflection;
using RuntimeInspectorNamespace;
using UnityEngine;
using UnityEngine.UI;

public class HierarchyFieldProject : HierarchyField
{
    private const float PackageTextXOffset = 35f;
    private const float ProjectTextXOffset = 50f;
    private const float OffsetDelta = ProjectTextXOffset - PackageTextXOffset;

    [SerializeField]
    private RectTransform projectContentTransform;

    [SerializeField]
    private Text projectNameText;

    [SerializeField]
    private Toggle projectMultiSelectionToggle;

    private static readonly MethodInfo PreferredWidthSetter = typeof(HierarchyField)
        .GetProperty(nameof(PreferredWidth), BindingFlags.Instance | BindingFlags.Public)
        ?.GetSetMethod(true);

    private static readonly FieldInfo ContentTransformField = typeof(HierarchyField)
        .GetField("contentTransform", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo NameTextField = typeof(HierarchyField)
        .GetField("nameText", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo MultiSelectionToggleField = typeof(HierarchyField)
        .GetField("multiSelectionToggle", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo ShouldRecalculateContentWidthField = typeof(RuntimeHierarchy)
        .GetField("shouldRecalculateContentWidth", BindingFlags.Instance | BindingFlags.NonPublic);

    private float lastAppliedWidth = float.NaN;
    private int lastAppliedDepth = int.MinValue;
    private bool lastToggleVisibility;
    private bool forceNextUpdate;

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
        ForceNextUpdate();
    }

    private void OnValidate()
    {
        EnsureReferences();
        ForceNextUpdate();
    }

    private void LateUpdate()
    {
        EnsureReferences();

        if (!projectContentTransform || !projectNameText || Skin == null || Data == null)
            return;

        bool toggleVisible = projectMultiSelectionToggle && projectMultiSelectionToggle.gameObject.activeSelf;
        float nameWidth = projectNameText.rectTransform.sizeDelta.x;
        float targetWidth = Data.Depth * Skin.IndentAmount + ProjectTextXOffset + nameWidth;

        bool requiresUpdate = forceNextUpdate ||
                              !Mathf.Approximately(lastAppliedWidth, targetWidth) ||
                              lastAppliedDepth != Data.Depth ||
                              lastToggleVisibility != toggleVisible;

        if (!requiresUpdate)
            return;

        forceNextUpdate = false;
        lastAppliedWidth = targetWidth;
        lastAppliedDepth = Data.Depth;
        lastToggleVisibility = toggleVisible;

        float baseX = Skin.IndentAmount * Data.Depth + (toggleVisible ? Skin.LineHeight * 0.8f : 0f);
        Vector2 anchoredPosition = projectContentTransform.anchoredPosition;
        projectContentTransform.anchoredPosition = new Vector2(baseX + OffsetDelta, anchoredPosition.y);

        if (PreferredWidthSetter != null && !Mathf.Approximately(PreferredWidth, targetWidth))
            PreferredWidthSetter.Invoke(this, new object[] { targetWidth });

        MarkHierarchyContentWidthDirty();
    }

    private void ForceNextUpdate()
    {
        forceNextUpdate = true;
        lastAppliedWidth = float.NaN;
        lastAppliedDepth = int.MinValue;
    }

    private void EnsureReferences()
    {
        if (!projectContentTransform && ContentTransformField != null)
            projectContentTransform = ContentTransformField.GetValue(this) as RectTransform;

        if (!projectNameText && NameTextField != null)
            projectNameText = NameTextField.GetValue(this) as Text;

        if (!projectMultiSelectionToggle && MultiSelectionToggleField != null)
            projectMultiSelectionToggle = MultiSelectionToggleField.GetValue(this) as Toggle;
    }

    private void MarkHierarchyContentWidthDirty()
    {
        if (Hierarchy == null || ShouldRecalculateContentWidthField == null)
            return;

        ShouldRecalculateContentWidthField.SetValue(Hierarchy, true);
    }
}
