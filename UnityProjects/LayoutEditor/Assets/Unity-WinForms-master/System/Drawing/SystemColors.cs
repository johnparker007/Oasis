namespace System.Drawing
{
    public static class SystemColors
    {
        public static readonly Color ActiveBorder = new Color(KnownColor.ActiveBorder, 0xFFB4B4B4);
        public static readonly Color ActiveCaption = new Color(KnownColor.ActiveCaption, 0xFF99B4D1);
        public static readonly Color ActiveCaptionText = new Color(KnownColor.ActiveCaptionText, 0xFF000000);
        public static readonly Color AppWorkspace = new Color(KnownColor.AppWorkspace, 0xFFABABAB);
        public static readonly Color ButtonFace = new Color(KnownColor.ButtonFace, 0xFFF0F0F0);
        public static readonly Color ButtonHighlight = new Color(KnownColor.ButtonHighlight, 0xFFFFFFFF); // unchanged, as cannot see used anywhere in the demo
        public static readonly Color ButtonShadow = new Color(KnownColor.ButtonShadow, 0xFFA0A0A0);
        public static readonly Color Control = new Color(KnownColor.Control, 0xFF484848); // OK
        public static readonly Color ControlDark = new Color(KnownColor.ControlDark, 0xFF191919); // OK
        public static readonly Color ControlDarkDark = new Color(KnownColor.ControlDarkDark, 0xFF101010); // OK
        public static readonly Color ControlLight = new Color(KnownColor.ControlLight, 0xFFefefef); // ok - part of 3d bevelled area
        public static readonly Color ControlLightLight = new Color(KnownColor.ControlLightLight, 0xFFFFFFFF);// unchanged, as cannot see used anywhere in the demo
        public static readonly Color ControlText = new Color(KnownColor.ControlText, 0xFFFFFFFF); // can see in the font dialog sample text, guess FF for now
        public static readonly Color Desktop = new Color(KnownColor.Desktop, 0xFF000000);// unchanged, as cannot see used anywhere in the demo
        public static readonly Color GradientActiveCaption = new Color(KnownColor.GradientActiveCaption, 0xFFB9D1EA); // unchanged, as cannot see used anywhere in the demo
        public static readonly Color GradientInactiveCaption = new Color(KnownColor.GradientInactiveCaption, 0xFFD7E4F2);// unchanged, as cannot see used anywhere in the demo
        public static readonly Color GrayText = new Color(KnownColor.GrayText, 0xFF6D6D6D); // still TODO can see in the date picker only
        public static readonly Color Highlight = new Color(KnownColor.Highlight, 0xFF3e5f96); // ok
        public static readonly Color HighlightText = new Color(KnownColor.HighlightText, 0xFFFFFFFF); // OK
        public static readonly Color HotTrack = new Color(KnownColor.HotTrack, 0xFFffffff); // OK - only saw used in calendar
        public static readonly Color InactiveBorder = new Color(KnownColor.InactiveBorder, 0xFFF4F7FC);
        public static readonly Color InactiveCaption = new Color(KnownColor.InactiveCaption, 0xFF969696); // OKish - used for Disabled button text in demo
        public static readonly Color InactiveCaptionText = new Color(KnownColor.InactiveCaptionText, 0xFF000000);// unchanged, as cannot see used anywhere in the demo
        public static readonly Color Info = new Color(KnownColor.Info, 0xFFFFFFFF); // unchanged, as cannot see used anywhere in the demo
        public static readonly Color InfoText = new Color(KnownColor.InfoText, 0xFFffffff); // current month days in MonthCalendar
        public static readonly Color Menu = new Color(KnownColor.Menu, 0xFFff00ff);
        public static readonly Color MenuBar = new Color(KnownColor.MenuBar, 0xFFFff00ff);
        public static readonly Color MenuHighlight = new Color(KnownColor.MenuHighlight, 0xFFff00ff);// unchanged, as cannot see used anywhere in the demo
        public static readonly Color MenuText = new Color(KnownColor.MenuText, 0xFF000000);//c9// unchanged, as cannot see used anywhere in the demo
        public static readonly Color ScrollBar = new Color(KnownColor.ScrollBar, 0xFFff00ff);// unchanged, as cannot see used anywhere in the demo
        public static readonly Color Window = new Color(KnownColor.Window, 0xFF2A2A2A); // OK - seems to actually be text input fields though
        public static readonly Color WindowFrame = new Color(KnownColor.WindowFrame, 0xff1e1e1e); // prob OK - seems to be used as an outline for a label
        public static readonly Color WindowText = new Color(KnownColor.WindowText, 0xFF000000); // unchanged, as cannot see used anywhere in the demo

        internal static readonly Color uwfControlText = new Color(KnownColor.ControlText, 0xFFC9C9C9); // from unity Hierachy pane text
        internal static readonly Color uwfInfoText = new Color(KnownColor.InfoText, 0xFF404040);// unchanged, as cannot see used anywhere in the demo

        // 0xFF383838); // from unity Hierachy pane background
        // 0xC9C9C9  // from unity Hierachy pane text



        // JP ORIGINAL COLORS - commented to test making a dark theme:
        //public static readonly Color ActiveBorder = new Color(KnownColor.ActiveBorder, 0xFFB4B4B4);
        //public static readonly Color ActiveCaption = new Color(KnownColor.ActiveCaption, 0xFF99B4D1);
        //public static readonly Color ActiveCaptionText = new Color(KnownColor.ActiveCaptionText, 0xFF000000);
        //public static readonly Color AppWorkspace = new Color(KnownColor.AppWorkspace, 0xFFABABAB);
        //public static readonly Color ButtonFace = new Color(KnownColor.ButtonFace, 0xFFF0F0F0);
        //public static readonly Color ButtonHighlight = new Color(KnownColor.ButtonHighlight, 0xFFFFFFFF);
        //public static readonly Color ButtonShadow = new Color(KnownColor.ButtonShadow, 0xFFA0A0A0);
        //public static readonly Color Control = new Color(KnownColor.Control, 0xFFF0F0F0);
        //public static readonly Color ControlDark = new Color(KnownColor.ControlDark, 0xFFA0A0A0);
        //public static readonly Color ControlDarkDark = new Color(KnownColor.ControlDarkDark, 0xFF696969);
        //public static readonly Color ControlLight = new Color(KnownColor.ControlLight, 0xFFE3E3E3);
        //public static readonly Color ControlLightLight = new Color(KnownColor.ControlLightLight, 0xFFFFFFFF);
        //public static readonly Color ControlText = new Color(KnownColor.ControlText, 0xFF000000);
        //public static readonly Color Desktop = new Color(KnownColor.Desktop, 0xFF000000);
        //public static readonly Color GradientActiveCaption = new Color(KnownColor.GradientActiveCaption, 0xFFB9D1EA);
        //public static readonly Color GradientInactiveCaption = new Color(KnownColor.GradientInactiveCaption, 0xFFD7E4F2);
        //public static readonly Color GrayText = new Color(KnownColor.GrayText, 0xFF6D6D6D);
        //public static readonly Color Highlight = new Color(KnownColor.Highlight, 0xFF3399FF);
        //public static readonly Color HighlightText = new Color(KnownColor.HighlightText, 0xFFFFFFFF);
        //public static readonly Color HotTrack = new Color(KnownColor.HotTrack, 0xFF0066CC);
        //public static readonly Color InactiveBorder = new Color(KnownColor.InactiveBorder, 0xFFF4F7FC);
        //public static readonly Color InactiveCaption = new Color(KnownColor.InactiveCaption, 0xFFBFCDDB);
        //public static readonly Color InactiveCaptionText = new Color(KnownColor.InactiveCaptionText, 0xFF000000);
        //public static readonly Color Info = new Color(KnownColor.Info, 0xFFFFFFFF);
        //public static readonly Color InfoText = new Color(KnownColor.InfoText, 0xFF000000);
        //public static readonly Color Menu = new Color(KnownColor.Menu, 0xFFF0F0F0);
        //public static readonly Color MenuBar = new Color(KnownColor.MenuBar, 0xFFF0F0F0);
        //public static readonly Color MenuHighlight = new Color(KnownColor.MenuHighlight, 0xFF3399FF);
        //public static readonly Color MenuText = new Color(KnownColor.MenuText, 0xFF000000);
        //public static readonly Color ScrollBar = new Color(KnownColor.ScrollBar, 0xFFC8C8C8);
        //public static readonly Color Window = new Color(KnownColor.Window, 0xFFFFFFFF);
        //public static readonly Color WindowFrame = new Color(KnownColor.WindowFrame, 0xFF646464);
        //public static readonly Color WindowText = new Color(KnownColor.WindowText, 0xFF000000);

        //internal static readonly Color uwfControlText = new Color(KnownColor.ControlText, 0xFF404040);
        //internal static readonly Color uwfInfoText = new Color(KnownColor.InfoText, 0xFF404040);
    }
}
