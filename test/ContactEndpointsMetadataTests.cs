using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SosMVP.Extensions;
using SosMVP.Handlers;
using SosMVP.Security;
using test.Support;

namespace test;

/// <summary>
/// Walks the EndpointDataSource produced by <c>MapContactEndpoints</c> and checks the routes, the
/// public names, the response contract declared with <c>Produces</c> and the authorization group of
/// the seven routes against the table of RF-9. Both data sources created by the two groups have to
/// be flattened, otherwise the endpoints of one of them are never inspected.
/// </summary>
public class ContactEndpointsMetadataTests
{
    private static readonly Lazy<IReadOnlyList<RouteEndpoint>> LazyContactEndpoints = new(MaterializeContactEndpoints);

    private static readonly (string Route, string Method, string Name, int[] StatusCodes)[] ExpectedContract =
    [
        ("/contact/", "POST", "InsertContact", [201, 400, 401, 500]),
        ("/contact/{id}", "GET", "GetContact", [200, 400, 401, 404]),
        ("/contactsent/{enterpriseId}", "GET", "GetContactsByEnterprise", [200, 400, 401, 403]),
        ("/contact/{id}", "PUT", "UpdateContact", [204, 400, 401, 403, 404]),
        ("/contactv/{id}", "PUT", "UpdateContactVisibility", [204, 400, 401, 403, 404]),
        ("/contactsentsct/{enterpriseId}", "GET", "GetContactsByEnterpriseForSelect", [200, 400, 401]),
        ("/contactsct/", "GET", "GetContactsForSelect", [200, 401, 403])
    ];

    private static IReadOnlyList<RouteEndpoint> MaterializeContactEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddScoped<IEnterpriseChildrenService<ContactDTO>>(_ => null!);
        builder.Services.AddScoped<ISelectService<ContactDTO>>(_ => null!);
        builder.Services.AddSingleton(JwtTestTokens.Factory());

        var app = builder.Build();

        app.MapContactEndpoints();

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    private static RouteEndpoint Find(IReadOnlyList<RouteEndpoint> endpoints, string route, string method)
    {
        return endpoints.Single(endpoint =>
            endpoint.RoutePattern.RawText == route &&
            endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method, StringComparer.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<int> DeclaredStatusCodes(RouteEndpoint endpoint)
    {
        return endpoint.Metadata
            .GetOrderedMetadata<IProducesResponseTypeMetadata>()
            .Select(metadata => metadata.StatusCode)
            .Distinct()
            .OrderBy(statusCode => statusCode)
            .ToList();
    }

    [Fact]
    public void AllSevenRoutesAreMappedWithTheirExpectedMethods()
    {
        var endpoints = LazyContactEndpoints.Value;

        Assert.Equal(7, endpoints.Count);

        foreach (var (route, method, _, _) in ExpectedContract)
        {
            Assert.True(Find(endpoints, route, method) is not null, $"Falta el endpoint {method} {route}.");
        }
    }

    [Theory]
    [InlineData("/contact/", "POST")]
    [InlineData("/contact/{id}", "GET")]
    [InlineData("/contactsent/{enterpriseId}", "GET")]
    [InlineData("/contact/{id}", "PUT")]
    [InlineData("/contactv/{id}", "PUT")]
    [InlineData("/contactsentsct/{enterpriseId}", "GET")]
    [InlineData("/contactsct/", "GET")]
    public void EveryRoute_DeclaresExactlyTheStatusCodesOfTheTable(string route, string method)
    {
        var expected = ExpectedContract.Single(contract => contract.Route == route && contract.Method == method);

        var declared = DeclaredStatusCodes(Find(LazyContactEndpoints.Value, route, method));

        Assert.Equal(expected.StatusCodes.OrderBy(statusCode => statusCode), declared);
    }

    [Theory]
    [InlineData("/contact/", "POST", "InsertContact")]
    [InlineData("/contact/{id}", "GET", "GetContact")]
    [InlineData("/contactsent/{enterpriseId}", "GET", "GetContactsByEnterprise")]
    [InlineData("/contact/{id}", "PUT", "UpdateContact")]
    [InlineData("/contactv/{id}", "PUT", "UpdateContactVisibility")]
    [InlineData("/contactsentsct/{enterpriseId}", "GET", "GetContactsByEnterpriseForSelect")]
    [InlineData("/contactsct/", "GET", "GetContactsForSelect")]
    public void EveryRoute_DeclaresItsExactPublicName(string route, string method, string expectedName)
    {
        var declaredName = Find(LazyContactEndpoints.Value, route, method).Metadata.GetMetadata<IEndpointNameMetadata>();

        Assert.NotNull(declaredName);
        Assert.Equal(expectedName, declaredName.EndpointName);
    }

    [Theory]
    [InlineData("/contact/", "POST")]
    [InlineData("/contact/{id}", "GET")]
    [InlineData("/contactsent/{enterpriseId}", "GET")]
    [InlineData("/contact/{id}", "PUT")]
    [InlineData("/contactv/{id}", "PUT")]
    [InlineData("/contactsentsct/{enterpriseId}", "GET")]
    [InlineData("/contactsct/", "GET")]
    public void EveryRoute_DeclaresUnauthorizedBecauseNoEndpointIsPublic(string route, string method)
    {
        var endpoints = LazyContactEndpoints.Value;

        Assert.Contains(401, DeclaredStatusCodes(Find(endpoints, route, method)));
        Assert.NotEmpty(Find(endpoints, route, method).Metadata.GetOrderedMetadata<IAuthorizeData>());
        Assert.Null(Find(endpoints, route, method).Metadata.GetMetadata<IAllowAnonymous>());
    }

    [Theory]
    [InlineData("/contact/", "POST")]
    [InlineData("/contact/{id}", "GET")]
    [InlineData("/contactsentsct/{enterpriseId}", "GET")]
    public void AuthenticatedRoutes_RequireASessionWithoutTheAdminRole(string route, string method)
    {
        var authorizeData = Find(LazyContactEndpoints.Value, route, method)
            .Metadata.GetOrderedMetadata<IAuthorizeData>()
            .ToList();

        Assert.NotEmpty(authorizeData);
        Assert.DoesNotContain(authorizeData, data => data.Policy == AdminAuthorization.PolicyName);
    }

    [Theory]
    [InlineData("/contactsent/{enterpriseId}", "GET")]
    [InlineData("/contact/{id}", "PUT")]
    [InlineData("/contactv/{id}", "PUT")]
    [InlineData("/contactsct/", "GET")]
    public void AdminRoutes_RequireTheAdminOnlyPolicy(string route, string method)
    {
        var authorizeData = Find(LazyContactEndpoints.Value, route, method)
            .Metadata.GetOrderedMetadata<IAuthorizeData>()
            .ToList();

        Assert.NotEmpty(authorizeData);
        Assert.Contains(authorizeData, data => data.Policy == AdminAuthorization.PolicyName);
    }

    [Theory]
    [InlineData("/contact/{id}", "GET", "GetContactAsync", "id")]
    [InlineData("/contactsent/{enterpriseId}", "GET", "GetContactsByEnterpriseAsync", "enterpriseId")]
    [InlineData("/contact/{id}", "PUT", "UpdateContactAsync", "id")]
    [InlineData("/contactv/{id}", "PUT", "UpdateContactVisibilityAsync", "id")]
    [InlineData("/contactsentsct/{enterpriseId}", "GET", "GetContactsByEnterpriseForSelectAsync", "enterpriseId")]
    public void RoutesWithIdentifier_ReceiveItAsAnIntegerSoANonNumericValueIsRejected(
        string route,
        string method,
        string handlerName,
        string parameterName)
    {
        var routeParameter = Assert.Single(
            Find(LazyContactEndpoints.Value, route, method).RoutePattern.Parameters,
            candidate => candidate.Name == parameterName);

        Assert.False(routeParameter.IsOptional);

        var identifier = Assert.Single(
            typeof(ContactHandlers)
                .GetMethod(handlerName)!
                .GetParameters(),
            parameter => parameter.Name == parameterName);

        Assert.Equal(typeof(int), identifier.ParameterType);
    }

    [Theory]
    [InlineData("/contact/{id}", "GET", "id")]
    [InlineData("/contactsent/{enterpriseId}", "GET", "enterpriseId")]
    [InlineData("/contact/{id}", "PUT", "id")]
    [InlineData("/contactv/{id}", "PUT", "id")]
    [InlineData("/contactsentsct/{enterpriseId}", "GET", "enterpriseId")]
    public void RoutesWithIdentifier_DeclareTheIdentifierAsARequiredRouteParameter(
        string route,
        string method,
        string parameterName)
    {
        var parameter = Assert.Single(
            Find(LazyContactEndpoints.Value, route, method).RoutePattern.Parameters,
            candidate => candidate.Name == parameterName);

        Assert.False(parameter.IsOptional);
    }

    [Theory]
    [InlineData("/contact/{id}", "GET", typeof(ContactDTO))]
    [InlineData("/contactsent/{enterpriseId}", "GET", typeof(IEnumerable<ContactDTO>))]
    [InlineData("/contactsentsct/{enterpriseId}", "GET", typeof(IEnumerable<ContactDTO>))]
    [InlineData("/contactsct/", "GET", typeof(IEnumerable<ContactDTO>))]
    public void RoutesThatAnswerARead_DeclareTheirResponseType(string route, string method, Type expectedType)
    {
        var declaredType = Assert.Single(Find(LazyContactEndpoints.Value, route, method).Metadata
            .GetOrderedMetadata<IProducesResponseTypeMetadata>()
            .Where(metadata => metadata.StatusCode == StatusCodes.Status200OK && metadata.Type is not null && metadata.Type != typeof(void))
            .Select(metadata => metadata.Type));

        Assert.Equal(expectedType, declaredType);
    }

    [Fact]
    public void RoutesAnsweredWithNoContent_DeclareNoResponseType()
    {
        var typedCodes = new[] { ("/contact/", "POST"), ("/contact/{id}", "PUT"), ("/contactv/{id}", "PUT") }
            .SelectMany(route => Find(LazyContactEndpoints.Value, route.Item1, route.Item2).Metadata
                .GetOrderedMetadata<IProducesResponseTypeMetadata>()
                .Where(metadata => metadata.Type is not null && metadata.Type != typeof(void))
                .Select(metadata => metadata.StatusCode))
            .Distinct()
            .ToList();

        Assert.Empty(typedCodes);
    }

    [Fact]
    public void NoRouteDeclaresProblemDetails()
    {
        var problemDetailsNames = LazyContactEndpoints.Value
            .SelectMany(endpoint => endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>())
            .Select(metadata => metadata.Type?.Name)
            .Where(name => string.Equals(name, "ProblemDetails", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(problemDetailsNames);
    }
}