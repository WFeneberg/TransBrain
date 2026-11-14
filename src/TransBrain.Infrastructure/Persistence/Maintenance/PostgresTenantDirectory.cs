using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Customers;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Tours;
using TransBrain.Domain.Vehicles;
using TransBrain.Infrastructure.Persistence.Tenancy;

namespace TransBrain.Infrastructure.Persistence.Maintenance;

/// <summary>
/// Lists the tenants that own rows in this database, with headline counts, by scanning the same
/// table set <see cref="PostgresTenantDataMaintenance"/> backs up: tables carrying
/// <c>tenant_id</c> are grouped on it directly, owned tables (tour stops, invoice lines, ...)
/// reach their tenant through the ownership foreign key, and the two raw number-sequence tables
/// are added by hand. One UNION ALL round trip, aggregated in memory.
/// </summary>
internal sealed class PostgresTenantDirectory(TransBrainDbContext context) : ITenantDirectory
{
    private static readonly string[] SequenceTables = ["order_number_sequences", "invoice_number_sequences"];

    public async Task<IReadOnlyList<TenantSummary>> ListAsync(CancellationToken cancellationToken)
    {
        string sql = BuildSql();

        // Retry-on-failure (enabled by Aspire) wants the whole read inside the strategy; on a
        // transient failure the lambda reruns from scratch.
        IExecutionStrategy strategy = context.Database.CreateExecutionStrategy();
        Dictionary<Guid, Dictionary<string, int>> byTenant = await strategy.ExecuteAsync(async () =>
        {
            Dictionary<Guid, Dictionary<string, int>> result = [];

            await context.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                await using DbCommand command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = sql;

                await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    string table = reader.GetString(0);
                    Guid tenantId = reader.GetGuid(1);
                    int rows = reader.GetInt32(2);

                    if (!result.TryGetValue(tenantId, out Dictionary<string, int>? tables))
                    {
                        result[tenantId] = tables = new Dictionary<string, int>(StringComparer.Ordinal);
                    }

                    tables[table] = tables.GetValueOrDefault(table) + rows;
                }
            }
            finally
            {
                await context.Database.CloseConnectionAsync();
            }

            return result;
        });

        string customers = TableOf<Customer>();
        string drivers = TableOf<Driver>();
        string vehicles = TableOf<Vehicle>();
        string orders = TableOf<TransportOrder>();
        string tours = TableOf<Tour>();
        string invoices = TableOf<Invoice>();

        return [.. byTenant
            .Select(entry => new TenantSummary(
                entry.Key,
                entry.Value.GetValueOrDefault(customers),
                entry.Value.GetValueOrDefault(drivers),
                entry.Value.GetValueOrDefault(vehicles),
                entry.Value.GetValueOrDefault(orders),
                entry.Value.GetValueOrDefault(tours),
                entry.Value.GetValueOrDefault(invoices),
                entry.Value.Values.Sum()))
            .OrderBy(summary => summary.TenantId)];
    }

    /// <summary>
    /// One <c>SELECT table_name, tenant_id, count(*)</c> per tenant-owned table, UNION ALLed.
    /// Every identifier comes from the EF relational model or a constant here — never from
    /// input — and is quoted anyway.
    /// </summary>
    private string BuildSql()
    {
        StringBuilder sql = new();
        string tenantColumn = Quote(TenantEntity.ColumnName);

        foreach (ITable table in context.Model.GetRelationalModel().Tables)
        {
            AppendUnion(sql);
            if (table.Columns.Any(c => c.Name == TenantEntity.ColumnName))
            {
                sql.Append($"SELECT {Literal(table.Name)}, t.{tenantColumn}, count(*)::int "
                           + $"FROM {Quote(table.Name)} t GROUP BY t.{tenantColumn}");
                continue;
            }

            // Owned table: scoped through its ownership FK, like the backup does.
            IForeignKeyConstraint ownership = table.ForeignKeyConstraints
                    .FirstOrDefault(fk => fk.MappedForeignKeys.Any(mapped => mapped.IsOwnership)
                                          && fk.PrincipalTable.Columns.Any(c => c.Name == TenantEntity.ColumnName))
                ?? throw new InvalidOperationException(
                    $"Table '{table.Name}' carries neither a '{TenantEntity.ColumnName}' column nor an "
                    + "ownership foreign key to a tenant-owned table; the tenant directory cannot count it.");

            string joinOn = string.Join(" AND ", ownership.Columns.Zip(
                ownership.PrincipalColumns,
                (child, principal) => $"t.{Quote(child.Name)} = o.{Quote(principal.Name)}"));
            sql.Append($"SELECT {Literal(table.Name)}, o.{tenantColumn}, count(*)::int "
                       + $"FROM {Quote(table.Name)} t JOIN {Quote(ownership.PrincipalTable.Name)} o "
                       + $"ON {joinOn} GROUP BY o.{tenantColumn}");
        }

        foreach (string table in SequenceTables)
        {
            AppendUnion(sql);
            sql.Append($"SELECT {Literal(table)}, t.{tenantColumn}, count(*)::int "
                       + $"FROM {Quote(table)} t GROUP BY t.{tenantColumn}");
        }

        return sql.ToString();
    }

    private static void AppendUnion(StringBuilder sql)
    {
        if (sql.Length > 0)
        {
            sql.Append("\nUNION ALL\n");
        }
    }

    private string TableOf<TEntity>()
        => context.Model.FindEntityType(typeof(TEntity))?.GetTableName()
           ?? throw new InvalidOperationException($"Entity '{typeof(TEntity).Name}' is not mapped to a table.");

    private static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";

    private static string Literal(string value) => $"'{value.Replace("'", "''")}'";
}
