namespace TransBrain.Domain.Imports;

/// <summary>Lifecycle of an import session.</summary>
public enum ImportSessionStatus
{
    /// <summary>The source was analyzed; its tables await mapping and execution.</summary>
    Ready,

    /// <summary>An execution ran and every row was imported.</summary>
    Completed,

    /// <summary>An execution ran; some rows were imported, some failed.</summary>
    CompletedWithErrors,

    /// <summary>An execution ran and no row could be imported.</summary>
    Failed
}
