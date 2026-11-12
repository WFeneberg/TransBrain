using Microsoft.EntityFrameworkCore;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Orders;

namespace TransBrain.Infrastructure.Persistence.OrderNumbering;

/// <summary>
/// Hands out order numbers using an atomic database increment.
/// </summary>
/// <remarks>
/// Deliberately NOT "SELECT MAX(sequence) + 1": two concurrent creates read the same maximum and
/// produce the same number, and the unique index then rejects one of them, so a user's order
/// fails for a reason unrelated to their input. The upsert below increments inside a single
/// statement, taking a row lock for its duration, so concurrent callers serialise and receive
/// different numbers. OrderNumberGeneratorTests proves this with twenty concurrent callers.
/// </remarks>
internal sealed class SequentialOrderNumberGenerator(
    TransBrainDbContext context,
    ITenantContext tenantContext) : IOrderNumberGenerator
{
    public async Task<OrderNumber> NextAsync(int year, CancellationToken cancellationToken)
    {
        // Each tenant owns an independent sequence per year: order numbers are a tenant's
        // business numbering, and one tenant's volume must not create gaps in another's.
        Guid tenantId = tenantContext.TenantId;

        // Two details here are load-bearing rather than stylistic:
        //
        // The `AS "Value"` alias, because Database.SqlQuery<T> for a scalar type binds a single
        // column named exactly "Value"; returning last_number unaliased fails at runtime.
        //
        // ToListAsync followed by Single, rather than SingleAsync: an INSERT ... RETURNING is
        // non-composable SQL, and SingleAsync composes a SELECT over it, which throws
        // "'FromSql' or 'SqlQuery' was called with non-composable SQL". Enumerating with no
        // operator on top runs the statement as the root query, which is what we want.
        List<int> sequences = await context.Database
            .SqlQuery<int>($"""
                INSERT INTO order_number_sequences (tenant_id, year, last_number)
                VALUES ({tenantId}, {year}, 1)
                ON CONFLICT (tenant_id, year) DO UPDATE
                    SET last_number = order_number_sequences.last_number + 1
                RETURNING last_number AS "Value"
                """)
            .ToListAsync(cancellationToken);

        return OrderNumber.From(year, sequences.Single());
    }
}
