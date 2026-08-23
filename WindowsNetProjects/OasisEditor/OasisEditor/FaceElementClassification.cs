namespace OasisEditor;

public enum FaceElementCategory { Artwork, Component, Illumination }

/// <summary>The single subsystem boundary for every persisted Face element type.</summary>
public static class FaceElementClassification
{
    public static FaceElementCategory GetCategory(FaceElementModel element) => element switch
    {
        FaceArtworkElement => FaceElementCategory.Artwork,
        FaceReelDisplayElement or FaceSevenSegmentDisplayElement or FaceAlphaDisplayElement or FaceButtonElement => FaceElementCategory.Component,
        FaceLampWindowElement or FaceLampEmitterElement => FaceElementCategory.Illumination,
        _ => throw new ArgumentOutOfRangeException(nameof(element), element.GetType().FullName, "Unclassified Face element type.")
    };

    public static bool IsComponent(FaceElementModel element) => GetCategory(element) == FaceElementCategory.Component;
}

public enum FaceComponentKind { Reel, Button, SevenSegmentDisplay, AlphaDisplay }

/// <summary>Central Face-logical defaults used by native component placement.</summary>
internal static class FaceComponentFactory
{
    public static FaceElementModel Create(FaceComponentKind kind, double x, double y, double? width = null, double? height = null)
    {
        var (name, defaultWidth, defaultHeight) = kind switch
        {
            FaceComponentKind.Reel => ("Reel", 120d, 180d),
            FaceComponentKind.Button => ("Button", 80d, 40d),
            FaceComponentKind.SevenSegmentDisplay => ("Seven-Segment Display", 100d, 48d),
            FaceComponentKind.AlphaDisplay => ("Alpha Display", 220d, 48d),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var common = (Id: $"face-component-{Guid.NewGuid():N}", Name: name,
            X: x, Y: y, Width: width.GetValueOrDefault(defaultWidth), Height: height.GetValueOrDefault(defaultHeight));
        return kind switch
        {
            FaceComponentKind.Reel => new FaceReelDisplayElement { ObjectId=common.Id, Name=common.Name, X=common.X, Y=common.Y, Width=common.Width, Height=common.Height, Stops=1 },
            FaceComponentKind.Button => new FaceButtonElement { ObjectId=common.Id, Name=common.Name, X=common.X, Y=common.Y, Width=common.Width, Height=common.Height },
            FaceComponentKind.SevenSegmentDisplay => new FaceSevenSegmentDisplayElement { ObjectId=common.Id, Name=common.Name, X=common.X, Y=common.Y, Width=common.Width, Height=common.Height },
            FaceComponentKind.AlphaDisplay => new FaceAlphaDisplayElement { ObjectId=common.Id, Name=common.Name, X=common.X, Y=common.Y, Width=common.Width, Height=common.Height },
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }
}
