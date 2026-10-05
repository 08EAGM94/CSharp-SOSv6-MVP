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
/// Subject: <c>DeviceHandlers.UpdateDevicesAsync</c> with a fake
/// <c>IEnterpriseChildrenService&lt;DeviceDTO&gt;</c>, plus the route level answers that happen before
/// the handler runs. Covers RF-5.1 to RF-5.6, RF-1.3, RF-1.4, RF-8.1, RF-8.2, RF-8.3 and CE-1.
/// </summary>
public class DeviceHandlersUpdateDevicesTests
{
    private const string Route = "/device/{id}";
    private const string Method = "PUT";

    private const string UpdateBody =
        """
        {
          "enterpriseId": 5,
          "typeId": 3,
          "brand": "Makita",
          "model": "DHP484",
          "serialNumber": "SN-0007",
          "inventoryNumber": 321
        }
        """;

    private static DeviceDTO UpdateDto(int? bodyId = null, string? visibility = null)
    {
        return new DeviceDTO
        {
            Id = bodyId,
            EnterpriseId = 5,
            TypeId = 3,
            Brand = "Makita",
            Model = "DHP484",
            SerialNumber = "SN-0007",
            InventoryNumber = 321,
            Visibility = visibility
        };
    }

    private static void AssertOnlyUpdateAsyncChildWasCalled(FakeDeviceChildrenService service)
    {
        Assert.Equal(1, service.UpdateAsyncChildCalls);
        Assert.Equal(0, service.UpdateAsyncVisibilityCalls);
        Assert.Equal(0, service.AddAsyncChildCalls);
        Assert.Equal(0, service.GetAsyncChildCalls);
        Assert.Equal(0, service.GetAsyncChildrenByEnterpriseCalls);
        Assert.Equal(0, service.GetAsyncChildrenByEnterForSelectCalls);
    }

    [Fact]
    public async Task ValidDevice_IsAnsweredWithNoContentAndWithoutBody()
    {
        var service = new FakeDeviceChildrenService();

        var result = await DeviceHandlers.UpdateDevicesAsync(service, 7, UpdateDto());

        await HandlerTestSupport.AssertNoContentAsync(result);
    }

    [Fact]
    public async Task ValidDevice_IsDelegatedExactlyOnceToUpdateAsyncChild()
    {
        var service = new FakeDeviceChildrenService();
        var dto = UpdateDto();

        await DeviceHandlers.UpdateDevicesAsync(service, 7, dto);

        AssertOnlyUpdateAsyncChildWasCalled(service);
        Assert.Same(dto, service.LastUpdatedDto);
    }

    [Fact]
    public async Task RouteId_PrevailsOverTheIdSentInTheBody()
    {
        var service = new FakeDeviceChildrenService();

        await DeviceHandlers.UpdateDevicesAsync(service, 7, UpdateDto(bodyId: 999));

        Assert.Equal(7, Assert.IsType<DeviceDTO>(service.LastUpdatedDto).Id);
    }

    [Fact]
    public async Task TheRestOfTheBody_ReachesTheUseCaseUnchanged()
    {
        var service = new FakeDeviceChildrenService();

        await DeviceHandlers.UpdateDevicesAsync(service, 7, UpdateDto(bodyId: 999));

        var received = Assert.IsType<DeviceDTO>(service.LastUpdatedDto);
        Assert.Equal(7, received.Id);
        Assert.Equal(5, received.EnterpriseId);
        Assert.Equal(3, received.TypeId);
        Assert.Equal("Makita", received.Brand);
        Assert.Equal("DHP484", received.Model);
        Assert.Equal("SN-0007", received.SerialNumber);
        Assert.Equal(321, received.InventoryNumber);
    }

    [Fact]
    public async Task VisibilitySentInTheBody_DoesNotOpenTheVisibilityPortOfTheUseCase()
    {
        var service = new FakeDeviceChildrenService();

        var result = await DeviceHandlers.UpdateDevicesAsync(service, 7, UpdateDto(visibility: "DISABLED"));

        await HandlerTestSupport.AssertNoContentAsync(result);
        AssertOnlyUpdateAsyncChildWasCalled(service);
        Assert.Equal(0, service.UpdateAsyncVisibilityCalls);
    }

    [Fact]
    public async Task UnknownDevice_IsTranslatedToNotFound()
    {
        var service = new FakeDeviceChildrenService
        {
            ExceptionToThrow = new KeyNotFoundException(
                "No se pudo actualizar la información del equipo porque no existe un equipo con ese identificador.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => DeviceHandlers.UpdateDevicesAsync(service, 404, UpdateDto()),
            StatusCodes.Status404NotFound);

        AssertOnlyUpdateAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task RecordNotFound_DeliversTheMessageInSpanish()
    {
        var service = new FakeDeviceChildrenService
        {
            ExceptionToThrow = new KeyNotFoundException(
                "No se pudo actualizar la información del equipo porque no existe un equipo con ese identificador.")
        };

        var exception = await Assert.ThrowsAnyAsync<KeyNotFoundException>(
            () => DeviceHandlers.UpdateDevicesAsync(service, 404, UpdateDto()));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal(
            "No se pudo actualizar la información del equipo porque no existe un equipo con ese identificador.",
            response.Body);
    }

    [Fact]
    public async Task InvalidDevice_IsTranslatedToBadRequest()
    {
        var service = new FakeDeviceChildrenService
        {
            ExceptionToThrow = new EntityException("El número de inventario debe ser un número mayor que cero.")
        };
        var dto = UpdateDto();
        dto.InventoryNumber = 0;

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => DeviceHandlers.UpdateDevicesAsync(service, 7, dto),
            StatusCodes.Status400BadRequest);

        AssertOnlyUpdateAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task BusinessFailure_IsTranslatedToBadRequest()
    {
        var service = new FakeDeviceChildrenService
        {
            ExceptionToThrow = new HexArchApplicationException("El equipo debe pertenecer a una empresa existente.")
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => DeviceHandlers.UpdateDevicesAsync(service, 7, UpdateDto()),
            StatusCodes.Status400BadRequest);

        AssertOnlyUpdateAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task AdminSessionThroughTheRoute_AnswersNoContentAndTheRouteIdWins()
    {
        var service = new FakeDeviceChildrenService();
        await using var host = DeviceEndpointTestHost.Start(service);

        var response = await host.SendAsync(
            Route,
            Method,
            DeviceEndpointTestHost.SessionToken(AdminAuthorization.AdminRole),
            jsonBody: UpdateBody,
            idValue: "7");

        Assert.Equal(StatusCodes.Status204NoContent, response.StatusCode);
        Assert.Equal(string.Empty, response.Body);

        var received = Assert.IsType<DeviceDTO>(service.LastUpdatedDto);
        Assert.Equal(7, received.Id);
        Assert.Equal(5, received.EnterpriseId);
        Assert.Equal(3, received.TypeId);
        Assert.Equal("Makita", received.Brand);
        Assert.Equal("DHP484", received.Model);
        Assert.Equal("SN-0007", received.SerialNumber);
        Assert.Equal(321, received.InventoryNumber);
        AssertOnlyUpdateAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task SessionThatIsNotAdmin_IsAnsweredWithForbiddenAndTheUseCaseIsNotReached()
    {
        var service = new FakeDeviceChildrenService();
        await using var host = DeviceEndpointTestHost.Start(service);

        var response = await host.SendAsync(
            Route,
            Method,
            DeviceEndpointTestHost.SessionToken("user"),
            jsonBody: UpdateBody,
            idValue: "7");

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task NonNumericIdentifierInTheRoute_IsAnsweredWithBadRequestAndTheUseCaseIsNotReached()
    {
        var service = new FakeDeviceChildrenService();
        await using var host = DeviceEndpointTestHost.Start(service);

        var response = await host.SendAsync(
            Route,
            Method,
            DeviceEndpointTestHost.SessionToken(AdminAuthorization.AdminRole),
            jsonBody: UpdateBody,
            idValue: "abc");

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task RequestWithoutSession_IsAnsweredWithUnauthorizedAndTheUseCaseIsNotReached()
    {
        var service = new FakeDeviceChildrenService();
        await using var host = DeviceEndpointTestHost.Start(service);

        var response = await host.SendAsync(Route, Method, bearerToken: null, jsonBody: UpdateBody, idValue: "7");

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }
}
