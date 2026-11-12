using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TransBrain.Application.Abstractions;

namespace TransBrain.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the model without booting the Api host: the
/// context now takes a scoped <see cref="ITenantContext"/>, which the design-time root provider
/// cannot resolve. Scaffolding only needs the model, never a database or a tenant, so a
/// connection-string-less Npgsql context with a throwing tenant stub is exactly enough.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TransBrainDbContext>
{
    public TransBrainDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<TransBrainDbContext> options = new();
        options.UseNpgsql();
        return new TransBrainDbContext(options.Options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid TenantId =>
            throw new InvalidOperationException("The design-time context has no tenant; nothing may query tenant data.");
    }
}
