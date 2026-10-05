using HexArch.Application.DTOs;
using HexArch.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using SosMVP.Handlers;
using SosMVP.Security;
using test.Fakes;
using test.Support;

namespace test;

/// <summary>
/// Subject: <c>DeviceHandlers.GetDevicesByEnterpriseAsync</c> with a fake
/// <c>IEnterpriseChildrenService&lt;DeviceDTO&gt;</c>, plus the route level answers that happen before
/// the handler runs. Covers RF-4.1 to RF-4.7, RF-1.3, RF-1.7 and CE-6b.
/// </summary>
public class DeviceHandlersGetDevicesByEnterpriseTests
{
    private const string Route = "/devicesent/{enterpriseId}";
    private const string Method = "GET";

    private static DeviceDTO StoredDevice(int id, string? visibility)
    {
        return new DeviceDTO
        {
            Id = id,
            EnterpriseId = 5,
            TypeId = 2,
            Brand = "Bosch",
            Model = "GBH 18V-26",
            SerialNumber = $"SN-{id:D4}",
            InventoryNumber = 100 + id,
            Visibility = visibility,
            Type = new TypeDTO { Type = "Herramientas especiales" }
        };
    }

    private static List<DeviceDTO> CompleteListing()
    {
        return
        [
            StoredDevice(7, "ENABLED"),
            StoredDevice(8, "DISABLED")
        ];
    }

    private static void AssertOnlyGetAsyncChildrenByEnterpriseWasCalled(FakeDeviceChildrenService service)
    {
        Assert.Equal(1, service.GetAsyncChildrenByEnterpriseCalls);
        Assert.Equal(0, service.AddAsyncChildCalls);
        Assert.Equal(0, service.GetAsyncChildCalls);
        Assert.Equal(0, service.GetAsyncChildrenByEnterForSelectCalls);
        Assert.Equal(0, service.UpdateAsyncChildCalls);
        Assert.Equal(0, service.UpdateAsyncVisibilityCalls);
    }

    [Fact]
    public async Task DevicesOfTheEnterprise_AreAnsweredWithOkAndEveryPropertyOfEveryRecord()
    {
        var service = new FakeDeviceChildrenService { ChildrenResult = CompleteListing() };

        var result = await DeviceHandlers.GetDevicesByEnterpriseAsync(service, 5);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var body = HandlerTestSupport.ReadJsonBody(response.Body);
        Assert.Equal(2, body.GetArrayLength());

        var first = body[0];
        Assert.Equal(7, first.GetProperty("id").GetInt32());
        Assert.Equal(5, first.GetProperty("enterpriseId").GetInt32());
        Assert.Equal(2, first.GetProperty("typeId").GetInt32());
        Assert.Equal("Bosch", first.GetProperty("brand").GetString());
        Assert.Equal("GBH 18V-26", first.GetProperty("model").GetString());
        Assert.Equal("SN-0007", first.GetProperty("serialNumber").GetString());
        Assert.Equal(107, first.GetProperty("inventoryNumber").GetInt32());
        Assert.Equal("ENABLED", first.GetProperty("visibility").GetString());
        Assert.Equal("Herramientas especiales", first.GetProperty("type").GetProperty("type").GetString());

        AssertOnlyGetAsyncChildrenByEnterpriseWasCalled(service);
    }

    [Fact]
    public async Task RouteEnterpriseId_ReachesTheUseCaseAsTheRequestedEnterprise()
    {
        var service = new FakeDeviceChildrenService { ChildrenResult = CompleteListing() };

        await DeviceHandlers.GetDevicesByEnterpriseAsync(service, 5);

        Assert.Equal(5, Assert.IsType<DeviceDTO>(service.LastEnterpriseDto).EnterpriseId);
    }

    [Fact]
    public async Task OnlyTheRouteEnterpriseIdReachesTheUseCase()
    {
        var service = new FakeDeviceChildrenService { ChildrenResult = CompleteListing() };

        await DeviceHandlers.GetDevicesByEnterpriseAsync(service, 5);

        var requested = Assert.IsType<DeviceDTO>(service.LastEnterpriseDto);
        Assert.Equal(5, requested.EnterpriseId);
        Assert.Null(requested.Id);
        Assert.Null(requested.TypeId);
        Assert.Null(requested.Brand);
        Assert.Null(requested.Model);
        Assert.Null(requested.SerialNumber);
        Assert.Null(requested.InventoryNumber);
        Assert.Null(requested.Visibility);
        Assert.Null(requested.Type);
    }

    [Fact]
    public async Task Listing_IncludesDisabledRecordsAndIsNotFilteredByTheHandler()
    {
        var service = new FakeDeviceChildrenService { ChildrenResult = CompleteListing() };

        var result = await DeviceHandlers.GetDevicesByEnterpriseAsync(service, 5);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var body = HandlerTestSupport.ReadJsonBody(response.Body);
        Assert.Equal(2, body.GetArrayLength());
        Assert.Equal("ENABLED", body[0].GetProperty("visibility").GetString());
        Assert.Equal("DISABLED", body[1].GetProperty("visibility").GetString());
        Assert.Equal("SN-0008", body[1].GetProperty("serialNumber").GetString());

        Assert.Null(Assert.IsType<DeviceDTO>(service.LastEnterpriseDto).Visibility);
        AssertOnlyGetAsyncChildrenByEnterpriseWasCalled(service);
    }

    [Fact]
    public async Task EnterpriseWithoutDevices_IsAnsweredWithOkAndAnEmptyCollection()
    {
        var service = new FakeDeviceChildrenService { ChildrenResult = [] };

        var result = await DeviceHandlers.GetDevicesByEnterpriseAsync(service, 5);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(0, HandlerTestSupport.ReadJsonBody(response.Body).GetArrayLength());
        AssertOnlyGetAsyncChildrenByEnterpriseWasCalled(service);
    }

    [Fact]
    public async Task BusinessFailure_IsTranslatedToBadRequest()
    {
        var service = new FakeDeviceChildrenService
        {
            ExceptionToThrow = new EntityException("El identificador de la empresa debe ser un número mayor que cero.")
        };

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => DeviceHandlers.GetDevicesByEnterpriseAsync(service, 0),
            StatusCodes.Status400BadRequest);

        AssertOnlyGetAsyncChildrenByEnterpriseWasCalled(service);
    }

    [Fact]
    public async Task TheRouteIsReservedForAdministrators()
    {
        await using var host = DeviceEndpointTestHost.Start(new FakeDeviceChildrenService());

        var authorizeData = host.Find(Route, Method).Metadata.GetOrderedMetadata<IAuthorizeData>().ToList();

        Assert.NotEmpty(authorizeData);
        Assert.Contains(authorizeData, data => data.Policy == AdminAuthorization.PolicyName);
    }

    [Fact]
    public async Task AdminSessionThroughTheRoute_AnswersOkWithTheCompleteListingIncludingDisabled()
    {
        var service = new FakeDeviceChildrenService { ChildrenResult = CompleteListing() };
        await using var host = DeviceEndpointTestHost.Start(service);

        var response = await host.SendAsync(
            Route,
            Method,
            DeviceEndpointTestHost.SessionToken(AdminAuthorization.AdminRole),
            enterpriseIdValue: "5");

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var body = HandlerTestSupport.ReadJsonBody(response.Body);
        Assert.Equal(2, body.GetArrayLength());
        Assert.Equal("DISABLED", body[1].GetProperty("visibility").GetString());
        Assert.Equal(5, Assert.IsType<DeviceDTO>(service.LastEnterpriseDto).EnterpriseId);
        AssertOnlyGetAsyncChildrenByEnterpriseWasCalled(service);
    }

    [Fact]
    public async Task SessionThatIsNotAdmin_IsAnsweredWithForbiddenAndTheUseCaseIsNotReached()
    {
        var service = new FakeDeviceChildrenService { ChildrenResult = CompleteListing() };
        await using var host = DeviceEndpointTestHost.Start(service);

        var response = await host.SendAsync(
            Route,
            Method,
            DeviceEndpointTestHost.SessionToken("user"),
            enterpriseIdValue: "5");

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task NonNumericEnterpriseIdInTheRoute_IsAnsweredWithBadRequestAndTheUseCaseIsNotReached()
    {
        var service = new FakeDeviceChildrenService { ChildrenResult = CompleteListing() };
        await using var host = DeviceEndpointTestHost.Start(service);

        var response = await host.SendAsync(
            Route,
            Method,
            DeviceEndpointTestHost.SessionToken(AdminAuthorization.AdminRole),
            enterpriseIdValue: "cinco");

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task RequestWithoutSession_IsAnsweredWithUnauthorizedAndTheUseCaseIsNotReached()
    {
        var service = new FakeDeviceChildrenService { ChildrenResult = CompleteListing() };
        await using var host = DeviceEndpointTestHost.Start(service);

        var response = await host.SendAsync(Route, Method, bearerToken: null, enterpriseIdValue: "5");

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }
}
