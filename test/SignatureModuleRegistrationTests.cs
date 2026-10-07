using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.Abstractions.SecondaryPorts;
using HexArch.Application.DTOs;
using HexArch.Application.UseCases;
using HexArch.Data.Models;
using HexArch.Domain.Entities;
using HexArch.Repository;
using Microsoft.Extensions.DependencyInjection;
using SosMVP.Extensions;

namespace test;

/// <summary>
/// Checks the signature ports registered by <c>AddUserModule</c>: both are scoped, the secondary
/// port resolves to the single <c>UserRepository</c> of the scope and the use case receives that
/// same instance, so the endpoint, the use case and the adapter always share one instance per request.
/// </summary>
public class SignatureModuleRegistrationTests
{
    private static ServiceCollection BuildServices()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        // Unit tests never touch the database, the repository only needs the context reference.
        services.AddScoped<Sosv6DbContext>(_ => null!);

        services.AddUserModule();

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

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISignatureRepository<UserEntity, UserDTO>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISignatureService<UserDTO>>());
    }

    [Fact]
    public void ISignatureRepository_ResolvesToTheSingleUserRepositoryOfTheScope()
    {
        using var provider = BuildContainer();
        using var scope = provider.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<UserRepository>();

        Assert.Same(repository, scope.ServiceProvider.GetRequiredService<ISignatureRepository<UserEntity, UserDTO>>());
        Assert.Same(repository, scope.ServiceProvider.GetRequiredService<IRepository<UserEntity, UserDTO>>());
        Assert.Same(repository, scope.ServiceProvider.GetRequiredService<IUserRepository>());
    }

    [Fact]
    public void ISignatureService_ReturnsTheSameInstanceOfTheConcreteUseCase()
    {
        using var provider = BuildContainer();
        using var scope = provider.CreateScope();

        var first = scope.ServiceProvider.GetRequiredService<ISignatureService<UserDTO>>();
        var second = scope.ServiceProvider.GetRequiredService<ISignatureService<UserDTO>>();

        Assert.IsType<SignatureService<UserEntity, UserDTO>>(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void TheSignatureUseCaseReceivesTheSingleUserRepositoryOfTheScope()
    {
        using var provider = BuildContainer();
        using var scope = provider.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<UserRepository>();
        var useCase = scope.ServiceProvider.GetRequiredService<ISignatureService<UserDTO>>();

        Assert.Same(repository, CollaboratorOf(useCase));
    }

    [Fact]
    public void EverySignatureRegistrationIsScoped()
    {
        var services = BuildServices();

        Type[] signatureServices =
        [
            typeof(ISignatureRepository<UserEntity, UserDTO>),
            typeof(ISignatureService<UserDTO>)
        ];

        foreach (var serviceType in signatureServices)
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
            firstScope.ServiceProvider.GetRequiredService<ISignatureService<UserDTO>>(),
            secondScope.ServiceProvider.GetRequiredService<ISignatureService<UserDTO>>());
        Assert.NotSame(
            firstScope.ServiceProvider.GetRequiredService<UserRepository>(),
            secondScope.ServiceProvider.GetRequiredService<UserRepository>());
    }

    private static object? CollaboratorOf(object useCase)
    {
        return useCase
            .GetType()
            .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Select(field => field.GetValue(useCase))
            .FirstOrDefault(value => value is UserRepository);
    }
}
