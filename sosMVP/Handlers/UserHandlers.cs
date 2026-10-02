using System.Security.Claims;
using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Http;
using SosMVP.Security;

namespace SosMVP.Handlers;

public static class UserHandlers
{
    private const string AdminRole = "admin";
    private const string UserRole = "user";
    private const string DisabledVisibility = "DISABLED";

    public static async Task<IResult> InsertUserAsync(ICommonService<UserDTO> commonService, UserDTO dto)
    {
        if (dto.Role is not (AdminRole or UserRole))
        {
            return Error(StatusCodes.Status400BadRequest, "El rol debe ser admin o user.");
        }

        await commonService.AddAsyncInfo(dto);

        return Results.StatusCode(StatusCodes.Status201Created);
    }

    public static async Task<IResult> GetUserAsync(ICommonService<UserDTO> commonService, int id)
    {
        var user = await commonService.GetAsyncInfo(new UserDTO { Id = id });

        return Results.Ok(user);
    }

    public static async Task<IResult> GetUsersAsync(ICommonService<UserDTO> commonService)
    {
        var users = await commonService.GetAsyncAllInfo();

        return Results.Ok(users);
    }

    public static async Task<IResult> UpdateUserAsync(ICommonService<UserDTO> commonService, int id, UserDTO dto)
    {
        dto.Id = id;

        await commonService.UpdateAsyncInfo(dto);

        return Results.NoContent();
    }

    public static async Task<IResult> UpdateUserVisibilityAsync(
        ICommonService<UserDTO> commonService,
        ClaimsPrincipal principal,
        int id,
        UserDTO dto)
    {
        if (dto.Visibility == DisabledVisibility && AdminAuthorization.ReadUserIdClaim(principal) == id)
        {
            return Error(StatusCodes.Status403Forbidden, "El administrador no puede deshabilitar su propia cuenta.");
        }

        await commonService.UpdateAsyncVisibility(new UserDTO { Id = id, Visibility = dto.Visibility });

        return Results.NoContent();
    }

    public static async Task<IResult> LoginAsync(IUserService userService, JwtTokenFactory tokenFactory, UserDTO dto)
    {
        var user = await userService.Login(dto);
        var token = tokenFactory.CreateToken(user);

        return Results.Ok(new TokenResponse(token));
    }

    public static IResult AdminVerificationAsync(IUserService userService, UserDTO dto)
    {
        if (string.IsNullOrWhiteSpace(dto.AdminNickname) || string.IsNullOrWhiteSpace(dto.AdminPwd))
        {
            return Error(StatusCodes.Status400BadRequest, "El apodo y la contraseña del administrador son obligatorios.");
        }

        return userService.AdminPwdConfirmation(dto)
            ? Results.Ok(new AdminConfirmation(true))
            : Error(StatusCodes.Status403Forbidden, "La contraseña del administrador no coincide.");
    }

    private static IResult Error(int statusCode, string message)
    {
        return Results.Text(message, statusCode: statusCode);
    }
}

public sealed record TokenResponse(string Token);

public sealed record AdminConfirmation(bool Confirmed);
