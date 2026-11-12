using AwesomeAssertions;
using FluentValidation.Results;
using TransBrain.Application.Features.Invoices.ListInvoices;

namespace TransBrain.Application.Tests.Features.Invoices;

public class ListInvoicesQueryValidatorTests
{
    private readonly ListInvoicesQueryValidator _validator = new();

    [Fact]
    public void Validate_DefaultPaging_Passes()
    {
        ValidationResult result = _validator.Validate(new ListInvoicesQuery(1, 20));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(10_001, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Validate_PagingOutOfRange_Fails(int page, int pageSize)
    {
        ValidationResult result = _validator.Validate(new ListInvoicesQuery(page, pageSize));

        result.IsValid.Should().BeFalse();
    }
}
