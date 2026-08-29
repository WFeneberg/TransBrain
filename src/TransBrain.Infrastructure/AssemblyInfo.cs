using System.Runtime.CompilerServices;

// RedisCacheService is internal by design (it is an implementation detail behind ICacheService,
// resolved only through DI in production). RedisCacheServiceTests constructs it directly against
// a real, Testcontainers-backed Redis instance, so it needs to see internal types here the same
// way TransBrain.Application.Tests already does for TransBrain.Application.
[assembly: InternalsVisibleTo("TransBrain.Api.IntegrationTests")]
