using Microsoft.EntityFrameworkCore;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Billing;

namespace TransBrain.Infrastructure.Persistence.InvoiceNumbering;

/// <summary>
/// Hands out invoice numbers using an atomic database increment, exactly like
/// <c>SequentialOrderNumberGenerator</c> and for the same reason: two concurrent creates must
/// serialise on the sequence row and receive different numbers, because a duplicate invoice
/// number is a bookkeeping defect, not just a failed insert.
/// </summary>
internal sealed class SequentialInvoiceNumberGenerator(TransBrainDbContext context) : IInvoiceNumberGenerator
{
    public async Task<InvoiceNumber> NextAsync(int year, CancellationToken cancellationToken)
    {
        // See SequentialOrderNumberGenerator for why the alias and the ToListAsync are load-bearing.
        List<int> sequences = await context.Database
            .SqlQuery<int>($"""
                INSERT INTO invoice_number_sequences (year, last_number)
                VALUES ({year}, 1)
                ON CONFLICT (year) DO UPDATE
                    SET last_number = invoice_number_sequences.last_number + 1
                RETURNING last_number AS "Value"
                """)
            .ToListAsync(cancellationToken);

        return InvoiceNumber.From(year, sequences.Single());
    }
}
