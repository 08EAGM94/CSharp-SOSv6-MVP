using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SosMVP.Extensions;
using test.Support;

namespace test;

/// <summary>
/// Subject: the public name and the response contract declared by the six routes of
/// <c>MapEnterpriseEndpoints</c>, checked against the verifiable table of RF-9 (RF-9.1, RF-9.2,
/// RF-9.3). Both data sources created by the two groups have to be flattened, otherwise the endpoints
/// of one of them are never inspected.
/// </summary>
public class EnterpriseEndpointsMetadataTests
{
    private static readonly Lazy<IReadOnlyList<RouteEndpoint>> LazyEndpoints = new(MaterializeEnterpriseEndpoints);

    private static IReadOnlyList<RouteEndpoint> Endpoints()
    {
        return LazyEndpoints.Value;
    }

    /// <summary>
    /// Reading the endpoints is what forces RequestDelegateFactory to bind every handler, so this is
    /// the very metadata the server would serve (the binding itself is guarded by
    /// EnterpriseRoutingTests).
    /// </summary>
    private static IReadOnlyList<RouteEndpoint> MaterializeEnterpriseEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddScoped<ICommonService<EnterpriseDTO>>(_ => null!);
        builder.Services.AddScoped<ISelectService<EnterpriseDTO>>(_ => null!);
        builder.Services.AddSingleton(JwtTestTokens.Factory());

        var app = builder.Build();

        app.MapEnterpriseEndpoints();

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    private static RouteEndpoint Find(string route, string method)
    {
        return Endpoints().Single(endpoint =>
            endpoint.RoutePattern.RawText == route &&
            endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The documented status codes, without duplicates and in a stable order so that the comparison
    /// against the table of the spec is a comparison of sets.
    /// </summary>
    private static IReadOnlyList<int> DeclaredStatusCodes(RouteEndpoint endpoint)
    {
        return endpoint.Metadata
            .GetOrderedMetadata<IProducesResponseTypeMetadata>()
            .Select(metadata => metadata.StatusCode)
            .Distinct()
            .OrderBy(statusCode => statusCode)
            .ToList();
    }

    /// <summary>
    /// Asserts the whole public contract of one route: the name it is published under and the codes it
    /// documents, exactly. A code missing or a code of more makes this fail (RF-9.1, RF-9.2, RF-9.3).
    /// </summary>
    private static void AssertContract(string method, string route, string expectedName, int[] expectedStatusCodes)
    {
        var endpoint = Find(route, method);

        // RF-9.1: WithName publishes this exact name.
        var declaredName = endpoint.Metadata.GetMetadata<IEndpointNameMetadata>();

        Assert.NotNull(declaredName);
        Assert.Equal(expectedName, declaredName.EndpointName);

        // RF-9.2, RF-9.3: the documented codes are exactly these: neither one missing nor one extra.
        Assert.Equal(
            expectedStatusCodes.OrderBy(statusCode => statusCode).ToList(),
            DeclaredStatusCodes(endpoint));
    }

    [Fact]
    public void AllSixRoutesAreDeclaredWithTheirExpectedMethods()
    {
        Assert.Equal(6, Endpoints().Count);

        foreach (var (method, route) in new[]
        {
            ("POST", "/enterprise/"),
            ("GET", "/enterprise/{id}"),
            ("GET", "/enterprises/"),
            ("PUT", "/enterprise/{id}"),
            ("PUT", "/enterprisev/{id}"),
            ("GET", "/enterprisesct/")
        })
        {
            Assert.True(Find(route, method) is not null, $"Falta el endpoint {method} {route}.");
        }
    }

    [Fact]
    public void InsertEnterprise_HasCorrectWithNameAndProduces()
    {
        // 201 answered on success, 400 on a business rule failure, 401 without a valid session and
        // 500 when the persistence fails (RF-2, RF-1.2, CE-11, CE-17).
        AssertContract(HttpMethods.Post, "/enterprise/", "InsertEnterprise", [201, 400, 401, 500]);
    }

    [Fact]
    public void GetEnterprise_HasCorrectWithNameAndProduces()
    {
        // 200 with the enterprise, 400 on a business rule failure or a non numeric id, 401 without a
        // valid session and 404 when it does not exist (RF-3, RF-1.2, CE-10, CE-14).
        AssertContract(HttpMethods.Get, "/enterprise/{id}", "GetEnterprise", [200, 400, 401, 404]);
    }

    [Fact]
    public void GetEnterprises_HasCorrectWithNameAndProduces()
    {
        // The listing only answers 200, including the empty collection, and 401 without a session:
        // it filters nothing and never fails (RF-4, CE-6, CE-6b).
        AssertContract(HttpMethods.Get, "/enterprises/", "GetEnterprises", [200, 401]);
    }

    [Fact]
    public void UpdateEnterprise_HasCorrectWithNameAndProduces()
    {
        // 204 on success, 400 on a rule failure, 401 without a session, 403 without the admin role and
        // 404 when it does not exist (RF-5, RF-1.2, RF-1.4, CE-5).
        AssertContract(HttpMethods.Put, "/enterprise/{id}", "UpdateEnterprise", [204, 400, 401, 403, 404]);
    }

    [Fact]
    public void UpdateEnterpriseVisibility_HasCorrectWithNameAndProduces()
    {
        // Same contract as the update above: it changes a property of the same aggregate and is gated
        // by the same admin policy (RF-6, RF-1.3, RF-1.4, CE-13).
        AssertContract(HttpMethods.Put, "/enterprisev/{id}", "UpdateEnterpriseVisibility", [204, 400, 401, 403, 404]);
    }

    [Fact]
    public void GetEnterprisesForSelects_HasCorrectWithNameAndProduces()
    {
        // The filtered listing also only answers 200 and 401: the visibility filter lives in the query,
        // never in the handler, so it adds no error of its own (RF-7, CE-6c).
        AssertContract(HttpMethods.Get, "/enterprisesct/", "GetEnterprisesForSelects", [200, 401]);
    }

    [Fact]
    public void NoTwoRoutesShareTheSamePublicName()
    {
        // Endpoint names are the contract clients link against, so a duplicated one would make one of
        // the two unreachable by name (RF-9.1).
        var names = Endpoints()
            .Select(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName)
            .ToList();

        Assert.DoesNotContain(names, name => name is null);
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }
}
