using System.Reflection;
using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SosMVP.Extensions;
using SosMVP.Handlers;
using test.Support;

namespace test;

/// <summary>
/// Subject: the routing table produced by <c>MapEnterpriseEndpoints</c>. Starting the application is
/// NOT a valid check here: the route table is built lazily, so a handler whose parameters the minimal
/// API cannot bind makes the application start normally and then answer every request with 500.
/// This file is the regression guard that materializes the table for real.
/// </summary>
public class EnterpriseRoutingTests
{
    private static readonly Lazy<IReadOnlyList<RouteEndpoint>> LazyEndpoints = new(MaterializeEnterpriseEndpoints);

    private static IReadOnlyList<RouteEndpoint> EnterpriseEndpoints()
    {
        return LazyEndpoints.Value;
    }

    private static IReadOnlyList<RouteEndpoint> MaterializeEnterpriseEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddScoped<ICommonService<EnterpriseDTO>>(_ => null!);
        builder.Services.AddScoped<ISelectService<EnterpriseDTO>>(_ => null!);
        builder.Services.AddSingleton(JwtTestTokens.Factory());

        var app = builder.Build();

        app.MapEnterpriseEndpoints();

        // Reading the endpoints is what forces RequestDelegateFactory to bind every handler; a
        // parameter it cannot infer throws here instead of at request time.
        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    [Fact]
    public void TheRoutingTableIsBuiltWithoutAnyBindingFailure()
    {
        var routes = EnterpriseEndpoints()
            .Select(endpoint => $"{endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single()} {endpoint.RoutePattern.RawText}")
            .OrderBy(route => route, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            [
                "GET /enterprise/{id}",
                "GET /enterprises/",
                "GET /enterprisesct/",
                "POST /enterprise/",
                "PUT /enterprise/{id}",
                "PUT /enterprisev/{id}"
            ],
            routes);
    }

    [Fact]
    public void InsertEnterprise_HasASingleRequestBodySoNoParameterStaysUnbound()
    {
        // A minimal API accepts one complex body per endpoint. A handler declaring two of them leaves
        // the second without a binding source, which breaks the whole routing table. The body of
        // InsertEnterprise is therefore the single InsertEnterpriseRequest wrapper (RF-2.1, RF-2.2).
        var bodyParameters = typeof(EnterpriseHandlers)
            .GetMethod(nameof(EnterpriseHandlers.InsertEnterpriseAsync))!
            .GetParameters()
            .Where(parameter => parameter.ParameterType != typeof(ICommonService<EnterpriseDTO>))
            .ToList();

        var body = Assert.Single(bodyParameters);

        Assert.Equal(typeof(InsertEnterpriseRequest), body.ParameterType);
        Assert.NotNull(typeof(InsertEnterpriseRequest).GetProperty(nameof(InsertEnterpriseRequest.Enterprise)));
        Assert.NotNull(typeof(InsertEnterpriseRequest).GetProperty(nameof(InsertEnterpriseRequest.Contact)));
    }

    [Fact]
    public void InsertEnterpriseRequest_IsDeclaredAsASealedRecordOnTheHandlersFile()
    {
        // The wrapper lives next to the handler, following the precedent of TokenResponse and
        // AdminConfirmation in UserHandlers.cs.
        var type = typeof(EnterpriseHandlers).Assembly.GetType("SosMVP.Handlers.InsertEnterpriseRequest");

        Assert.NotNull(type);
        Assert.True(type!.IsSealed);
        Assert.NotNull(type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        Assert.Equal(typeof(EnterpriseHandlers).Assembly, type.Assembly);
    }
}