using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SosMVP.Handlers;
using SosMVP.Security;

namespace SosMVP.Extensions;

public static class BinnacleEndpointsExtensions
{
    public static IEndpointRouteBuilder MapBinnacleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var authenticatedEndpoints = endpoints.MapGroup(string.Empty).RequireAuthorization();

        var adminEndpoints = endpoints.MapGroup(string.Empty).RequireAuthorization(AdminAuthorization.PolicyName);

        authenticatedEndpoints.MapPost("/binnacle/", BinnacleHandlers.InsertBinnacleAsync)
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status500InternalServerError)
            .WithName("InsertBinnacle");

        authenticatedEndpoints.MapGet("/binnacle/{id}", BinnacleHandlers.GetBinnacleAsync)
            .Produces<BinnacleDTO>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetBinnacle");

        authenticatedEndpoints.MapGet("/binnaclesfu/{page}/{elemsKey}", BinnacleHandlers.FollowupListAsync)
            .Produces<PaginationResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName("FollowupList");

        adminEndpoints.MapPost("/binnaclesr/", BinnacleHandlers.BinnaclesReportAsync)
            .Produces<PaginationResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithName("BinnaclesReport");

        adminEndpoints.MapPut("/binnacle/{id}", BinnacleHandlers.UpdateBinnacleAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("UpdateBinnacle");

        adminEndpoints.MapPut("/binnaclev/{id}", BinnacleHandlers.UpdateBinnacleVisibilityAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("UpdateBinnacleVisibility");

        authenticatedEndpoints.MapPut("/binnaclefup/{id}", BinnacleHandlers.FollowupPartialAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("FollowupPartial");

        authenticatedEndpoints.MapPut("/binnaclera/{id}", BinnacleHandlers.ResetActivitiesAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("ResetActivities");

        authenticatedEndpoints.MapPut("/binnaclecb/{id}", BinnacleHandlers.CancelBinnacleAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("CancelBinnacle");

        authenticatedEndpoints.MapPut("/binnaclefsh/{id}", BinnacleHandlers.FinishBinnacleAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("FinishBinnacle");

        return endpoints;
    }
}