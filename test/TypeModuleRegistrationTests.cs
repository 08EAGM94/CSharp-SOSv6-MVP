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
/// Checks the dependency injection registration created by AddTypeModule: every Type port is
/// scoped and the concrete adapter, its interface and the use case that consumes it always
/// share one single instance inside the same scope. The container is composed the same way
/// Program.cs composes it, so the validation of the whole graph is realistic.
/// </summary>
public class TypeModuleRegistrationTests
{
    private static ServiceCollection BuildServices()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        // Unit tests never touch the database, the repositories only need the context reference.
        services.AddScoped<Sosv6DbContext>(_ => null!);

        services.AddUserModule();
        services.AddTypeModule();

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

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<TypeRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRepository<TypeEntity, TypeDTO>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISelectRepository<TypeDTO>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IMapper<TypeDTO, TypeEntity>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CommonService<TypeEntity, TypeDTO>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ICommonService<TypeDTO>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SelectService<TypeDTO>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISelectService<TypeDTO>>());
    }

    [Fact]
    public void ICommonService_ReturnsTheSameInstanceOnEveryResolutionInsideTheSameScope()
    {
        using var provider = BuildContainer();
        using var scope = provider.CreateScope();

        var first = scope.ServiceProvider.GetRequiredService<ICommonService<TypeDTO>>();
        var second = scope.ServiceProvider.GetRequiredService<ICommonService<TypeDTO>>();

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Same(scope.ServiceProvider.GetRequiredService<CommonService<TypeEntity, TypeDTO>>(), first);
    }

    [Fact]
    public void ISelectService_ReturnsTheSameInstanceOfTheConcreteSelectService()
    {
        using var provider = BuildContainer();
        using var scope = provider.CreateScope();

        var selectService = scope.ServiceProvider.GetRequiredService<ISelectService<TypeDTO>>();

        Assert.Same(scope.ServiceProvider.GetRequiredService<SelectService<TypeDTO>>(), selectService);
    }

    [Fact]
    public void BothSecondaryPortsResolveToTheSingleTypeRepositoryInstance()
    {
        using var provider = BuildContainer();
        using var scope = provider.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<TypeRepository>();

        Assert.Same(repository, scope.ServiceProvider.GetRequiredService<IRepository<TypeEntity, TypeDTO>>());
        Assert.Same(repository, scope.ServiceProvider.GetRequiredService<ISelectRepository<TypeDTO>>());
    }

    [Fact]
    public void TheDtoToEntityMapperIsRegisteredWithItsProductionImplementation()
    {
        using var provider = BuildContainer();
        using var scope = provider.CreateScope();

        var mapper = scope.ServiceProvider.GetRequiredService<IMapper<TypeDTO, TypeEntity>>();

        Assert.IsType<TypeDTOtoEntityMapper>(mapper);
    }

    [Fact]
    public void EveryTypeRegistrationIsScoped()
    {
        var services = BuildServices();

        Type[] typeServices =
        [
            typeof(TypeRepository),
            typeof(IRepository<TypeEntity, TypeDTO>),
            typeof(ISelectRepository<TypeDTO>),
            typeof(IMapper<TypeDTO, TypeEntity>),
            typeof(CommonService<TypeEntity, TypeDTO>),
            typeof(ICommonService<TypeDTO>),
            typeof(SelectService<TypeDTO>),
            typeof(ISelectService<TypeDTO>)
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

        var firstCommonService = firstScope.ServiceProvider.GetRequiredService<ICommonService<TypeDTO>>();
        var secondCommonService = secondScope.ServiceProvider.GetRequiredService<ICommonService<TypeDTO>>();

        Assert.NotSame(firstCommonService, secondCommonService);
        Assert.NotSame(
            firstScope.ServiceProvider.GetRequiredService<TypeRepository>(),
            secondScope.ServiceProvider.GetRequiredService<TypeRepository>());
        Assert.NotSame(
            firstScope.ServiceProvider.GetRequiredService<ISelectService<TypeDTO>>(),
            secondScope.ServiceProvider.GetRequiredService<ISelectService<TypeDTO>>());
    }
}
