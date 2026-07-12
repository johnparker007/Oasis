namespace OasisEditor;

internal static class FaceElementModelComparer
{
    public static bool AreEquivalent(FaceElementModel left, FaceElementModel right)
    {
        return left.GetType() == right.GetType()
               && left.ObjectId == right.ObjectId
               && left.Name == right.Name
               && left.X == right.X
               && left.Y == right.Y
               && left.Width == right.Width
               && left.Height == right.Height
               && left.IsVisible == right.IsVisible
               && left.IsTransformLocked == right.IsTransformLocked
               && Equals(left.LinkedMachineObjectReference, right.LinkedMachineObjectReference)
               && left.LinkedPanel2DElementId == right.LinkedPanel2DElementId;
    }
}
