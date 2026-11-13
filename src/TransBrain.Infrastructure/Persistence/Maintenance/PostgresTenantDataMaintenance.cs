using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using TransBrain.Application.Abstractions;
using TransBrain.Infrastructure.Persistence.Tenancy;

namespace TransBrain.Infrastructure.Persistence.Maintenance;

/// <summary>
/// Copies one tenant's rows out of and back into PostgreSQL, driven by the EF relational model
/// so a new entity or column is backed up without anyone remembering this class exists.
/// </summary>
/// <remarks>
/// Deliberately not written against the aggregates: a backup must be a faithful byte-for-byte
/// copy, and round-tripping rows through entity constructors and value converters would make the
/// copy only as faithful as that mapping code. Instead PostgreSQL itself converts each row to
/// JSON (<c>to_jsonb</c>) and back (<c>jsonb_populate_recordset</c>), which preserves every
/// column of every type without a single CLR type mapping here.
///
/// Scoping: tables carrying the <c>tenant_id</c> column are filtered on it directly; tables of
/// owned collections (tour stops, invoice lines, ...) carry no tenant column and are reached
/// through their ownership foreign key instead. A model table with neither is refused loudly,
/// because silently skipping it would mean quietly incomplete backups. The two raw
/// number-sequence tables (<c>order_number_sequences</c>, <c>invoice_number_sequences</c>) have
/// no EF entity at all — see their migrations — and are added by hand so restored tenants do not
/// hand out duplicate order or invoice numbers.
///
/// An uploaded file is hostile input even though only admins may send one: every row lands with
/// the caller's tenant stamped over whatever the file claimed, and after the inserts the
/// transaction verifies no row references another tenant's data before anything commits.
/// </remarks>
internal sealed class PostgresTenantDataMaintenance(
    TransBrainDbContext context,
    ITenantContext tenantContext,
    TimeProvider clock) : ITenantDataMaintenance
{
    /// <summary>Raw tables without an EF entity; columns listed here because no model knows them.</summary>
    private static readonly (string Name, string[] Columns, string[] KeyColumns)[] SequenceTables =
    [
        ("order_number_sequences", ["year", "last_number"], ["year"]),
        ("invoice_number_sequences", ["year", "last_number"], ["year"])
    ];

    public async Task<TenantBackupDocument> ExportAsync(CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.TenantId;
        IReadOnlyList<TablePlan> plans = BuildPlans();
        string schemaVersion = await GetSchemaVersionAsync(cancellationToken);

        // The execution strategy wrapper is mandatory: Aspire enables retry-on-failure, which
        // refuses user-initiated transactions outside ExecuteAsync. On a retry the whole lambda
        // runs again from scratch, so all state is created inside it.
        IExecutionStrategy strategy = context.Database.CreateExecutionStrategy();
        Dictionary<string, JsonElement> tables = await strategy.ExecuteAsync(async () =>
        {
            Dictionary<string, JsonElement> result = [];

            // RepeatableRead gives all per-table SELECTs one snapshot: a backup taken while a
            // dispatcher saves must be a moment in time, not a mix of before and after.
            await using IDbContextTransaction transaction = await context.Database
                .BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);

            foreach (TablePlan plan in plans)
            {
                string json = (string)(await ScalarAsync(ExportSql(plan), tenantId, cancellationToken))!;
                using JsonDocument document = JsonDocument.Parse(json);
                result[plan.Name] = document.RootElement.Clone();
            }

            await transaction.CommitAsync(cancellationToken);
            return result;
        });

        return new TenantBackupDocument(
            TenantBackupDocument.Format,
            TenantBackupDocument.Version,
            schemaVersion,
            tenantId,
            clock.GetUtcNow(),
            tables);
    }

    public async Task<IReadOnlyDictionary<string, int>> ImportAsync(
        TenantBackupDocument backup, CancellationToken cancellationToken)
    {
        Guid tenantId = tenantContext.TenantId;
        IReadOnlyList<TablePlan> plans = BuildPlans();

        HashSet<string> knownTables = plans.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        foreach ((string name, JsonElement rows) in backup.Tables)
        {
            if (!knownTables.Contains(name))
            {
                throw new TenantBackupDataException($"The backup contains rows for the unknown table '{name}'.");
            }

            if (rows.ValueKind != JsonValueKind.Array)
            {
                throw new TenantBackupDataException($"Table '{name}' in the backup is not a JSON array of rows.");
            }
        }

        try
        {
            IExecutionStrategy strategy = context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                Dictionary<string, int> restored = [];

                await using IDbContextTransaction transaction =
                    await context.Database.BeginTransactionAsync(cancellationToken);

                // Delete dependents before their principals (reverse insert order), then insert
                // principals before dependents, so no foreign key is ever momentarily broken.
                for (int i = plans.Count - 1; i >= 0; i--)
                {
                    await ExecuteAsync(DeleteSql(plans[i]), tenantId, rowsJson: null, cancellationToken);
                }

                foreach (TablePlan plan in plans)
                {
                    restored[plan.Name] =
                        backup.Tables.TryGetValue(plan.Name, out JsonElement rows) && rows.GetArrayLength() > 0
                            ? await ExecuteAsync(InsertSql(plan), tenantId, rows.GetRawText(), cancellationToken)
                            : 0;
                }

                await ValidateTenantIntegrityAsync(plans, restored, tenantId, cancellationToken);
                await ResyncIdentitySequencesAsync(plans, cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return (IReadOnlyDictionary<string, int>)restored;
            });
        }
        catch (PostgresException exception)
        {
            // Duplicate keys, broken foreign keys, unknown JSON fields, unparsable values: all
            // mean "this file cannot be restored", and the transaction has already rolled back.
            throw new TenantBackupDataException(
                $"The backup could not be restored: {exception.MessageText}", exception);
        }
    }

    public async Task<string> GetSchemaVersionAsync(CancellationToken cancellationToken)
    {
        IEnumerable<string> applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
        return applied.Order(StringComparer.Ordinal).LastOrDefault()
               ?? throw new InvalidOperationException(
                   "The database has no applied migrations; tenant backup needs a migrated database.");
    }

    /// <summary>
    /// One plan per physical table, sorted so principals precede dependents (insert order).
    /// </summary>
    private IReadOnlyList<TablePlan> BuildPlans()
    {
        List<TablePlan> plans = [];

        foreach (ITable table in context.Model.GetRelationalModel().Tables)
        {
            bool hasTenantColumn = HasTenantColumn(table);

            TableJoin? owner = null;
            if (!hasTenantColumn)
            {
                IForeignKeyConstraint ownership = table.ForeignKeyConstraints
                        .FirstOrDefault(fk => fk.MappedForeignKeys.Any(mapped => mapped.IsOwnership)
                                              && HasTenantColumn(fk.PrincipalTable))
                    ?? throw new InvalidOperationException(
                        $"Table '{table.Name}' carries neither a '{TenantEntity.ColumnName}' column nor an "
                        + "ownership foreign key to a tenant-owned table. Teach the tenant backup how to "
                        + "scope it (or exclude it explicitly) before shipping the entity.");
                owner = ToJoin(ownership);
            }

            // Physical foreign keys into other tenant-owned tables; checked after a restore so a
            // hand-crafted file cannot attach rows to another tenant's data. The ownership join
            // is excluded: rows reached through it are verified by count instead (see below).
            TableJoin[] foreignKeys = table.ForeignKeyConstraints
                .Where(fk => fk.PrincipalTable != table && HasTenantColumn(fk.PrincipalTable))
                .Select(ToJoin)
                .Where(join => owner is null || !join.Equals(owner))
                .ToArray();

            plans.Add(new TablePlan(
                table.Name,
                hasTenantColumn,
                [.. table.PrimaryKey!.Columns.Select(c => c.Name)],
                [.. table.Columns.Select(c => c.Name).Where(n => n != TenantEntity.ColumnName)],
                owner,
                foreignKeys));
        }

        foreach ((string name, string[] columns, string[] keyColumns) in SequenceTables)
        {
            plans.Add(new TablePlan(name, HasTenantColumn: true, keyColumns, columns, Owner: null, []));
        }

        return SortForInsert(plans);
    }

    private static bool HasTenantColumn(ITable table)
        => table.Columns.Any(c => c.Name == TenantEntity.ColumnName);

    private static TableJoin ToJoin(IForeignKeyConstraint constraint) => new(
        constraint.PrincipalTable.Name,
        [.. constraint.Columns.Select(c => c.Name)],
        [.. constraint.PrincipalColumns.Select(c => c.Name)]);

    /// <summary>
    /// Kahn's algorithm over the foreign keys between the planned tables. Ready tables are taken
    /// in name order so the sequence is deterministic. A cycle throws: it would need deferred
    /// constraints to restore, and that decision should be taken when the cycle is introduced.
    /// </summary>
    private static List<TablePlan> SortForInsert(List<TablePlan> plans)
    {
        Dictionary<string, TablePlan> byName = plans.ToDictionary(p => p.Name, StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> pending = plans.ToDictionary(
            p => p.Name,
            p => p.ForeignKeys.Select(fk => fk.PrincipalTable)
                .Concat(p.Owner is null ? [] : [p.Owner.PrincipalTable])
                .Where(principal => principal != p.Name && byName.ContainsKey(principal))
                .ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);

        List<TablePlan> sorted = [];
        while (pending.Count > 0)
        {
            string[] ready = [.. pending
                .Where(entry => entry.Value.Count == 0)
                .Select(entry => entry.Key)
                .Order(StringComparer.Ordinal)];

            if (ready.Length == 0)
            {
                throw new InvalidOperationException(
                    "The tenant tables form a foreign-key cycle; the tenant backup cannot order its inserts. "
                    + $"Involved tables: {string.Join(", ", pending.Keys.Order(StringComparer.Ordinal))}.");
            }

            foreach (string name in ready)
            {
                sorted.Add(byName[name]);
                pending.Remove(name);
            }

            foreach (HashSet<string> dependencies in pending.Values)
            {
                dependencies.ExceptWith(ready);
            }
        }

        return sorted;
    }

    // ---- SQL ----------------------------------------------------------------------------------
    // Identifiers are interpolated, values never are. Every identifier comes from the EF model or
    // this file's own constants — nothing from the uploaded document is ever treated as a name;
    // file table names are only MATCHED against the model's. Row data travels as a parameter.

    private string ExportSql(TablePlan plan)
    {
        string orderBy = string.Join(", ", plan.KeyColumns.Select(c => $"t.{Quote(c)}"));
        string source = plan.HasTenantColumn
            ? $"{Quote(plan.Name)} t WHERE t.{Quote(TenantEntity.ColumnName)} = @tenant"
            : $"{Quote(plan.Name)} t JOIN {Quote(plan.Owner!.PrincipalTable)} o ON {JoinOn(plan.Owner, "t", "o")} "
              + $"WHERE o.{Quote(TenantEntity.ColumnName)} = @tenant";

        return $"SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY {orderBy}), '[]'::jsonb)::text FROM {source}";
    }

    private string DeleteSql(TablePlan plan) => plan.HasTenantColumn
        ? $"DELETE FROM {Quote(plan.Name)} WHERE {Quote(TenantEntity.ColumnName)} = @tenant"
        : $"DELETE FROM {Quote(plan.Name)} t USING {Quote(plan.Owner!.PrincipalTable)} o "
          + $"WHERE {JoinOn(plan.Owner, "t", "o")} AND o.{Quote(TenantEntity.ColumnName)} = @tenant";

    private string InsertSql(TablePlan plan)
    {
        string columns = string.Join(", ", plan.DataColumns.Select(Quote));
        string values = string.Join(", ", plan.DataColumns.Select(c => $"r.{Quote(c)}"));
        string rows = $"jsonb_populate_recordset(NULL::{Quote(plan.Name)}, @rows::jsonb) r";

        // The tenant column is never taken from the file: whatever the uploaded rows claim, they
        // become the caller's rows or they fail the integrity checks.
        return plan.HasTenantColumn
            ? $"INSERT INTO {Quote(plan.Name)} ({columns}, {Quote(TenantEntity.ColumnName)}) "
              + $"SELECT {values}, @tenant FROM {rows}"
            : $"INSERT INTO {Quote(plan.Name)} ({columns}) SELECT {values} FROM {rows}";
    }

    private async Task ValidateTenantIntegrityAsync(
        IReadOnlyList<TablePlan> plans,
        IReadOnlyDictionary<string, int> restored,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        foreach (TablePlan plan in plans)
        {
            // Tables without a tenant column belong to whoever their owner row belongs to. The
            // tenant's old rows are gone, so scoping by owner must find exactly the inserted
            // rows; fewer means some row attached itself to ANOTHER tenant's owner.
            if (plan.Owner is not null)
            {
                string countSql =
                    $"SELECT count(*)::int FROM {Quote(plan.Name)} t "
                    + $"JOIN {Quote(plan.Owner.PrincipalTable)} o ON {JoinOn(plan.Owner, "t", "o")} "
                    + $"WHERE o.{Quote(TenantEntity.ColumnName)} = @tenant";
                int reachable = (int)(await ScalarAsync(countSql, tenantId, cancellationToken))!;
                if (reachable != restored[plan.Name])
                {
                    throw new TenantBackupDataException(
                        $"Table '{plan.Name}' contains rows that do not belong to this tenant's data.");
                }
            }

            foreach (TableJoin foreignKey in plan.ForeignKeys)
            {
                string scope = plan.HasTenantColumn
                    ? $"t.{Quote(TenantEntity.ColumnName)} = @tenant"
                    : $"o.{Quote(TenantEntity.ColumnName)} = @tenant";
                string ownerJoin = plan.Owner is null
                    ? string.Empty
                    : $"JOIN {Quote(plan.Owner.PrincipalTable)} o ON {JoinOn(plan.Owner, "t", "o")} ";
                string crossSql =
                    $"SELECT count(*)::int FROM {Quote(plan.Name)} t {ownerJoin}"
                    + $"JOIN {Quote(foreignKey.PrincipalTable)} p ON {JoinOn(foreignKey, "t", "p")} "
                    + $"WHERE {scope} AND p.{Quote(TenantEntity.ColumnName)} <> @tenant";

                if ((int)(await ScalarAsync(crossSql, tenantId, cancellationToken))! > 0)
                {
                    throw new TenantBackupDataException(
                        $"Table '{plan.Name}' references rows of another tenant in '{foreignKey.PrincipalTable}'.");
                }
            }
        }
    }

    /// <summary>
    /// After explicit inserts into identity columns (the synthesized keys of owned collections),
    /// PostgreSQL's backing sequences still stand where they stood, so the next EF insert — a new
    /// tour stop, say — would be handed an already-used key. Advance each one past its column's
    /// maximum. The maximum is taken across ALL tenants, because the sequences are shared.
    /// </summary>
    private async Task ResyncIdentitySequencesAsync(
        IReadOnlyList<TablePlan> plans, CancellationToken cancellationToken)
    {
        const string discoverSql = """
            SELECT c.table_name, c.column_name
            FROM information_schema.columns c
            WHERE c.table_schema = current_schema()
              AND c.table_name = ANY(@tables)
              AND (c.is_identity = 'YES' OR c.column_default LIKE 'nextval(%')
            """;

        List<(string Table, string Column)> identityColumns = [];
        await using (DbCommand discover = CreateCommand(discoverSql))
        {
            AddParameter(discover, "tables", plans.Select(p => p.Name).ToArray());
            await using DbDataReader reader = await discover.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                identityColumns.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        foreach ((string table, string column) in identityColumns)
        {
            string sql =
                $"SELECT setval(pg_get_serial_sequence(@table, @column), "
                + $"coalesce((SELECT max({Quote(column)}) FROM {Quote(table)}), 0) + 1, false)";
            await using DbCommand command = CreateCommand(sql);
            AddParameter(command, "table", Quote(table));
            AddParameter(command, "column", column);
            await command.ExecuteScalarAsync(cancellationToken);
        }
    }

    // ---- plumbing -----------------------------------------------------------------------------

    private async Task<int> ExecuteAsync(
        string sql, Guid tenantId, string? rowsJson, CancellationToken cancellationToken)
    {
        await using DbCommand command = CreateCommand(sql);
        AddParameter(command, "tenant", tenantId);
        if (rowsJson is not null)
        {
            AddParameter(command, "rows", rowsJson);
        }

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<object?> ScalarAsync(string sql, Guid tenantId, CancellationToken cancellationToken)
    {
        await using DbCommand command = CreateCommand(sql);
        AddParameter(command, "tenant", tenantId);
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private DbCommand CreateCommand(string sql)
    {
        DbCommand command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
        return command;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string JoinOn(TableJoin join, string childAlias, string principalAlias)
        => string.Join(" AND ", join.Columns.Zip(
            join.PrincipalColumns,
            (child, principal) => $"{childAlias}.{Quote(child)} = {principalAlias}.{Quote(principal)}"));

    private static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";

    /// <param name="Name">Physical table name.</param>
    /// <param name="HasTenantColumn">Whether the table carries <c>tenant_id</c> itself.</param>
    /// <param name="KeyColumns">Primary-key columns; exports order by them for determinism.</param>
    /// <param name="DataColumns">All columns except the tenant column.</param>
    /// <param name="Owner">Ownership join for tables scoped through their owner.</param>
    /// <param name="ForeignKeys">Joins into other tenant-owned tables, for integrity checks.</param>
    private sealed record TablePlan(
        string Name,
        bool HasTenantColumn,
        IReadOnlyList<string> KeyColumns,
        IReadOnlyList<string> DataColumns,
        TableJoin? Owner,
        IReadOnlyList<TableJoin> ForeignKeys);

    private sealed record TableJoin(string PrincipalTable, string[] Columns, string[] PrincipalColumns)
    {
        public bool Equals(TableJoin? other) => other is not null
            && PrincipalTable == other.PrincipalTable
            && Columns.SequenceEqual(other.Columns)
            && PrincipalColumns.SequenceEqual(other.PrincipalColumns);

        public override int GetHashCode() => PrincipalTable.GetHashCode();
    }
}
