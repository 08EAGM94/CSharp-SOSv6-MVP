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
/// Subject: <c>DeviceHandlers.GetDevicesByEnterpriseForSelectAsync</c> with a fake
/// <c>IEnterpriseChildrenService&lt;DeviceDTO&gt;</c>, plus the route level answers that happen before
/// the handler runs. Covers RF-7.1 to RF-7.6, RF-8.3, RF-8.4, RF-1.6 and CE-6, CE-6c.
/// </summary>
public class DeviceHandlersGetDevicesByEnterpriseForSelectTests
{
    private const string Route = "/devicesentsct/{enterpriseId}";
    private const string Method = "GET";

    private static DeviceDTO DeviceForSelect(int id, string? visibility = null)
    {
        return new DeviceDTO
        {
            Id = id,
            Brand = "Bosch",
            SerialNumber = $"SN-{id:D4}",
            Visibility = visibility
        };
    }

    private static void AssertOnlyGetAsyncChildrenByEnterForSelectWasCalled(FakeDeviceChildrenService service)
    {
        Assert.Equal(1, service.GetAsyncChildrenByEnterForSelectCalls);
        Assert.Equal(0, service.GetAsyncChildrenByEnterpriseCalls);
        Assert.Equal(0, service.GetAsyncChildCalls);
        Assert.Equal(0, service.AddAsyncChildCalls);
        Assert.Equal(0, service.UpdateAsyncChildCalls);
        Assert.Equal(0, service.UpdateAsyncVisibilityCalls);
    }

    [Fact]
    public async Task EnabledDevices_AreAnsweredWithOkAndTheCollectionReturnedByTheUseCase()
    {
        var service = new FakeDeviceChildrenService
        {
            ForSelectResult = [DeviceForSelect(1), DeviceForSelect(5)]
        };

        var result = await DeviceHandlers.GetDevicesByEnterpriseForSelectAsync(service, 5);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var items = HandlerTestSupport.ReadJsonBody(response.Body).EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal(1, items[0].GetProperty("id").GetInt32());
        Assert.Equal("Bosch", items[0].GetProperty("brand").GetString());
        Assert.Equal("SN-0001", items[0].GetProperty("serialNumber").GetString());
        Assert.Equal(5, items[1].GetProperty("id").GetInt32());
        Assert.Equal("SN-0005", items[1].GetProperty("serialNumber").GetString());

        AssertOnlyGetAsyncChildrenByEnterForSelectWasCalled(service);
    }

    [Fact]
    public async Task RouteEnterpriseId_ReachesTheUseCaseAsTheRequestedEnterprise()
    {
        var service = new FakeDeviceChildrenService { ForSelectResult = [DeviceForSelect(1)] };

        await DeviceHandlers.GetDevicesByEnterpriseForSelectAsync(service, 5);

        Assert.Equal(5, Assert.IsType<DeviceDTO>(service.LastForSelectDto).EnterpriseId);
    }

    [Fact]
    public async Task OnlyTheRouteEnterpriseIdReachesTheUseCase()
    {
        var service = new FakeDeviceChildrenService { ForSelectResult = [DeviceForSelect(1)] };

        await DeviceHandlers.GetDevicesByEnterpriseForSelectAsync(service, 5);

        var requested = Assert.IsType<DeviceDTO>(service.LastForSelectDto);
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
    public async Task Listing_CarriesOnlyIdBrandAndSerialNumberAndNoVisibility()
    {
        var service = new FakeDeviceChildrenService { ForSelectResult = [DeviceForSelect(1)] };

        var result = await DeviceHandlers.GetDevicesByEnterpriseForSelectAsync(service, 5);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var item = HandlerTestSupport.ReadJsonBody(response.Body)[0];
        Assert.Equal(1, item.GetProperty("id").GetInt32());
        Assert.Equal("Bosch", item.GetProperty("brand").GetString());
        Assert.Equal("SN-0001", item.GetProperty("serialNumber").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("visibility").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("enterpriseId").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("typeId").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("model").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("inventoryNumber").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("type").ValueKind);
    }

    [Fact]
    public async Task EnabledCollection_ReachesTheResponseIntactWithoutFilteringNorAddingRecords()
    {
        var service = new FakeDeviceChildrenService
        {
            ForSelectResult = [DeviceForSelect(1), DeviceForSelect(5), DeviceForSelect(9)]
        };

        var result = await DeviceHandlers.GetDevicesByEnterpriseForSelectAsync(service, 5);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var items = HandlerTestSupport.ReadJsonBody(response.Body).EnumerateArray().ToList();
        Assert.Equal(3, items.Count);
        Assert.Equal([1, 5, 9], items.Select(item => item.GetProperty("id").GetInt32()).ToArray());
        Assert.All(items, item => Assert.Equal(JsonValueKind.Null, item.GetProperty("visibility").ValueKind));
        AssertOnlyGetAsyncChildrenByEnterForSelectWasCalled(service);
    }

    [Fact]
    public async Task TheVisibilityFilterLivesInTheRepositoryAndNotInTheHandler()
    {
        var service = new FakeDeviceChildrenService
        {
            ForSelectResult = [DeviceForSelect(1), DeviceForSelect(2, visibility: "DISABLED")]
        };

        var result = await DeviceHandlers.GetDevicesByEnterpriseForSelectAsync(service, 5);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);

        var items = HandlerTestSupport.ReadJsonBody(response.Body).EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal(2, items[1].GetProperty("id").GetInt32());
        Assert.Equal("DISABLED", items[1].GetProperty("visibility").GetString());
        Assert.Null(Assert.IsType<DeviceDTO>(service.LastForSelectDto).Visibility);
    }

    [Fact]
    public async Task EnterpriseWithoutEnabledDevices_IsAnsweredWithOkAndAnEmptyCollection()
    {
        var service = new FakeDeviceChildrenService { ForSelectResult = [] };

        var result = await DeviceHandlers.GetDevicesByEnterpriseForSelectAsync(service, 5);

        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal("[]", response.Body);
        Assert.Equal(JsonValueKind.Array, HandlerTestSupport.ReadJsonBody(response.Body).ValueKind);
        AssertOnlyGetAsyncChildrenByEnterForSelectWasCalled(service);
    }

    [Fact]
    public async Task BusinessFailureOfTheUseCase_IsTranslatedToBadRequest()
    {
        var service = new FakeDeviceChildrenService
        {
            ExceptionToThrow = new HexArchApplicationException("No fue posible consultar los equipos habilitados.\n")
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => DeviceHandlers.GetDevicesByEnterpriseForSelectAsync(service, 5),
            StatusCodes.Status400BadRequest);

        AssertOnlyGetAsyncChildrenByEnterForSelectWasCalled(service);
    }

    [Fact]
    public async Task GenericUseCaseFailure_IsTranslatedToBadRequest()
    {
        var service = new FakeDeviceChildrenService
        {
            ExceptionToThrow = new InvalidOperationException("No fue posible consultar los equipos habilitados.")
        };

        await HandlerTestSupport.AssertTranslationAsync<InvalidOperationException>(
            () => DeviceHandlers.GetDevicesByEnterpriseForSelectAsync(service, 5),
            StatusCodes.Status400BadRequest);

        AssertOnlyGetAsyncChildrenByEnterForSelectWasCalled(service);
    }

    [Fact]
    public async Task BusinessFailure_DeliversTheMessageInSpanish()
    {
        var service = new FakeDeviceChildrenService
        {
            ExceptionToThrow = new HexArchApplicationException("No fue posible consultar los equipos habilitados.\n")
        };

        var exception = await Assert.ThrowsAnyAsync<HexArchApplicationException>(
            () => DeviceHandlers.GetDevicesByEnterpriseForSelectAsync(service, 5));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("No fue posible consultar los equipos habilitados.\n", response.Body);
    }

    [Fact]
    public async Task SessionThatIsNotAdmin_OpensTheRouteAndAnswersOk()
    {
        var service = new FakeDeviceChildrenService { ForSelectResult = [DeviceForSelect(1)] };
        await using var host = DeviceEndpointTestHost.Start(service);

        var response = await host.SendAsync(
            Route,
            Method,
            DeviceEndpointTestHost.SessionToken("user"),
            enterpriseIdValue: "5");

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, response.StatusCode);

        var items = HandlerTestSupport.ReadJsonBody(response.Body).EnumerateArray().ToList();
        Assert.Single(items);
        Assert.Equal(1, items[0].GetProperty("id").GetInt32());
        Assert.Equal("SN-0001", items[0].GetProperty("serialNumber").GetString());
        Assert.Equal(5, Assert.IsType<DeviceDTO>(service.LastForSelectDto).EnterpriseId);
        AssertOnlyGetAsyncChildrenByEnterForSelectWasCalled(service);
    }

    [Fact]
    public async Task NonNumericEnterpriseIdInTheRoute_IsAnsweredWithBadRequestAndTheUseCaseIsNotReached()
    {
        var service = new FakeDeviceChildrenService { ForSelectResult = [DeviceForSelect(1)] };
        await using var host = DeviceEndpointTestHost.Start(service);

        var response = await host.SendAsync(
            Route,
            Method,
            DeviceEndpointTestHost.SessionToken("user"),
            enterpriseIdValue: "cinco");

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task RequestWithoutSession_IsAnsweredWithUnauthorizedAndTheUseCaseIsNotReached()
    {
        var service = new FakeDeviceChildrenService { ForSelectResult = [DeviceForSelect(1)] };
        await using var host = DeviceEndpointTestHost.Start(service);

        var response = await host.SendAsync(Route, Method, bearerToken: null, enterpriseIdValue: "5");

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }
}
