namespace TransBrain.Api.Authorization;

public static class Policies
{
    public const string MasterDataWrite = "MasterDataWrite";
    public const string DispatchWrite = "DispatchWrite";
    public const string TourStatusWrite = "TourStatusWrite";
    public const string Read = "Read";

    /// <summary>
    /// The global maintenance area: backup/restore, import and export of ANY tenant's data.
    /// Cross-tenant by nature, so it is strictly the system administrator's business — a
    /// tenant admin has no access here.
    /// </summary>
    public const string SystemMaintenance = "SystemMaintenance";
}
