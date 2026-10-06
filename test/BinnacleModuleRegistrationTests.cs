using HexArch.Application.Abstractions.Mapper;
using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.Abstractions.SecondaryPorts;
using HexArch.Application.DTOs;
using HexArch.Application.UseCases;
using HexArch.Data.Mappers.DtoToEntity;
using HexArch.Data.Models;
using HexArch.Domain.Entities;
using HexArch.Repository;
using Microsoft.Extensions.DependencyInjection;
using SosMVP.Extensions;

namespace test;

/// <summary>
/// Checks the dependency injection registration created by <c>AddBinnacleModule</c>: the repository,
/// its port, the mapper and the use case are scoped and resolve. The container is composed the same
/// way Program.cs composes it.
/// </summary>
public class BinnacleModuleRegistrationTests
{
    private static ServiceCollection BuildServices()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        // Unit tests never touch the database, the repository only needs the context reference.
        services.AddScoped<Sosv6DbContext>(_ => null!);

        services.AddBinnacleModule();

        return services;
    }

    private static ServiceProvider BuildContainer()
    {
        return BuildServices().BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    [Fact]
    public void TheContainerStartsWithoutErrors()
    {
        using var provider = BuildContainer();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<BinnacleRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IBinnacleRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IMapper<BinnacleDTO, BinnacleEntity>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IBinnacleService>());
    }

    [Fact]
    public void TheSecondaryPortResolvesToTheSingleRepositoryInstance()
    {
        using var provider = BuildContainer();
        using var scope = provider.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<BinnacleRepository>();

        Assert.Same(repository, scope.ServiceProvider.GetRequiredService<IBinnacleRepository>());
    }

    [Fact]
    public void IBinnacleService_ResolvesAsTheBinnacleUseCase()
    {
        using var provider = BuildContainer();
        using var scope = provider.CreateScope();

        Assert.IsType<BinnacleService>(scope.ServiceProvider.GetRequiredService<IBinnacleService>());
    }

    [Fact]
    public void IBinnacleService_IsWiredToTheSecondaryPortSharedInsideTheScope()
    {
        using var provider = BuildContainer();
        using var scope = provider.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<IBinnacleRepository>();
        var useCase = Assert.IsType<BinnacleService>(scope.ServiceProvider.GetRequiredService<IBinnacleService>());

        Assert.Same(repository, CollaboratorOf(useCase));
    }

    [Fact]
    public void TheMapperResolvesAsTheBinnacleDtoToEntityMapper()
    {
        using var provider = BuildContainer();
        using var scope = provider.CreateScope();

        Assert.IsType<BinnacleDTOtoEntityMapper>(
            scope.ServiceProvider.GetRequiredService<IMapper<BinnacleDTO, BinnacleEntity>>());
    }

    [Fact]
    public void EveryBinnacleRegistrationIsScoped()
    {
        var services = BuildServices();

        Type[] typeServices =
        [
            typeof(BinnacleRepository),
            typeof(IBinnacleRepository),
            typeof(IMapper<BinnacleDTO, BinnacleEntity>),
            typeof(IBinnacleService)
        ];

        foreach (var serviceType in typeServices)
        {
            var descriptor = Assert.Single(services, candidate => candidate.ServiceType == serviceType);

            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        }
    }

    [Fact]
    public void TheInstancesAreRecreatedOnEachScopeSoTheLifetimeIsNotSingleton()
    {
        using var provider = BuildContainer();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        Assert.NotSame(
            firstScope.ServiceProvider.GetRequiredService<IBinnacleService>(),
            secondScope.ServiceProvider.GetRequiredService<IBinnacleService>());
        Assert.NotSame(
            firstScope.ServiceProvider.GetRequiredService<IBinnacleRepository>(),
            secondScope.ServiceProvider.GetRequiredService<IBinnacleRepository>());
    }

    [Fact]
    public void AddBinnacleModule_ReturnsTheSameCollectionSoTheCompositionRootCanChain()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddBinnacleModule());
    }

    private static object? CollaboratorOf(object useCase)
    {
        return useCase
            .GetType()
            .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Select(field => field.GetValue(useCase))
            .FirstOrDefault(value => value is IBinnacleRepository);
    }
}