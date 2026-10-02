using HexArch.Application.DTOs;
using HexArch.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using test.Fakes;
using test.Support;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace test;

public class UserHandlersUpdateUserVisibilityTests
{
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
            Signature = "Firma Que No Debe Propagarse",
            Visibility = "DISABLED"
        };
    }

    [Fact]
    public async Task AnotherUserDisabled_IsAnsweredWithNoContent()
    {
        var commonService = new FakeCommonService();
        var session = HandlerTestSupport.CreateSession(userId: 1, role: "admin");

        var result = await UserHandlers.UpdateUserVisibilityAsync(commonService, session, 7, BodyWithExtraProperties());

        await HandlerTestSupport.AssertNoContentAsync(result);
        Assert.Equal(1, commonService.UpdateAsyncVisibilityCalls);
    }

    [Fact]
    public async Task OnlyIdAndVisibilityReachTheUseCase()
    {
        var commonService = new FakeCommonService();
        var session = HandlerTestSupport.CreateSession(userId: 1, role: "admin");

        await UserHandlers.UpdateUserVisibilityAsync(commonService, session, 7, BodyWithExtraProperties());

        var received = Assert.IsType<UserDTO>(commonService.LastVisibilityDto);
        Assert.Equal(7, received.Id);
        Assert.Equal("DISABLED", received.Visibility);
        Assert.Null(received.Name);
        Assert.Null(received.Surname);
        Assert.Null(received.Nickname);
        Assert.Null(received.Password);
        Assert.Null(received.Role);
        Assert.Null(received.Signature);
        Assert.Null(received.ConfPwd);
        Assert.Null(received.AdminNickname);
        Assert.Null(received.AdminPwd);
    }

    [Fact]
    public async Task RouteId_OverridesTheIdOfTheBody()
    {
        var commonService = new FakeCommonService();
        var session = HandlerTestSupport.CreateSession(userId: 1, role: "admin");

        await UserHandlers.UpdateUserVisibilityAsync(commonService, session, 7, BodyWithExtraProperties());

        Assert.Equal(7, Assert.IsType<UserDTO>(commonService.LastVisibilityDto).Id);
    }

    [Fact]
    public async Task AdministratorDisablingItself_IsAnsweredWithForbiddenAndDoesNotInvokeTheUseCase()
    {
        var commonService = new FakeCommonService();
        var session = HandlerTestSupport.CreateSession(userId: 7, role: "admin");

        var result = await UserHandlers.UpdateUserVisibilityAsync(commonService, session, 7, BodyWithExtraProperties());

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal(0, commonService.UpdateAsyncVisibilityCalls);
    }

    [Fact]
    public async Task SelfDisabling_DeliversTheMessageInSpanish()
    {
        var commonService = new FakeCommonService();
        var session = HandlerTestSupport.CreateSession(userId: 7, role: "admin");

        var result = await UserHandlers.UpdateUserVisibilityAsync(commonService, session, 7, BodyWithExtraProperties());

        await HandlerTestSupport.AssertBodyIsInSpanishAsync(result);
    }

    [Fact]
    public async Task AdministratorEnablingItself_IsAllowed()
    {
        var commonService = new FakeCommonService();
        var session = HandlerTestSupport.CreateSession(userId: 7, role: "admin");
        var body = BodyWithExtraProperties();
        body.Visibility = "ENABLED";

        var result = await UserHandlers.UpdateUserVisibilityAsync(commonService, session, 7, body);

        await HandlerTestSupport.AssertNoContentAsync(result);
        Assert.Equal(1, commonService.UpdateAsyncVisibilityCalls);
    }

    [Fact]
    public async Task SessionWithoutIdentifier_DoesNotBlockTheVisibilityChange()
    {
        var commonService = new FakeCommonService();
        var session = HandlerTestSupport.CreateSession(role: "admin");

        var result = await UserHandlers.UpdateUserVisibilityAsync(commonService, session, 7, BodyWithExtraProperties());

        await HandlerTestSupport.AssertNoContentAsync(result);
        Assert.Equal(1, commonService.UpdateAsyncVisibilityCalls);
    }

    [Fact]
    public async Task VisibilityOutsideTheAllowedValues_IsTranslatedToBadRequestAndDoesNotWrite()
    {
        var commonService = new FakeCommonService
        {
            ExceptionToThrow = new HexArchApplicationException("La visibilidad debe ser ENABLED o DISABLED.\n")
        };
        var session = HandlerTestSupport.CreateSession(userId: 1, role: "admin");
        var body = BodyWithExtraProperties();
        body.Visibility = "HABILITADO";

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => UserHandlers.UpdateUserVisibilityAsync(commonService, session, 7, body),
            StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task UnknownUser_IsTranslatedToNotFoundAndDoesNotWrite()
    {
        var commonService = new FakeCommonService
        {
            ExceptionToThrow = new KeyNotFoundException("No se pudo cambiar la visibilidad del usuario porque no existe un usuario con ese identificador.")
        };
        var session = HandlerTestSupport.CreateSession(userId: 1, role: "admin");

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => UserHandlers.UpdateUserVisibilityAsync(commonService, session, 404, BodyWithExtraProperties()),
            StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task EntityRuleViolation_IsTranslatedToBadRequestAndDoesNotWrite()
    {
        var commonService = new FakeCommonService
        {
            ExceptionToThrow = new EntityException("El identificador debe ser un número mayor que cero.")
        };
        var session = HandlerTestSupport.CreateSession(userId: 1, role: "admin");

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => UserHandlers.UpdateUserVisibilityAsync(commonService, session, 0, BodyWithExtraProperties()),
            StatusCodes.Status400BadRequest);
    }
}