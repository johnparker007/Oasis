using System.Windows;

namespace OasisEditor;

internal static class PanelElementModelFactory
{
    public const double NewRectangleWidth = 180;
    public const double NewRectangleHeight = 120;
    public const double NewImageWidth = 220;
    public const double NewImageHeight = 140;
    public const double NewLampWidth = 80;
    public const double NewLampHeight = 40;
    public const double NewReelWidth = 120;
    public const double NewReelHeight = 180;
    public const double NewSevenSegmentWidth = 80;
    public const double NewSevenSegmentHeight = 120;
    public const double NewSegmentAlphaWidth = 220;
    public const double NewSegmentAlphaHeight = 60;
    public const double NewVfdDotMatrixWidth = 480;
    public const double NewVfdDotMatrixHeight = 80;


    public static PanelElementFile CreateRectangleElement(Point canvasPoint)
    {
        var x = Math.Max(0, canvasPoint.X - (NewRectangleWidth / 2));
        var y = Math.Max(0, canvasPoint.Y - (NewRectangleHeight / 2));
        var objectId = Guid.NewGuid().ToString("N");
        return new PanelElementFile
        {
            ObjectId = objectId,
            Name = Panel2DDocumentStorage.CreateDefaultElementName(PanelElementKind.Rectangle, objectId),
            Kind = Panel2DDocumentStorage.SerializeElementKind(PanelElementKind.Rectangle),
            X = x,
            Y = y,
            Width = NewRectangleWidth,
            Height = NewRectangleHeight
        };
    }

    public static PanelElementFile CreateImageElement(Point canvasPoint)
    {
        var x = Math.Max(0, canvasPoint.X - (NewImageWidth / 2));
        var y = Math.Max(0, canvasPoint.Y - (NewImageHeight / 2));
        var objectId = Guid.NewGuid().ToString("N");
        return new PanelElementFile
        {
            ObjectId = objectId,
            Name = Panel2DDocumentStorage.CreateDefaultElementName(PanelElementKind.Image, objectId),
            Kind = Panel2DDocumentStorage.SerializeElementKind(PanelElementKind.Image),
            X = x,
            Y = y,
            Width = NewImageWidth,
            Height = NewImageHeight
        };
    }



    public static PanelElementFile CreateAddableElement(AddablePanelElementKind kind, Point panelPoint)
    {
        return kind switch
        {
            AddablePanelElementKind.Lamp => CreateLampElement(panelPoint),
            AddablePanelElementKind.Reel => CreateReelElement(panelPoint),
            AddablePanelElementKind.SevenSegmentDisplay => CreateSevenSegmentDisplayElement(panelPoint),
            AddablePanelElementKind.SegmentAlpha => CreateSegmentAlphaElement(panelPoint),
            AddablePanelElementKind.VfdDotMatrix => CreateVfdDotMatrixElement(panelPoint),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported addable Panel2D element kind.")
        };
    }

    private static PanelElementFile CreateLampElement(Point panelPoint)
    {
        var objectId = Guid.NewGuid().ToString("N");
        return CreateBaseElement(PanelElementKind.Lamp, objectId, "Lamp", panelPoint, NewLampWidth, NewLampHeight) with
        {
            OnColorHex = "#FF3030",
            OffColorHex = "#2A0505",
            TextColorHex = "#FFFFFF",
            TextBoxFontName = "Tahoma",
            TextBoxFontStyle = "Regular",
            TextBoxFontSize = "8"
        };
    }

    private static PanelElementFile CreateReelElement(Point panelPoint)
    {
        var objectId = Guid.NewGuid().ToString("N");
        return CreateBaseElement(PanelElementKind.Reel, objectId, "Reel 1", panelPoint, NewReelWidth, NewReelHeight) with
        {
            DisplayNumber = 1,
            Stops = 24,
            VisibleScale = 3d / 24d,
            IsReversed = false
        };
    }

    private static PanelElementFile CreateSevenSegmentDisplayElement(Point panelPoint)
    {
        var objectId = Guid.NewGuid().ToString("N");
        return CreateBaseElement(PanelElementKind.SevenSegment, objectId, "7 Segment Display", panelPoint, NewSevenSegmentWidth, NewSevenSegmentHeight) with
        {
            DisplayNumber = 1,
            OnColorHex = "#FF4040"
        };
    }

    private static PanelElementFile CreateSegmentAlphaElement(Point panelPoint)
    {
        var objectId = Guid.NewGuid().ToString("N");
        return CreateBaseElement(PanelElementKind.Alpha, objectId, "Segment Alpha", panelPoint, NewSegmentAlphaWidth, NewSegmentAlphaHeight) with
        {
            SegmentDisplayType = "led16seg",
            OnColorHex = "#FFB000",
            IsReversed = false
        };
    }

    private static PanelElementFile CreateVfdDotMatrixElement(Point panelPoint)
    {
        var objectId = Guid.NewGuid().ToString("N");
        return CreateBaseElement(PanelElementKind.VfdDotMatrix, objectId, "VFD Dot Matrix", panelPoint, NewVfdDotMatrixWidth, NewVfdDotMatrixHeight) with
        {
            OnColorHex = "#FFB000"
        };
    }

    private static PanelElementFile CreateBaseElement(PanelElementKind kind, string objectId, string name, Point panelPoint, double width, double height)
    {
        return new PanelElementFile
        {
            ObjectId = objectId,
            Name = name,
            Kind = Panel2DDocumentStorage.SerializeElementKind(kind),
            X = Math.Max(0, panelPoint.X),
            Y = Math.Max(0, panelPoint.Y),
            Width = width,
            Height = height,
            IsVisible = true
        };
    }
}

internal enum AddablePanelElementKind
{
    Lamp,
    Reel,
    SevenSegmentDisplay,
    SegmentAlpha,
    VfdDotMatrix
}
