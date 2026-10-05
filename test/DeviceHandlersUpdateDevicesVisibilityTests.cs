using System.Text.Json;
using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using SosMVP.Security;
using test.Fakes;
using test.Support;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace test;

/// <summary>
/// Subject: <c>DeviceHandlers.UpdateDevicesVisibilityAsync</c> with a fake
/// <c>IEnterpriseChildrenService&lt;DeviceDTO&gt;</c>, plus the route level answers that happen before
/// the handler runs. Covers RF-6.1 to RF-6.6, RF-8.1, RF-8.3, RF-1.3, RF-1.4, CE-1, CE-2 and CE-13.
/// </summary>
public class DeviceHandlersUpdateDevicesVisibilityTests
{
    private const string Route = "/devicev/{id}";
    private const string Method = "PUT";

    private const int RouteId = 7;

    private const string InvalidVisibilityMessage = "La visibilidad debe ser ENABLED o DISABLED.\n";

    private const string NonPositiveIdMessage = "El identificador debe ser un número mayor que cero.\n";

    private const string ConsumerBodyJson =
        """
        {
          "id": 99,
          "enterpriseId": 5,
          "typeId": 3,
          "brand": "Makita",
          "model": "DHP484",
          "serialNumber": "SN-0007",
          "inventoryNumber": 321,
          "visibility": "DISABLED"
        }
        """;

    private static DeviceDTO BodyWithEveryPropertyInformed(int? bodyId = 99, string? visibility = "DISABLED")
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
            Visibility = visibility,
            Type = new TypeDTO { Type = "Herramientas especiales" }
        };
    }

    private static DeviceDTO BodyAsItArrivesFromJson()
    {
        return JsonSerializer.Deserialize<DeviceDTO>(
            ConsumerBodyJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private static FakeDeviceChildrenService ServiceThatRejectsVisibility()
    {
        return new FakeDeviceChildrenService
        {
            ExceptionToThrow = new HexArchApplicationException(InvalidVisibilityMessage)
        };
    }

    private static void AssertOnlyUpdateAsyncVisibilityWasCalled(FakeDeviceChildrenService service)
    {
        Assert.Equal(1, service.UpdateAsyncVisibilityCalls);
        Assert.Equal(0, service.UpdateAsyncChildCalls);
        Assert.Equal(0, service.AddAsyncChildCalls);
        Assert.Equal(0, service.GetAsyncChildCalls);
        Assert.Equal(0, service.GetAsyncChildrenByEnterpriseCalls);
        Assert.Equal(0, service.GetAsyncChildrenByEnterForSelectCalls);
        Assert.Null(service.LastUpdatedDto);
    }

    private static void AssertEveryPropertyExceptIdAndVisibilityIsNull(DeviceDTO dto)
    {
        Assert.Null(dto.EnterpriseId);
        Assert.Null(dto.TypeId);
        Assert.Null(dto.Brand);
        Assert.Null(dto.Model);
        Assert.Null(dto.SerialNumber);
        Assert.Null(dto.InventoryNumber);
        Assert.Null(dto.Type);
    }

    [Fact]
    public async Task ValidVisibilityUpdate_IsAnsweredWithNoContentAndWithoutBody()
    {
        var service = new FakeDeviceChildrenService();

        var result = await DeviceHandlers.UpdateDevicesVisibilityAsync(service, RouteId, BodyWithEveryPropertyInformed());

        await HandlerTestSupport.AssertNoContentAsync(result);
    }

    [Fact]
    public async Task ValidVisibilityUpdate_IsDelegatedOnceToTheUseCaseAndLeavesTheOtherPortsUntouched()
    {
        var service = new FakeDeviceChildrenService();

        await DeviceHandlers.UpdateDevicesVisibilityAsync(service, RouteId, BodyWithEveryPropertyInformed());

        AssertOnlyUpdateAsyncVisibilityWasCalled(service);
        Assert.NotNull(service.LastVisibilityDto);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(null)]
    public async Task RouteId_OverridesAnyIdSentInTheBody(int? bodyId)
    {
        var service = new FakeDeviceChildrenService();

        var result = await DeviceHandlers.UpdateDevicesVisibilityAsync(service, RouteId, BodyWithEveryPropertyInformed(bodyId: bodyId));

        await HandlerTestSupport.AssertNoContentAsync(result);
        Assert.Equal(RouteId, Assert.IsType<DeviceDTO>(service.LastVisibilityDto).Id);
        AssertOnlyUpdateAsyncVisibilityWasCalled(service);
    }

    [Fact]
    public async Task OnlyIdAndVisibilityReachTheUseCase_EveryOtherPropertyOfTheBodyIsDiscarded()
    {
        var service = new FakeDeviceChildrenService();

        await DeviceHandlers.UpdateDevicesVisibilityAsync(service, RouteId, BodyWithEveryPropertyInformed());

        var received = Assert.IsType<DeviceDTO>(service.LastVisibilityDto);
        Assert.Equal(RouteId, received.Id);
        Assert.Equal("DISABLED", received.Visibility);
        AssertEveryPropertyExceptIdAndVisibilityIsNull(received);
        AssertOnlyUpdateAsyncVisibilityWasCalled(service);
    }

    [Fact]
    public async Task RealisticJsonBody_LeavesOnlyIdAndVisibilityAtTheUseCase()
    {
        var body = BodyAsItArrivesFromJson();

        Assert.Equal(99, body.Id);
        Assert.Equal(5, body.EnterpriseId);
        Assert.Equal(3, body.TypeId);
        Assert.Equal("Makita", body.Brand);
        Assert.Equal("DHP484", body.Model);
        Assert.Equal("SN-0007", body.SerialNumber);
        Assert.Equal(321, body.InventoryNumber);
        Assert.Equal("DISABLED", body.Visibility);

        var service = new FakeDeviceChildrenService();

        var result = await DeviceHandlers.UpdateDevicesVisibilityAsync(service, RouteId, body);

        await HandlerTestSupport.AssertNoContentAsync(result);

        var received = Assert.IsType<DeviceDTO>(service.LastVisibilityDto);
        Assert.Equal(RouteId, received.Id);
        Assert.Equal("DISABLED", received.Visibility);
        AssertEveryPropertyExceptIdAndVisibilityIsNull(received);
        AssertOnlyUpdateAsyncVisibilityWasCalled(service);
    }

    [Fact]
    public async Task TheDelegatedDtoIsANewProjectionAndNotTheBodyItself()
    {
        var service = new FakeDeviceChildrenService();
        var body = BodyWithEveryPropertyInformed();

        await DeviceHandlers.UpdateDevicesVisibilityAsync(service, RouteId, body);

        Assert.NotSame(body, service.LastVisibilityDto);
        Assert.Equal("Makita", body.Brand);
        Assert.Equal(321, body.InventoryNumber);
    }

    [Theory]
    [InlineData("ENABLED")]
    [InlineData("DISABLED")]
    public async Task VisibilityOfTheBody_IsTheOnlyValueHandedToTheUseCase(string visibility)
    {
        var service = new FakeDeviceChildrenService();

        var result = await DeviceHandlers.UpdateDevicesVisibilityAsync(
            service,
            RouteId,
            BodyWithEveryPropertyInformed(visibility: visibility));

        await HandlerTestSupport.AssertNoContentAsync(result);
        AssertOnlyUpdateAsyncVisibilityWasCalled(service);
        Assert.Equal(visibility, Assert.IsType<DeviceDTO>(service.LastVisibilityDto).Visibility);
    }

    [Theory]
    [InlineData("INVALIDA")]
    [InlineData("enabled")]
    [InlineData("")]
    [InlineData(null)]
    public async Task VisibilityOutsideTheAllowedValues_IsTranslatedToBadRequest(string? visibility)
    {
        var service = ServiceThatRejectsVisibility();

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => DeviceHandlers.UpdateDevicesVisibilityAsync(
                service,
                RouteId,
                BodyWithEveryPropertyInformed(visibility: visibility)),
            StatusCodes.Status400BadRequest);

        AssertOnlyUpdateAsyncVisibilityWasCalled(service);
        Assert.Equal(visibility, Assert.IsType<DeviceDTO>(service.LastVisibilityDto).Visibility);
    }

    [Fact]
    public async Task RejectedVisibility_DeliversTheMessageInSpanish()
    {
        var service = ServiceThatRejectsVisibility();

        var exception = await Assert.ThrowsAnyAsync<HexArchApplicationException>(
            () => DeviceHandlers.UpdateDevicesVisibilityAsync(
                service,
                RouteId,
                BodyWithEveryPropertyInformed(visibility: "INVALIDA")));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal(InvalidVisibilityMessage, response.Body);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task NonPositiveRouteId_IsTranslatedToBadRequestByTheUseCase(int routeId)
    {
        var service = new FakeDeviceChildrenService
        {
            ExceptionToThrow = new HexArchApplicationException(NonPositiveIdMessage)
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => DeviceHandlers.UpdateDevicesVisibilityAsync(
                service,
                routeId,
                BodyWithEveryPropertyInformed()),
            StatusCodes.Status400BadRequest);

        AssertOnlyUpdateAsyncVisibilityWasCalled(service);
        Assert.Equal(routeId, Assert.IsType<DeviceDTO>(service.LastVisibilityDto).Id);
    }

    [Fact]
    public async Task UnknownDevice_IsTranslatedToNotFoundAndIsNotRetried()
    {
        var service = new FakeDeviceChildrenService
        {
            ExceptionToThrow = new KeyNotFoundException(
                "No se pudo cambiar la visibilidad del equipo porque no existe un equipo con ese identificador.")
        };

        await HandlerTestSupport.AssertTranslationAsync<KeyNotFoundException>(
            () => DeviceHandlers.UpdateDevicesVisibilityAsync(service, 404, BodyWithEveryPropertyInformed()),
            StatusCodes.Status404NotFound);

        AssertOnlyUpdateAsyncVisibilityWasCalled(service);
        Assert.Equal(404, Assert.IsType<DeviceDTO>(service.LastVisibilityDto).Id);
    }

    [Fact]
    public async Task RecordNotFound_DeliversTheMessageInSpanish()
    {
        var service = new FakeDeviceChildrenService
        {
            ExceptionToThrow = new KeyNotFoundException(
                "No se pudo cambiar la visibilidad del equipo porque no existe un equipo con ese identificador.")
        };

        var exception = await Assert.ThrowsAnyAsync<KeyNotFoundException>(
            () => DeviceHandlers.UpdateDevicesVisibilityAsync(service, 404, BodyWithEveryPropertyInformed()));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal(
            "No se pudo cambiar la visibilidad del equipo porque no existe un equipo con ese identificador.",
            response.Body);
    }

    [Fact]
    public async Task AdminSessionThroughTheRoute_AnswersNoContentAndOnlyIdAndVisibilityTravel()
    {
        var service = new FakeDeviceChildrenService();
        await using var host = DeviceEndpointTestHost.Start(service);

        var response = await host.SendAsync(
            Route,
            Method,
            DeviceEndpointTestHost.SessionToken(AdminAuthorization.AdminRole),
            jsonBody: ConsumerBodyJson,
            idValue: "7");

        Assert.Equal(StatusCodes.Status204NoContent, response.StatusCode);
        Assert.Equal(string.Empty, response.Body);

        var received = Assert.IsType<DeviceDTO>(service.LastVisibilityDto);
        Assert.Equal(7, received.Id);
        Assert.Equal("DISABLED", received.Visibility);
        AssertEveryPropertyExceptIdAndVisibilityIsNull(received);
        AssertOnlyUpdateAsyncVisibilityWasCalled(service);
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
            jsonBody: ConsumerBodyJson,
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
            jsonBody: ConsumerBodyJson,
            idValue: "abc");

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task RequestWithoutSession_IsAnsweredWithUnauthorizedAndTheUseCaseIsNotReached()
    {
        var service = new FakeDeviceChildrenService();
        await using var host = DeviceEndpointTestHost.Start(service);

        var response = await host.SendAsync(Route, Method, bearerToken: null, jsonBody: ConsumerBodyJson, idValue: "7");

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }
}
