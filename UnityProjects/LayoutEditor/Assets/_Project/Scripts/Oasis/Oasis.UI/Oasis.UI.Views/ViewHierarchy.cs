using Oasis.UI.ViewModels;
using System.Drawing;
using System.Windows.Forms;
using Oasis.Layout;
using System.Collections.Generic;

namespace Oasis.UI.Views
{
    public class ViewHierarchy : ViewTab
    {
        public override int ControlWidth => 220;

        public override bool LeftAlignedPosition => true;

        public override string TabName => "Hierarchy";

        private TreeView _treeView = null;


        public ViewHierarchy(RootUI rootUI, Control parent, ViewModel viewModel) : base(rootUI, parent, viewModel)
        {
            _treeView = new TreeView();
            _treeView.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom;
            //_treeView.Location = new Point(0, uwfHeaderHeight - 1); // All controls should be placed with Form header offset.
            //_treeView.Location = new Point(0, uwfHeaderHeight - 1 + _menuStrip.Height);
            //_treeView.Height = Height - uwfHeaderHeight + 1 - _menuStrip.Height;
            //_treeView.TabStop = false;
            _treeView.Size = _tabControl.Size;
            //_treeView.BackColor = Color.Aquamarine;
            //_treeView.NodeMouseClick += TreeViewOnNodeMouseClick;

            BuildHierarchyData();

            _tabPage.Controls.Add(_treeView);
        }

        private void BuildHierarchyData()
        {
            LayoutObject layoutObject = _rootUI.UIController.LayoutEditor.Layout;
            if(layoutObject == null)
            {
                return;
            }

            BuildComponentNode(layoutObject, typeof(ComponentLamp), "Lamps", "Lamp");
            BuildComponentNode(layoutObject, typeof(ComponentSwitch), "Switches", "Switch");
            BuildComponentNode(layoutObject, typeof(ComponentButton), "Buttons", "Button");
            BuildComponentNode(layoutObject, typeof(ComponentReel), "Reels", "Reel");
            BuildComponentNode(layoutObject, typeof(ComponentBackground), "Backgrounds", "Background");
            BuildComponentNode(layoutObject, typeof(ComponentSevenSegment), "Seven Segments", "Seven Segment");
            BuildComponentNode(layoutObject, typeof(ComponentAlpha), "Alphas", "Alpha");


            // Refresh method or ExpandAll will update view list. 
            // NOTE: most controls don't need to be refreshed. Make sure to take a look 
            // at Refresh implementation in Control that you think is not working.
            _treeView.ExpandAll(); // JP need this to create view list

            _treeView.CollapseAll();

            // Grip renderer is normal control. Bring it to front if you use it over other controls that can technically hide it.
            // uwfSizeGripRenderer.BringToFront();
        }

        private void BuildComponentNode(LayoutObject layoutObject, System.Type type, string name, string subNodeName)
        {
            List<Component> components = layoutObject.Components.FindAll(x => x.GetType() == type);
            TreeNode node = new TreeNode(name + " (" + components.Count + ")");

            for(int componentIndex = 0; componentIndex < components.Count; ++componentIndex)
            {
                AddNode(node, subNodeName + (componentIndex + 1), null);
            }

            _treeView.Nodes.Add(node);
        }

        private static void AddNode(TreeNode parent, string text, object tag)
        {
            var node = new TreeNode(text);
            node.Tag = tag;

            parent.Nodes.Add(node);
        }
        private void TreeViewOnNodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            //if (e.Button != MouseButtons.Left || e.Node == null || e.Node.Tag == null)
            //    return;

            //var panelType = e.Node.Tag as Type;
            //if (panelType == null)
            //    return;

            //var panel = Activator.CreateInstance(panelType) as BaseExamplePanel;
            //if (panel == null)
            //    return;

            //SetPanel(panel);
        }


    }
}
