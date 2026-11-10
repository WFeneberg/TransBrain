using TransBrain.Domain.Common;

namespace TransBrain.Domain.Vehicles;

/// <summary>
/// One entry of a maintainable vehicle selection list. Vehicles store the <see cref="Code"/>, so the
/// code is fixed once created; the display <see cref="Name"/> can be changed at any time. An entry that
/// should no longer be chosen is deactivated rather than deleted, so vehicles using it stay valid.
/// </summary>
public sealed class VehicleCatalogEntry
{
    public const int MaxCodeLength = 20;
    public const int MaxNameLength = 100;

    // EF Core materialization only. Every other construction goes through Create.
    private VehicleCatalogEntry()
    {
        Code = null!;
        Name = null!;
    }

    private VehicleCatalogEntry(Guid id, VehicleCatalogKind kind, string code, string name, bool isActive)
    {
        Id = id;
        Kind = kind;
        Code = code;
        Name = name;
        IsActive = isActive;
    }

    public Guid Id { get; private set; }

    public VehicleCatalogKind Kind { get; private set; }

    /// <summary>Stable key stored on vehicles. Letters, digits, <c>-</c> and <c>_</c> only.</summary>
    public string Code { get; private set; }

    public string Name { get; private set; }

    public bool IsActive { get; private set; }

    public static Result<VehicleCatalogEntry> Create(VehicleCatalogKind kind, string code, string name, bool isActive)
    {
        if (!Enum.IsDefined(kind))
        {
            return Error.Validation("VehicleCatalog.UnknownKind", $"'{kind}' is not a known vehicle catalog.");
        }

        string trimmedCode = code?.Trim() ?? string.Empty;
        if (trimmedCode.Length is 0 or > MaxCodeLength || !trimmedCode.All(IsCodeCharacter))
        {
            return Error.Validation(
                "VehicleCatalog.InvalidCode",
                $"Code must be 1 to {MaxCodeLength} characters long and contain only letters, digits, '-' and '_'.");
        }

        Error? nameError = ValidateName(name);
        if (nameError is not null)
        {
            return nameError;
        }

        return new VehicleCatalogEntry(Guid.CreateVersion7(), kind, trimmedCode, name.Trim(), isActive);
    }

    public Result<VehicleCatalogEntry> Update(string name, bool isActive)
    {
        Error? nameError = ValidateName(name);
        if (nameError is not null)
        {
            return nameError;
        }

        Name = name.Trim();
        IsActive = isActive;
        return this;
    }

    public static bool IsCodeCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '-' or '_';

    private static Error? ValidateName(string? name)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        return trimmed.Length is 0 or > MaxNameLength
            ? Error.Validation("VehicleCatalog.InvalidName", $"Name must be 1 to {MaxNameLength} characters long.")
            : null;
    }
}
