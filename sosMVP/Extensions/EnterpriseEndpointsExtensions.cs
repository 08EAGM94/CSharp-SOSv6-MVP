using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SosMVP.Handlers;
using SosMVP.Security;

namespace SosMVP.Extensions;

public static class EnterpriseEndpointsExtensions
{
    public static IEndpointRouteBuilder MapEnterpriseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // None of the four non admin routes are public; both groups demand a valid session so
        // missing/expired token is answered as 401 by the authentication pipeline (RF-1.1, RF-1.2, RF-1.6, RF-1.7).
        var authenticatedEndpoints = endpoints.MapGroup(string.Empty).RequireAuthorization();

        // Only PUT /enterprise/{id} and PUT /enterprisev/{id} require the admin role (RF-1.3, RF-1.4, RF-1.5).
        var adminEndpoints = endpoints.MapGroup(string.Empty).RequireAuthorization(AdminAuthorization.PolicyName);

        authenticatedEndpoints.MapPost("/enterprise/", EnterpriseHandlers.InsertEnterpriseAsync)
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithName("InsertEnterprise");

        authenticatedEndpoints.MapGet("/enterprise/{id}", EnterpriseHandlers.GetEnterpriseAsync)
            .Produces<EnterpriseDTO>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetEnterprise");

        authenticatedEndpoints.MapGet("/enterprises/", EnterpriseHandlers.GetEnterprisesAsync)
            .Produces<IEnumerable<EnterpriseDTO>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName("GetEnterprises");

        adminEndpoints.MapPut("/enterprise/{id}", EnterpriseHandlers.UpdateEnterpriseAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("UpdateEnterprise");

        adminEndpoints.MapPut("/enterprisev/{id}", EnterpriseHandlers.UpdateEnterpriseVisibilityAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("UpdateEnterpriseVisibility");

        authenticatedEndpoints.MapGet("/enterprisesct/", EnterpriseHandlers.GetEnterprisesForSelectsAsync)
            .Produces<IEnumerable<EnterpriseDTO>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName("GetEnterprisesForSelects");

        return endpoints;
    }
}
