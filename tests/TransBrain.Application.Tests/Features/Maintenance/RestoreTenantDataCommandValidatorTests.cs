using AwesomeAssertions;
using FluentValidation.Results;
using TransBrain.Application.Abstractions;
using TransBrain.Application.Features.Maintenance.RestoreTenantData;
using TransBrain.Application.Tests.Fakes;

namespace TransBrain.Application.Tests.Features.Maintenance;

public class RestoreTenantDataCommandValidatorTests
{
    private readonly RestoreTenantDataCommandValidator _validator = new();

    [Fact]
    public void Validate_WellFormedBackup_Passes()
    {
        TenantBackupDocument backup = FakeTenantDataMaintenance.Document(Guid.CreateVersion7());

        ValidationResult result = _validator.Validate(new RestoreTenantDataCommand(backup));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ForeignFileFormat_Fails()
    {
        TenantBackupDocument backup = FakeTenantDataMaintenance.Document(
            Guid.CreateVersion7(), fileFormat: "some-other-export");

        ValidationResult result = _validator.Validate(new RestoreTenantDataCommand(backup));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Backup.FileFormat");
    }

    [Fact]
    public void Validate_EmptyTenantId_Fails()
    {
        TenantBackupDocument backup = FakeTenantDataMaintenance.Document(Guid.Empty);

        ValidationResult result = _validator.Validate(new RestoreTenantDataCommand(backup));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Backup.TenantId");
    }

    [Fact]
    public void Validate_EmptySchemaVersion_Fails()
    {
        TenantBackupDocument backup = FakeTenantDataMaintenance.Document(
            Guid.CreateVersion7(), schemaVersion: "");

        ValidationResult result = _validator.Validate(new RestoreTenantDataCommand(backup));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Backup.SchemaVersion");
    }

    [Fact]
    public void Validate_MissingBackup_Fails()
    {
        ValidationResult result = _validator.Validate(new RestoreTenantDataCommand(null!));

        result.IsValid.Should().BeFalse();
    }
}
