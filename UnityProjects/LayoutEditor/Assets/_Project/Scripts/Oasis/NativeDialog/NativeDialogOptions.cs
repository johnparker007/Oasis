using System;

namespace Oasis.NativeDialog
{
    public enum NativeDialogIcon
    {
        None,
        Information,
        Warning,
        Error,
    }

    public sealed class NativeDialogOptions
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public bool ShowOkButton { get; set; } = true;
        public bool ShowCancelButton { get; set; }
        public bool ShowCloseButton { get; set; } = true;
        public NativeDialogIcon Icon { get; set; } = NativeDialogIcon.None;
        public Action OnOkClicked { get; set; }
        public Action OnCancelClicked { get; set; }
        public Action OnClosed { get; set; }
    }
}
