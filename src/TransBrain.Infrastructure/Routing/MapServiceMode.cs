namespace TransBrain.Infrastructure.Routing;

/// <summary>Where a map service call goes: the self-hosted container or the public service.</summary>
public enum MapServiceMode
{
    /// <summary>The container the AppHost starts: no external call, data limited to the local extract.</summary>
    SelfHosted,

    /// <summary>The public service: worldwide data, external calls under the provider's usage limits.</summary>
    Online
}
