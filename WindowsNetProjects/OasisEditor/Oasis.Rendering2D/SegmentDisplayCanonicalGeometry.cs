namespace OasisEditor.SegmentDisplays;

internal static class SevenSegmentBitMapping
{
    public const int A = 0;
    public const int B = 1;
    public const int C = 2;
    public const int D = 3;
    public const int E = 4;
    public const int F = 5;
    public const int G = 6;
    public const int DecimalPoint = 7;
    public const int SegmentCount = 7;
    public const int MaskWidth = 8;
}

internal sealed record NormalizedPoint(double X, double Y);
internal sealed record SegmentShapeDefinition(int SegmentIndex, string SegmentName, IReadOnlyList<NormalizedPoint> Polygon);
internal sealed record SegmentDisplayGeometryDefinition(string Topology, IReadOnlyList<SegmentShapeDefinition> Segments, SegmentShapeDefinition? DecimalPoint);

internal static class SevenSegmentCanonicalGeometry
{
    public const string Topology = "sevenSegment";

    public static SegmentDisplayGeometryDefinition Definition { get; } = new(
        Topology,
        new[]
        {
            Segment(0, "A", (0.22, 0.00), (0.88, 0.00), (0.93, 0.04), (0.82, 0.11), (0.37, 0.11), (0.29, 0.05)),
            Segment(1, "B", (0.95, 0.07), (1.00, 0.11), (0.91, 0.48), (0.78, 0.42), (0.85, 0.14)),
            Segment(2, "C", (0.74, 0.59), (0.68, 0.86), (0.76, 0.93), (0.82, 0.88), (0.91, 0.50)),
            Segment(3, "D", (0.08, 0.95), (0.13, 0.99), (0.65, 0.99), (0.71, 0.94), (0.64, 0.87), (0.18, 0.87)),
            Segment(4, "E", (0.09, 0.51), (0.00, 0.88), (0.05, 0.91), (0.15, 0.84), (0.23, 0.57)),
            Segment(5, "F", (0.19, 0.11), (0.09, 0.48), (0.26, 0.41), (0.32, 0.11), (0.26, 0.05)),
            Segment(6, "G", (0.26, 0.45), (0.15, 0.49), (0.27, 0.55), (0.74, 0.55), (0.84, 0.50), (0.73, 0.45))
        },
        Segment(7, "DP", (0.84, 0.88), (0.98, 0.88), (0.98, 1.00), (0.84, 1.00)));

    private static SegmentShapeDefinition Segment(int index, string name, params (double X, double Y)[] points)
    {
        return new SegmentShapeDefinition(index, name, points.Select(point => new NormalizedPoint(point.X, point.Y)).ToArray());
    }
}
