using System.Numerics;

namespace OasisEditor.Avalonia.Rendering3D;

public sealed record SceneCameraState(Vector3 Target, float Distance, float Yaw, float Pitch, float FieldOfView = 45)
{
    public static SceneCameraState Default => new(Vector3.Zero, 4, .65f, .3f);
    public void Validate()
    {
        if (!float.IsFinite(Target.X) || !float.IsFinite(Target.Y) || !float.IsFinite(Target.Z)
            || !float.IsFinite(Distance) || Distance is < .0001f or > 1000000
            || !float.IsFinite(Yaw) || !float.IsFinite(Pitch) || Math.Abs(Pitch) > 1.56f
            || !float.IsFinite(FieldOfView) || FieldOfView is < 5 or > 120)
            throw new ArgumentException("Invalid 3D camera state.");
    }
    public Vector3 Eye => Target + Distance * new Vector3(MathF.Sin(Yaw) * MathF.Cos(Pitch), MathF.Sin(Pitch), MathF.Cos(Yaw) * MathF.Cos(Pitch));
    public Matrix4x4 View => Matrix4x4.CreateLookAt(Eye, Target, Vector3.UnitY);
    public Matrix4x4 Projection(float aspect)
    {
        var near = Math.Max(.00001f, Distance / 10000);
        var far = Math.Max(100, Distance * 100);
        var y = 1 / MathF.Tan(FieldOfView * MathF.PI / 360);
        return new(y / Math.Max(.01f, aspect), 0, 0, 0, 0, y, 0, 0,
            0, 0, (far + near) / (near - far), -1, 0, 0, 2 * far * near / (near - far), 0);
    }
    public static SceneCameraState Frame(Vector3 minimum, Vector3 maximum)
    {
        var radius = Math.Max(.01f, (maximum - minimum).Length() * .5f);
        return new((minimum + maximum) * .5f, radius / MathF.Sin(MathF.PI / 8) * 1.15f, .65f, .3f);
    }
    public SceneCameraState Orbit(float deltaX, float deltaY) => this with { Yaw = MathF.IEEERemainder(Yaw + deltaX, MathF.Tau), Pitch = Math.Clamp(Pitch + deltaY, -1.55f, 1.55f) };
    public SceneCameraState Zoom(float steps) => this with { Distance = Math.Clamp(Distance * MathF.Pow(.86f, steps), .0001f, 1000000) };
    public SceneCameraState Pan(float dx, float dy)
    {
        var right = Vector3.Normalize(Vector3.Cross(Target - Eye, Vector3.UnitY));
        var up = Vector3.Normalize(Vector3.Cross(right, Target - Eye));
        return this with { Target = Target + (right * dx + up * dy) * Distance };
    }
    public (Vector3 Origin, Vector3 Direction) Ray(float x, float y, float aspect)
    {
        if (!Matrix4x4.Invert(View * Projection(aspect), out var inverse)) throw new InvalidOperationException("Camera matrix is singular.");
        var near = Vector4.Transform(new Vector4(x, y, -1, 1), inverse);
        var far = Vector4.Transform(new Vector4(x, y, 1, 1), inverse);
        var a = new Vector3(near.X, near.Y, near.Z) / near.W;
        var b = new Vector3(far.X, far.Y, far.Z) / far.W;
        return (a, Vector3.Normalize(b - a));
    }
}
