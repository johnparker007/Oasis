using System.Collections.Generic;

namespace OasisEditor;

public enum InspectorAggregateValueState
{
    Unavailable,
    Common,
    Mixed
}

public readonly record struct InspectorAggregateValue<T>(InspectorAggregateValueState State, T? Value)
{
    public bool IsAvailable => State != InspectorAggregateValueState.Unavailable;
    public bool IsCommon => State == InspectorAggregateValueState.Common;
    public bool IsMixed => State == InspectorAggregateValueState.Mixed;

    public static InspectorAggregateValue<T> Unavailable() => new(InspectorAggregateValueState.Unavailable, default);
    public static InspectorAggregateValue<T> Common(T? value) => new(InspectorAggregateValueState.Common, value);
    public static InspectorAggregateValue<T> Mixed() => new(InspectorAggregateValueState.Mixed, default);
}

public static class InspectorAggregateValue
{
    public static InspectorAggregateValue<T> From<T>(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        using var enumerator = values.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return InspectorAggregateValue<T>.Unavailable();
        }

        var first = enumerator.Current;
        var comparer = EqualityComparer<T>.Default;
        while (enumerator.MoveNext())
        {
            if (!comparer.Equals(first, enumerator.Current))
            {
                return InspectorAggregateValue<T>.Mixed();
            }
        }

        return InspectorAggregateValue<T>.Common(first);
    }
}
