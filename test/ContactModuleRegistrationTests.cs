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
/// Checks the dependency injection registration created by <c>AddContactModule</c>: every contact port
/// is scoped and the concrete adapter, its interfaces and the use case that consumes it always share
/// one single instance inside the same scope. The container is composed the same way Program.cs
/// composes it, so the validation of the whole graph is realistic.
/// </summary>
public class ContactModuleRegistrationTests
{
    private static ServiceCollection BuildServices()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        // Unit tests never touch the database, the repository only needs the context reference.
        services.AddScoped<Sosv6DbContext>(_ => null!);

        // Program.cs composes the user module first: it is the one that registers the contact mapper.
        services.AddUserModule();
        services.AddContactModule();

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

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ContactRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IByEnterpriseRepository<ContactEntity, ContactDTO>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISelectRepository<ContactDTO>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<EnterpriseChildrenService<ContactEntity, ContactDTO>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEnterpriseChildrenService<ContactDTO>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SelectService<ContactDTO>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISelectService<ContactDTO>>());
    }

    [Fact]
    public void BothSecondaryPortsResolveToTheSingleContactRepositoryInstance()
    {
        using var provider = BuildContainer();
        using var scope = provider.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<ContactRepository>();

        Assert.Same(repository, scope.ServiceProvider.GetRequiredService<IByEnterpriseRepository<ContactEntity, ContactDTO>>());
        Assert.Same(repository, scope.ServiceProvider.GetRequiredService<ISelectRepository<ContactDTO>>());
    }

    [Fact]
    public void IEnterpriseChildrenService_ReturnsTheSameInstanceOfTheConcreteUseCase()
    {
        using var provider = BuildContainer();
        using var scope = provider.CreateScope();

        var childrenService = scope.ServiceProvider.GetRequiredService<IEnterpriseChildrenService<ContactDTO>>();

        Assert.Same(scope.ServiceProvider.GetRequiredService<EnterpriseChildrenService<ContactEntity, ContactDTO>>(), childrenService);
    }

    [Fact]
    public void ISelectService_ReturnsTheSameInstanceOfTheConcreteSelectService()
    {
        using var provider = BuildContainer();
        using var scope = provider.CreateScope();

        var selectService = scope.ServiceProvider.GetRequiredService<ISelectService<ContactDTO>>();

        Assert.Same(scope.ServiceProvider.GetRequiredService<SelectService<ContactDTO>>(), selectService);
    }

    [Fact]
    public void TheUseCasesTalkToTheSecondaryPortsAndNotToTheAdapter()
    {
        using var provider = BuildContainer();
        using var scope = provider.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<ContactRepository>();
        var mapper = scope.ServiceProvider.GetRequiredService<IMapper<ContactDTO, ContactEntity>>();

        var childrenService = Assert.IsType<EnterpriseChildrenService<ContactEntity, ContactDTO>>(
            scope.ServiceProvider.GetRequiredService<IEnterpriseChildrenService<ContactDTO>>());

        var selectService = Assert.IsType<SelectService<ContactDTO>>(
            scope.ServiceProvider.GetRequiredService<ISelectService<ContactDTO>>());

        Assert.NotNull(childrenService);
        Assert.NotNull(selectService);
        Assert.IsType<ContactDTOtoEntityMapper>(mapper);
        Assert.NotNull(repository);
    }

    [Fact]
    public void EveryContactRegistrationIsScoped()
    {
        var services = BuildServices();

        Type[] typeServices =
        [
            typeof(ContactRepository),
            typeof(IByEnterpriseRepository<ContactEntity, ContactDTO>),
            typeof(ISelectRepository<ContactDTO>),
            typeof(EnterpriseChildrenService<ContactEntity, ContactDTO>),
            typeof(IEnterpriseChildrenService<ContactDTO>),
            typeof(SelectService<ContactDTO>),
            typeof(ISelectService<ContactDTO>)
        ];

        foreach (var serviceType in typeServices)
        {
            var descriptor = Assert.Single(services, candidate => candidate.ServiceType == serviceType);

            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        }
    }

    [Fact]
    public void TheContactMapperKeepsASingleRegistrationOwnedByTheUserModule()
    {
        // Plan decision: AddContactModule does not register the mapper again, because a duplicated
        // descriptor would break the Assert.Single above of every other module.
        var services = BuildServices();

        var descriptor = Assert.Single(services, candidate => candidate.ServiceType == typeof(IMapper<ContactDTO, ContactEntity>));

        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void TheInstancesAreRecreatedOnEachScopeSoTheLifetimeIsNotSingleton()
    {
        using var provider = BuildContainer();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        Assert.NotSame(
            firstScope.ServiceProvider.GetRequiredService<IEnterpriseChildrenService<ContactDTO>>(),
            secondScope.ServiceProvider.GetRequiredService<IEnterpriseChildrenService<ContactDTO>>());
        Assert.NotSame(
            firstScope.ServiceProvider.GetRequiredService<ContactRepository>(),
            secondScope.ServiceProvider.GetRequiredService<ContactRepository>());
        Assert.NotSame(
            firstScope.ServiceProvider.GetRequiredService<ISelectService<ContactDTO>>(),
            secondScope.ServiceProvider.GetRequiredService<ISelectService<ContactDTO>>());
    }

    [Fact]
    public void AddContactModule_ReturnsTheSameCollectionSoTheCompositionRootCanChain()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddContactModule());
    }

    [Fact]
    public void EveryRegistrationThatAddContactModuleAdds_IsScoped()
    {
        // Stronger than listing the expected types by hand: whatever the module adds, it must be scoped.
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddScoped<Sosv6DbContext>(_ => null!);
        services.AddUserModule();

        var before = services.Select(descriptor => descriptor.ServiceType).ToList();

        services.AddContactModule();

        var added = services
            .Where(descriptor => !before.Contains(descriptor.ServiceType))
            .ToList();

        Assert.NotEmpty(added);
        Assert.All(added, descriptor => Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime));
    }

    [Fact]
    public void BothUseCasesAreWiredToTheSameRepositoryInstanceInsideTheScope()
    {
        // RNF-3: the two use cases must share the single adapter of the scope, not two of them.
        using var provider = BuildContainer();
        using var scope = provider.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<ContactRepository>();
        var childrenService = scope.ServiceProvider.GetRequiredService<IEnterpriseChildrenService<ContactDTO>>();
        var selectService = scope.ServiceProvider.GetRequiredService<ISelectService<ContactDTO>>();

        Assert.Same(repository, CollaboratorOf(childrenService));
        Assert.Same(repository, CollaboratorOf(selectService));
    }

    [Fact]
    public void ScopedRegistrationsCannotBeResolvedFromTheRootProvider()
    {
        using var provider = BuildContainer();

        Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IEnterpriseChildrenService<ContactDTO>>());
    }

    [Fact]
    public void TheContainerValidationIsEffectiveAndWouldReportAMissingDependency()
    {
        // Without AddUserModule there is no IMapper<ContactDTO, ContactEntity>, so validating the
        // graph on build has to fail. It proves the passing test above is not vacuous.
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddScoped<Sosv6DbContext>(_ => null!);
        services.AddContactModule();

        var failure = Assert.ThrowsAny<Exception>(
            () => services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            }));

        Assert.Contains("IMapper", Describe(failure), StringComparison.Ordinal);
    }

    private static object? CollaboratorOf(object useCase)
    {
        return useCase
            .GetType()
            .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Select(field => field.GetValue(useCase))
            .FirstOrDefault(value => value is ContactRepository);
    }

    private static string Describe(Exception failure)
    {
        var messages = new List<string>();

        for (var current = failure; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }

        return string.Join(" | ", messages);
    }
}