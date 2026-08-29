using TransBrain.Application.Abstractions;

namespace TransBrain.Application.Tests.Fakes;

public sealed class StubCurrentUser(string? userId, params string[] roles) : ICurrentUser
{
    public string? UserId { get; } = userId;

    public bool IsInRole(string role) => roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    public static StubCurrentUser Dispatcher() => new("dispatcher-sub", "disponent");

    public static StubCurrentUser Admin() => new("admin-sub", "admin");

    public static StubCurrentUser Viewer() => new("viewer-sub", "viewer");

    public static StubCurrentUser DriverWith(string externalUserId) => new(externalUserId, "fahrer");
}
