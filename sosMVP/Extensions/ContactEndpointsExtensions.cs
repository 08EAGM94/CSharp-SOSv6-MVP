using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SosMVP.Handlers;
using SosMVP.Security;

namespace SosMVP.Extensions;

public static class ContactEndpointsExtensions
{
    public static IEndpointRouteBuilder MapContactEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var authenticatedEndpoints = endpoints.MapGroup(string.Empty).RequireAuthorization();

        var adminEndpoints = endpoints.MapGroup(string.Empty).RequireAuthorization(AdminAuthorization.PolicyName);

        authenticatedEndpoints.MapPost("/contact/", ContactHandlers.InsertContactAsync)
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithName("InsertContact");

        authenticatedEndpoints.MapGet("/contact/{id}", ContactHandlers.GetContactAsync)
            .Produces<ContactDTO>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetContact");

        adminEndpoints.MapGet("/contactsent/{enterpriseId}", ContactHandlers.GetContactsByEnterpriseAsync)
            .Produces<IEnumerable<ContactDTO>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithName("GetContactsByEnterprise");

        adminEndpoints.MapPut("/contact/{id}", ContactHandlers.UpdateContactAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("UpdateContact");

        adminEndpoints.MapPut("/contactv/{id}", ContactHandlers.UpdateContactVisibilityAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("UpdateContactVisibility");

        authenticatedEndpoints.MapGet("/contactsentsct/{enterpriseId}", ContactHandlers.GetContactsByEnterpriseForSelectAsync)
            .Produces<IEnumerable<ContactDTO>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName("GetContactsByEnterpriseForSelect");

        adminEndpoints.MapGet("/contactsct/", ContactHandlers.GetContactsForSelectAsync)
            .Produces<IEnumerable<ContactDTO>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithName("GetContactsForSelect");

        return endpoints;
    }
}