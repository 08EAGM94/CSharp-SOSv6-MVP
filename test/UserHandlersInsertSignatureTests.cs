using System.Security.Claims;
using HexArch.Application.DTOs;
using HexArch.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using test.Fakes;
using test.Support;

namespace test;

public class UserHandlersInsertSignatureTests
{
    private static readonly string MaxLengthSignature = "Firma valida de longitud maxima ".PadRight(255, 'f');

    private static UserDTO BodyWithExtraProperties()
    {
        return new UserDTO
        {
            Id = 99,
            Name = "Nombre Que No Debe Propagarse",
            Surname = "Apellido Que No Debe Propagarse",
            Nickname = "aliasQueNoDebePropagarse",
            Password = "clave-que-no-debe-propagarse",
            Role = "admin",
            Signature = MaxLengthSignature,
            Visibility = "DISABLED",
            ConfPwd = "clave-que-no-debe-propagarse",
            AdminNickname = "adminQueNoDebePropagarse",
            AdminPwd = "clave-que-no-debe-propagarse"
        };
    }

    [Fact]
    public async Task ValidSignature_IsAnsweredWithNoContent()
    {
        var signatureService = new FakeSignatureService();

        var result = await UserHandlers.InsertSignatureAsync(signatureService, 7, BodyWithExtraProperties());

        await HandlerTestSupport.AssertNoContentAsync(result);
        Assert.Equal(1, signatureService.InsertSignatureCalls);
    }

    [Fact]
    public async Task OnlyIdAndSignatureReachTheUseCase()
    {
        var signatureService = new FakeSignatureService();

        await UserHandlers.InsertSignatureAsync(signatureService, 7, BodyWithExtraProperties());

        var received = Assert.IsType<UserDTO>(signatureService.LastInsertedDto);
        Assert.Equal(7, received.Id);
        Assert.Equal(MaxLengthSignature, received.Signature);
        Assert.Null(received.Name);
        Assert.Null(received.Surname);
        Assert.Null(received.Nickname);
        Assert.Null(received.Password);
        Assert.Null(received.Role);
        Assert.Null(received.Visibility);
        Assert.Null(received.ConfPwd);
        Assert.Null(received.AdminNickname);
        Assert.Null(received.AdminPwd);
    }

    [Fact]
    public async Task RouteId_OverridesTheIdOfTheBody()
    {
        var signatureService = new FakeSignatureService();

        await UserHandlers.InsertSignatureAsync(signatureService, 7, BodyWithExtraProperties());

        Assert.Equal(7, Assert.IsType<UserDTO>(signatureService.LastInsertedDto).Id);
    }

    [Fact]
    public async Task NullSignature_IsAnsweredWithNoContent()
    {
        var signatureService = new FakeSignatureService();
        var body = BodyWithExtraProperties();
        body.Signature = null;

        var result = await UserHandlers.InsertSignatureAsync(signatureService, 7, body);

        await HandlerTestSupport.AssertNoContentAsync(result);
        Assert.Equal(1, signatureService.InsertSignatureCalls);
        Assert.Null(Assert.IsType<UserDTO>(signatureService.LastInsertedDto).Signature);
    }

    [Fact]
    public void TheDelegateTakesNoSessionSoItCannotReadTheRoleNorAnswerForbidden()
    {
        // RF-1.8 / CE-28: without a ClaimsPrincipal the delegate cannot inspect the role,
        // so no invocation of it can ever answer 403 for a role reason.
        var method = typeof(UserHandlers).GetMethod(nameof(UserHandlers.InsertSignatureAsync));

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
            ExceptionToThrow = new KeyNotFoundException("No se pudo guardar la firma porque no existe un usuario con ese identificador.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => UserHandlers.InsertSignatureAsync(signatureService, 404, BodyWithExtraProperties()),
            StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task SignatureLongerThanTwoHundredFiftyFiveCharacters_IsTranslatedToBadRequest()
    {
        var signatureService = new FakeSignatureService
        {
            ExceptionToThrow = new EntityException("La firma no puede superar los 255 caracteres.")
        };
        var body = BodyWithExtraProperties();
        body.Signature = new string('a', 256);

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => UserHandlers.InsertSignatureAsync(signatureService, 7, body),
            StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task NonPositiveRouteId_IsTranslatedToBadRequest()
    {
        var signatureService = new FakeSignatureService
        {
            ExceptionToThrow = new EntityException("El identificador debe ser un número mayor que cero.")
        };

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => UserHandlers.InsertSignatureAsync(signatureService, 0, BodyWithExtraProperties()),
            StatusCodes.Status400BadRequest);
    }
}
