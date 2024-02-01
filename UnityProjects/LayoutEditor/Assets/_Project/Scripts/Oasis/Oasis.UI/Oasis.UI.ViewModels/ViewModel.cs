namespace Oasis.UI.ViewModels
{
    public abstract class ViewModel 
    {
        protected RootUI _rootUI = null;
        //protected ViewBase _view = null;

        public ViewModel(RootUI rootUI)
        {
            _rootUI = rootUI;
        }

        //public virtual void Update()
        //{

        //}
    }
}
