using TransBrain.Domain.Common;

namespace TransBrain.Domain.Drivers;

public sealed class Driver
{
    private readonly HashSet<LicenseClass> _licenseClasses = [];

    // EF Core materialization only. Every other construction goes through Create.
    private Driver()
    {
        FirstName = null!;
        LastName = null!;
    }

    private Driver(
        Guid id,
        string firstName,
        string lastName,
        IEnumerable<LicenseClass> licenseClasses,
        DateOnly licenseValidUntil,
        string? externalUserId)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        _licenseClasses = [.. licenseClasses];
        LicenseValidUntil = licenseValidUntil;
        ExternalUserId = externalUserId;
        Status = DriverStatus.Available;
    }

    public Guid Id { get; private set; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public IReadOnlyCollection<LicenseClass> LicenseClasses => _licenseClasses;

    public DateOnly LicenseValidUntil { get; private set; }

    public DriverStatus Status { get; private set; }

    /// <summary>Keycloak's <c>sub</c> claim, set when the driver has a login.</summary>
    public string? ExternalUserId { get; private set; }

    public static Result<Driver> Create(
        string firstName,
        string lastName,
        IReadOnlyCollection<LicenseClass> licenseClasses,
        DateOnly licenseValidUntil,
        string? externalUserId)
    {
        Result<Unit> validation = Validate(firstName, lastName, licenseClasses);
        if (!validation.IsSuccess)
        {
            return validation.Error!;
        }

        return new Driver(
            Guid.CreateVersion7(),
            firstName.Trim(),
            lastName.Trim(),
            licenseClasses,
            licenseValidUntil,
            NormalizeExternalUserId(externalUserId));
    }

    public Result<Driver> Update(
        string firstName,
        string lastName,
        IReadOnlyCollection<LicenseClass> licenseClasses,
        DateOnly licenseValidUntil,
        string? externalUserId)
    {
        Result<Unit> validation = Validate(firstName, lastName, licenseClasses);
        if (!validation.IsSuccess)
        {
            return validation.Error!;
        }

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        _licenseClasses.Clear();
        foreach (LicenseClass licenseClass in licenseClasses)
        {
            _licenseClasses.Add(licenseClass);
        }

        LicenseValidUntil = licenseValidUntil;
        ExternalUserId = NormalizeExternalUserId(externalUserId);

        return this;
    }

    public void MarkAbsent()
    {
        if (Status == DriverStatus.Available)
        {
            Status = DriverStatus.Absent;
        }
    }

    /// <remarks>
    /// Deliberately refuses to revive an inactive driver: deactivation is an administrative
    /// decision, and an availability toggle must not silently undo it.
    /// </remarks>
    public void MarkAvailable()
    {
        if (Status == DriverStatus.Absent)
        {
            Status = DriverStatus.Available;
        }
    }

    public void Deactivate() => Status = DriverStatus.Inactive;

    public bool CanDriveOn(DateOnly date) =>
        Status == DriverStatus.Available && LicenseValidUntil >= date;

    private static Result<Unit> Validate(
        string firstName,
        string lastName,
        IReadOnlyCollection<LicenseClass> licenseClasses)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            return Error.Validation("Driver.FirstNameRequired", "First name must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            return Error.Validation("Driver.LastNameRequired", "Last name must not be empty.");
        }

        if (licenseClasses.Count == 0)
        {
            return Error.Validation("Driver.LicenseClassRequired", "At least one licence class is required.");
        }

        return Unit.Value;
    }

    private static string? NormalizeExternalUserId(string? externalUserId) =>
        string.IsNullOrWhiteSpace(externalUserId) ? null : externalUserId.Trim();
}
