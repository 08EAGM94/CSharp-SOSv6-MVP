using HexArch.Application.DTOs;
using HexArch.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using SosMVP.Security;
using test.Fakes;
using test.Support;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace test;

/// <summary>
/// Subject: <c>DeviceHandlers.GetDeviceAsync</c> with a fake
/// <c>IEnterpriseChildrenService&lt;DeviceDTO&gt;</c>, plus the route level answers that happen before
/// the handler runs. Covers RF-3.1 to RF-3.5, RF-8.1, RF-8.2, RF-8.3, CE-1, CE-10 and the
/// authentication of the route (RF-1.1, RF-1.2).
/// </summary>
public class DeviceHandlersGetDeviceTests
{
    private const string Route = "/device/{id}";
    private const string Method = "GET";

    private static DeviceDTO StoredDevice(int id = 7, string? visibility = "ENABLED")
    {
        return new DeviceDTO
        {
            Id = id,
            EnterpriseId = 5,
            TypeId = 2,
            Brand = "Bosch",
            Model = "GBH 18V-26",
            SerialNumber = "SN-0001",
            InventoryNumber = 120,
            Visibility = visibility,
            Type = new TypeDTO { Type = "Herramientas especiales" }
        };
    }

    private static void AssertOnlyGetAsyncChildWasCalled(FakeDeviceChildrenService service)
    {
        Assert.Equal(1, service.GetAsyncChildCalls);
        Assert.Equal(0, service.AddAsyncChildCalls);
        Assert.Equal(0, service.GetAsyncChildrenByEnterpriseCalls);
        Assert.Equal(0, service.GetAsyncChildrenByEnterForSelectCalls);
        Assert.Equal(0, service.UpdateAsyncChildCalls);
        Assert.Equal(0, service.UpdateAsyncVisibilityCalls);
    }

    [Fact]
    public async Task ExistingDevice_AnswersOkWithTheCompleteDtoReturnedByTheUseCase()
    {
        var service = new FakeDeviceChildrenService { ChildResult = StoredDevice() };

        var result = await DeviceHandlers.GetDeviceAsync(service, 7);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var body = HandlerTestSupport.ReadJsonBody(response.Body);
        Assert.Equal(7, body.GetProperty("id").GetInt32());
        Assert.Equal(5, body.GetProperty("enterpriseId").GetInt32());
        Assert.Equal(2, body.GetProperty("typeId").GetInt32());
        Assert.Equal("Bosch", body.GetProperty("brand").GetString());
        Assert.Equal("GBH 18V-26", body.GetProperty("model").GetString());
        Assert.Equal("SN-0001", body.GetProperty("serialNumber").GetString());
        Assert.Equal(120, body.GetProperty("inventoryNumber").GetInt32());
        Assert.Equal("ENABLED", body.GetProperty("visibility").GetString());
        Assert.Equal("Herramientas especiales", body.GetProperty("type").GetProperty("type").GetString());

        AssertOnlyGetAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task RouteId_ReachesTheUseCaseAsTheRequestedId()
    {
        var service = new FakeDeviceChildrenService { ChildResult = StoredDevice() };

        await DeviceHandlers.GetDeviceAsync(service, 7);

        Assert.Equal(7, Assert.IsType<DeviceDTO>(service.LastRequestedDto).Id);
    }

    [Fact]
    public async Task OnlyTheRouteIdReachesTheUseCase()
    {
        var service = new FakeDeviceChildrenService { ChildResult = StoredDevice() };

        await DeviceHandlers.GetDeviceAsync(service, 7);

        var requested = Assert.IsType<DeviceDTO>(service.LastRequestedDto);
        Assert.Equal(7, requested.Id);
        Assert.Null(requested.EnterpriseId);
        Assert.Null(requested.TypeId);
        Assert.Null(requested.Brand);
        Assert.Null(requested.Model);
        Assert.Null(requested.SerialNumber);
        Assert.Null(requested.InventoryNumber);
        Assert.Null(requested.Visibility);
        Assert.Null(requested.Type);
    }

    [Fact]
    public async Task DisabledDevice_AnswersOkWithTheCompleteDtoAndIsNotFiltered()
    {
        var service = new FakeDeviceChildrenService
        {
            ChildResult = StoredDevice(visibility: "DISABLED")
        };

        var result = await DeviceHandlers.GetDeviceAsync(service, 7);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status404NotFound, response.StatusCode);

        var body = HandlerTestSupport.ReadJsonBody(response.Body);
        Assert.Equal(7, body.GetProperty("id").GetInt32());
        Assert.Equal("Bosch", body.GetProperty("brand").GetString());
        Assert.Equal("DISABLED", body.GetProperty("visibility").GetString());

        Assert.Null(Assert.IsType<DeviceDTO>(service.LastRequestedDto).Visibility);
        AssertOnlyGetAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task ResultDtoId_DoesNotChangeTheIdPropagatedToTheUseCase()
    {
        var service = new FakeDeviceChildrenService { ChildResult = StoredDevice(id: 999) };

        var result = await DeviceHandlers.GetDeviceAsync(service, 7);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(7, Assert.IsType<DeviceDTO>(service.LastRequestedDto).Id);
        Assert.Equal(999, HandlerTestSupport.ReadJsonBody(response.Body).GetProperty("id").GetInt32());
        AssertOnlyGetAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task UnknownDevice_IsTranslatedToNotFound()
    {
        var service = new FakeDeviceChildrenService
        {
            ExceptionToThrow = new KeyNotFoundException("No se encontró un equipo con la información solicitada.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => DeviceHandlers.GetDeviceAsync(service, 404),
            StatusCodes.Status404NotFound);

        AssertOnlyGetAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task RecordNotFound_DeliversTheMessageInSpanish()
    {
        var service = new FakeDeviceChildrenService
        {
            ExceptionToThrow = new KeyNotFoundException("No se encontró un equipo con la información solicitada.")
        };

        var exception = await Assert.ThrowsAnyAsync<KeyNotFoundException>(
            () => DeviceHandlers.GetDeviceAsync(service, 404));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal("No se encontró un equipo con la información solicitada.", response.Body);
    }

    [Fact]
    public async Task EntityRuleViolation_IsTranslatedToBadRequest()
    {
        var service = new FakeDeviceChildrenService
        {
            ExceptionToThrow = new EntityException("El identificador debe ser un número mayor que cero.")
        };

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => DeviceHandlers.GetDeviceAsync(service, 0),
            StatusCodes.Status400BadRequest);

        AssertOnlyGetAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task BusinessFailure_IsTranslatedToBadRequest()
    {
        var service = new FakeDeviceChildrenService
        {
            ExceptionToThrow = new HexArchApplicationException("El equipo no pertenece a la empresa solicitada.")
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => DeviceHandlers.GetDeviceAsync(service, 7),
            StatusCodes.Status400BadRequest);

        AssertOnlyGetAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task NonNumericIdentifierInTheRoute_IsAnsweredWithBadRequestAndTheUseCaseIsNotReached()
    {
        var service = new FakeDeviceChildrenService { ChildResult = StoredDevice() };
        await using var host = DeviceEndpointTestHost.Start(service);

        var response = await host.SendAsync(
            Route,
            Method,
            DeviceEndpointTestHost.SessionToken(AdminAuthorization.AdminRole),
            idValue: "abc");

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task ExistingIdentifierThroughTheRoute_IsAnsweredWithOkAndReachesTheUseCase()
    {
        var service = new FakeDeviceChildrenService { ChildResult = StoredDevice() };
        await using var host = DeviceEndpointTestHost.Start(service);

        var response = await host.SendAsync(
            Route,
            Method,
            DeviceEndpointTestHost.SessionToken("user"),
            idValue: "7");

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(7, HandlerTestSupport.ReadJsonBody(response.Body).GetProperty("id").GetInt32());
        AssertOnlyGetAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task RequestWithoutSession_IsAnsweredWithUnauthorizedAndTheUseCaseIsNotReached()
    {
        var service = new FakeDeviceChildrenService { ChildResult = StoredDevice() };
        await using var host = DeviceEndpointTestHost.Start(service);

        var response = await host.SendAsync(Route, Method, bearerToken: null, idValue: "7");

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }
}
