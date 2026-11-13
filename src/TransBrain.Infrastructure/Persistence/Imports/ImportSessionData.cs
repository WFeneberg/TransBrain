namespace TransBrain.Infrastructure.Persistence.Imports;

/// <summary>
/// The normalized table data of an import session, as one gzipped JSON blob. Infrastructure-only
/// on purpose: the Domain aggregate records what happened, the payload is just bytes it never
/// inspects. Kept in its own row so session lists never drag megabytes out of the database.
/// </summary>
internal sealed class ImportSessionData
{
    public Guid ImportSessionId { get; set; }

    public byte[] Payload { get; set; } = [];
}
