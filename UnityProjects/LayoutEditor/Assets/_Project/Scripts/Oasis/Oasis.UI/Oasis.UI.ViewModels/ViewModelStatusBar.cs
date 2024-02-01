namespace Oasis.UI.ViewModels
{
    public class ViewModelStatusBar : ViewModel
    {
        //public UnityEvent OnZoomChanged = new UnityEvent();

        //public ViewStatusBar ViewStatusBar
        //{
        //    get
        //    {
        //        return (ViewStatusBar)_view;
        //    }
        //}

        //public Size PanelSize
        //{
        //    get
        //    {
        //        return ViewStatusBar.Panel.Size;
        //    }
        //}

        //public Zoom Zoom
        //{
        //    get
        //    {
        //        return _rootUI.UIController.LayoutEditor.Zoom;
        //    }
        //}

        public ViewModelStatusBar(RootUI rootUI) : base(rootUI)
        {
            //_view = new ViewStatusBar(rootUI, this);

            //Zoom.OnZoomLevelSet.AddListener(OnZoomLevelSet);
        }

        //private void OnZoomLevelSet(float zoomLevel)
        //{
        //    OnZoomChanged?.Invoke();
        //}
    }
}
