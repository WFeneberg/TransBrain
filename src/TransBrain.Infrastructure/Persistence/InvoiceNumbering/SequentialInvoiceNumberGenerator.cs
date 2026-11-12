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
internal sealed class SequentialInvoiceNumberGenerator(
    TransBrainDbContext context,
    ITenantContext tenantContext) : IInvoiceNumberGenerator
{
    public async Task<InvoiceNumber> NextAsync(int year, CancellationToken cancellationToken)
    {
        // Each tenant owns an independent sequence per year; invoice numbering is a legal,
        // per-company (per-tenant) concern, so sequences must never interleave across tenants.
        Guid tenantId = tenantContext.TenantId;

        // See SequentialOrderNumberGenerator for why the alias and the ToListAsync are load-bearing.
        List<int> sequences = await context.Database
            .SqlQuery<int>($"""
                INSERT INTO invoice_number_sequences (tenant_id, year, last_number)
                VALUES ({tenantId}, {year}, 1)
                ON CONFLICT (tenant_id, year) DO UPDATE
                    SET last_number = invoice_number_sequences.last_number + 1
                RETURNING last_number AS "Value"
                """)
            .ToListAsync(cancellationToken);

        return InvoiceNumber.From(year, sequences.Single());
    }
}
