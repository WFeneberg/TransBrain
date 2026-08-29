using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Orders;

namespace TransBrain.Api.IntegrationTests;

public class OrderNumberGeneratorTests(TransBrainApiFactory factory) : IClassFixture<TransBrainApiFactory>
{
    [Fact]
    public async Task NextAsync_TwentyConcurrentCallers_EveryNumberIsDistinct()
    {
        // A SELECT MAX(...) + 1 implementation fails this test: concurrent readers see the same
        // maximum and produce duplicates. The atomic upsert does not.
        const int callers = 20;

        Task<OrderNumber>[] tasks = Enumerable.Range(0, callers)
            .Select(_ => Task.Run(async () =>
            {
                using IServiceScope scope = factory.Services.CreateScope();
                IOrderNumberGenerator generator =
                    scope.ServiceProvider.GetRequiredService<IOrderNumberGenerator>();
                return await generator.NextAsync(2099, CancellationToken.None);
            }))
            .ToArray();

        OrderNumber[] numbers = await Task.WhenAll(tasks);

        numbers.Select(n => n.Value).Distinct().Should().HaveCount(callers);
    }

    [Fact]
    public async Task NextAsync_DifferentYears_CountIndependently()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IOrderNumberGenerator generator = scope.ServiceProvider.GetRequiredService<IOrderNumberGenerator>();

        OrderNumber firstOf2098 = await generator.NextAsync(2098, CancellationToken.None);
        OrderNumber firstOf2097 = await generator.NextAsync(2097, CancellationToken.None);

        firstOf2098.Value.Should().Be("TB-2098-00001");
        firstOf2097.Value.Should().Be("TB-2097-00001");
    }
}
