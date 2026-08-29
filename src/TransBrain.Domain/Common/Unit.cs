namespace TransBrain.Domain.Common;

/// <summary>A result that carries no value. Used where an operation can only succeed or fail.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value = default;
}
