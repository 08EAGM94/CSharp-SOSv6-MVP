using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SosMVP.Handlers;
using SosMVP.Security;

namespace SosMVP.Extensions;

public static class TypeEndpointsExtensions
{
    public static IEndpointRouteBuilder MapTypeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // None of the six routes is public, so both groups demand a valid session: the pipeline of
        // Program.cs authenticates before authorizing, which answers 401 when the session is missing
        // or expired (RF-1.1, RF-1.2, RF-1.7).
        var authenticatedEndpoints = endpoints.MapGroup(string.Empty).RequireAuthorization();

        // The three routes that govern the catalogue compare the role of the session, and they do it
        // through the policy that already exists in AdminAuthorization, so a non admin or corrupted
        // session ends in 403 without running any handler (RF-1.3, RF-1.4, RF-1.5, CE-8).
        var adminEndpoints = endpoints.MapGroup(string.Empty).RequireAuthorization(AdminAuthorization.PolicyName);

        // The response contract is declared on each endpoint, never on the group: Produces documents
        // the codes without executing anything, and the ones produced by the middleware (401, 403) are
        // declared too (RF-9.2, RF-9.3).
        authenticatedEndpoints.MapPost("/type/", TypeHandlers.InsertTypeAsync)
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithName("InsertType");

        authenticatedEndpoints.MapGet("/type/{id}", TypeHandlers.GetTypeAsync)
            .Produces<TypeDTO>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetType");

        adminEndpoints.MapGet("/type/", TypeHandlers.GetTypesAsync)
            .Produces<IEnumerable<TypeDTO>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithName("GetTypes");

        adminEndpoints.MapPut("/type/{id}", TypeHandlers.UpdateTypeAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("UpdateType");

        adminEndpoints.MapPut("/typev/{id}", TypeHandlers.UpdateTypeVisibilityAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("UpdateTypeVisibility");

        authenticatedEndpoints.MapGet("/typesct/", TypeHandlers.GetTypesForSelectsAsync)
            .Produces<IEnumerable<TypeDTO>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName("GetTypesForSelects");

        return endpoints;
    }
}