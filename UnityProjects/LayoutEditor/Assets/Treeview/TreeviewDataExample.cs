using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// An example of tree data management logic.
/// </summary>
public class TreeviewDataExample : MonoBehaviour
{
    private const string treeviewComponentNotFound = "Treeview component not found.";
    private const string treeviewDisplayingByEditorDisabled = "Treeview displaying has been disabled in component \"Treeview\".";

    private Treeview treeview;

    /// <summary>
    /// Last event message.
    /// </summary>
    public Text Log;

    /// <summary>
    /// Gets the tree component, sets node event handlers, creates the root descendants.
    /// </summary>
    private void Awake()
    {
        if (!gameObject.TryGetComponent<Treeview>(out treeview))
        {
            Debug.LogError(treeviewComponentNotFound);
            return;
        }

        if (Log != null)
        {
            // Inherited by all descendants.
            treeview.Root.SelectHandler = new Node.NodeEventHandler((s, e) => 
                Log.text = $"Selected: {{Id: {e.Node.Id}, Text: \"{e.Node.Text}\"}}");
        }

        treeview.Root
            .AddChild("Lorem ipsum")
            .AddChild("Dolor sit amet", ReturnedNode.Created)
                .AddChild("Velit esse")
                .AddChild("Cillum")
                .AddChild("Fugiat nulla", ReturnedNode.Root)
            .AddChild("Consectetur")
            .AddChild("Adipiscing", ReturnedNode.Created)
                .AddChild("Excepteur", ReturnedNode.Created)
                    .AddChild("Cupidatat")
                    .AddChild("Non proident").Parent
                .AddChild("Sint occaecat")
            .AddChild("Ut aliquip", ReturnedNode.Root)
            .AddChild("Duis aute")
            .AddChild("Sunt in culpa")
            .AddChild("Qui officia")
            ;
    }
    
    /// <summary>
    /// Displays the tree.
    /// </summary>
    private void OnGUI()
    {
        treeview.SaveDefaultButtonStyle();

        if (treeview == null)
        {
            Debug.LogError(treeviewComponentNotFound);
            return;
        }

        if (treeview.DisplayInGame)
        {
            Debug.Log(treeviewDisplayingByEditorDisabled);
            treeview.DisplayInGame = false;
        }

        treeview.X = (Screen.width - treeview.Width) / 2;

        GUILayout.BeginArea(treeview.BackgroundRect);

        treeview.Display();

        GUILayout.EndArea();
    }
}
