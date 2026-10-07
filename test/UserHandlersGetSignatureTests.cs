using System.Security.Claims;
using System.Text.Json;
using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using test.Fakes;
using test.Support;

namespace test;

public class UserHandlersGetSignatureTests
{
    [Fact]
    public async Task ExistingSignature_AnswersOkWithOnlyTheSignatureProperty()
    {
        var signatureService = new FakeSignatureService
        {
            GetResult = new UserDTO { Id = 7, Signature = "abc" }
        };

        var result = await UserHandlers.GetSignatureAsync(signatureService, 7);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        // CE-27: the body carries the Signature property and nothing else.
        var body = HandlerTestSupport.ReadJsonBody(response.Body);
        var property = Assert.Single(body.EnumerateObject());
        Assert.Equal("signature", property.Name);
        Assert.Equal("abc", property.Value.GetString());
    }

    [Fact]
    public async Task OnlyTheRouteIdReachesTheUseCase()
    {
        var signatureService = new FakeSignatureService();

        await UserHandlers.GetSignatureAsync(signatureService, 7);

        var requested = Assert.IsType<UserDTO>(signatureService.LastRequestedDto);
        Assert.Equal(1, signatureService.GetSignatureCalls);
        Assert.Equal(7, requested.Id);
        Assert.Null(requested.Name);
        Assert.Null(requested.Surname);
        Assert.Null(requested.Nickname);
        Assert.Null(requested.Password);
        Assert.Null(requested.Role);
        Assert.Null(requested.Signature);
        Assert.Null(requested.Visibility);
        Assert.Null(requested.ConfPwd);
        Assert.Null(requested.AdminNickname);
        Assert.Null(requested.AdminPwd);
    }

    [Fact]
    public async Task DisabledUserSignature_IsStillAnsweredWithOk()
    {
        var signatureService = new FakeSignatureService
        {
            GetResult = new UserDTO { Id = 7, Signature = "firma del usuario", Visibility = "DISABLED" }
        };

        var result = await UserHandlers.GetSignatureAsync(signatureService, 7);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal("firma del usuario", HandlerTestSupport.ReadJsonBody(response.Body).GetProperty("signature").GetString());
    }

    [Fact]
    public async Task NullStoredSignature_IsAnsweredWithOk()
    {
        var signatureService = new FakeSignatureService
        {
            GetResult = new UserDTO { Id = 7, Signature = null }
        };

        var result = await UserHandlers.GetSignatureAsync(signatureService, 7);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Null, HandlerTestSupport.ReadJsonBody(response.Body).GetProperty("signature").ValueKind);
    }

    [Fact]
    public void TheDelegateTakesNoSessionSoItCannotReadTheRoleNorAnswerForbidden()
    {
        // RF-1.8 / CE-28: without a ClaimsPrincipal the delegate cannot inspect the role,
        // so no invocation of it can ever answer 403 for a role reason.
        var method = typeof(UserHandlers).GetMethod(nameof(UserHandlers.GetSignatureAsync));

        Assert.NotNull(method);
        Assert.DoesNotContain(
            method.GetParameters(),
            parameter => parameter.ParameterType == typeof(ClaimsPrincipal));
    }

    [Fact]
    public async Task UnknownUser_IsTranslatedToNotFound()
    {
        var signatureService = new FakeSignatureService
        {
            ExceptionToThrow = new KeyNotFoundException("No se pudo consultar la firma porque no existe un usuario con ese identificador.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => UserHandlers.GetSignatureAsync(signatureService, 404),
            StatusCodes.Status404NotFound);
    }
}
