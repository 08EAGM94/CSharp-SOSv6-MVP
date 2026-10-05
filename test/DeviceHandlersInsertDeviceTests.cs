using HexArch.Application.DTOs;
using HexArch.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SosMVP.Handlers;
using SosMVP.Security;
using test.Fakes;
using test.Support;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace test;

/// <summary>
/// Subject: <c>DeviceHandlers.InsertDeviceAsync</c> with a fake
/// <c>IEnterpriseChildrenService&lt;DeviceDTO&gt;</c>, plus the route level answers that happen before
/// the handler runs. Covers RF-2.1 to RF-2.6, RF-8.2, RF-8.3, RF-8.5 and the authentication of the
/// route (RF-1.1, RF-1.2).
/// </summary>
public class DeviceHandlersInsertDeviceTests
{
    private const string Route = "/device/";
    private const string Method = "POST";

    private const string ValidBody =
        """
        {
          "enterpriseId": 5,
          "typeId": 2,
          "brand": "Bosch",
          "model": "GBH 18V-26",
          "serialNumber": "SN-0001",
          "inventoryNumber": 120
        }
        """;

    private static DeviceDTO ValidDto(string? visibility = null)
    {
        return new DeviceDTO
        {
            Id = null,
            EnterpriseId = 5,
            TypeId = 2,
            Brand = "Bosch",
            Model = "GBH 18V-26",
            SerialNumber = "SN-0001",
            InventoryNumber = 120,
            Visibility = visibility
        };
    }

    private static DbUpdateException UniqueIndexConflict()
    {
        var sqlServerViolation = new InvalidOperationException(
            "Violation of UNIQUE KEY constraint 'uq_equipo'. SQL error codes 2601 or 2627.");

        return new DbUpdateException(
            "No se pudo registrar el equipo porque ya existe un equipo con ese número de serie.",
            sqlServerViolation);
    }

    private static void AssertOnlyAddAsyncChildWasCalled(FakeDeviceChildrenService service)
    {
        Assert.Equal(1, service.AddAsyncChildCalls);
        Assert.Equal(0, service.GetAsyncChildCalls);
        Assert.Equal(0, service.GetAsyncChildrenByEnterpriseCalls);
        Assert.Equal(0, service.GetAsyncChildrenByEnterForSelectCalls);
        Assert.Equal(0, service.UpdateAsyncChildCalls);
        Assert.Equal(0, service.UpdateAsyncVisibilityCalls);
    }

    [Fact]
    public async Task ValidDevice_IsAnsweredWithCreatedWithoutBodyAndWithoutLocation()
    {
        var service = new FakeDeviceChildrenService();

        var result = await DeviceHandlers.InsertDeviceAsync(service, ValidDto());

        await HandlerTestSupport.AssertCreatedWithoutLocationAsync(result);
    }

    [Fact]
    public async Task ValidDevice_IsDelegatedExactlyOnceToAddAsyncChild()
    {
        var service = new FakeDeviceChildrenService();
        var dto = ValidDto();

        await DeviceHandlers.InsertDeviceAsync(service, dto);

        AssertOnlyAddAsyncChildWasCalled(service);
        Assert.Same(dto, service.LastAddedDto);
    }

    [Fact]
    public async Task ValidDevice_ReachesTheUseCaseWithEveryPropertyUnchanged()
    {
        var service = new FakeDeviceChildrenService();

        await DeviceHandlers.InsertDeviceAsync(service, ValidDto());

        var received = Assert.IsType<DeviceDTO>(service.LastAddedDto);
        Assert.Null(received.Id);
        Assert.Equal(5, received.EnterpriseId);
        Assert.Equal(2, received.TypeId);
        Assert.Equal("Bosch", received.Brand);
        Assert.Equal("GBH 18V-26", received.Model);
        Assert.Equal("SN-0001", received.SerialNumber);
        Assert.Equal(120, received.InventoryNumber);
    }

    [Fact]
    public async Task VisibilitySentInTheBody_IsNotValidatedNorRewrittenByTheHandler()
    {
        var service = new FakeDeviceChildrenService();

        var result = await DeviceHandlers.InsertDeviceAsync(service, ValidDto(visibility: "DISABLED"));

        await HandlerTestSupport.AssertCreatedWithoutLocationAsync(result);
        AssertOnlyAddAsyncChildWasCalled(service);
        Assert.Equal("DISABLED", Assert.IsType<DeviceDTO>(service.LastAddedDto).Visibility);
    }

    [Fact]
    public async Task DuplicateDeviceOnTheUniqueIndex_IsTranslatedToInternalServerErrorAndIsNotRetried()
    {
        var service = new FakeDeviceChildrenService { ExceptionToThrow = UniqueIndexConflict() };

        await HandlerTestSupport.AssertTranslationAsync<DbUpdateException>(
            () => DeviceHandlers.InsertDeviceAsync(service, ValidDto()),
            StatusCodes.Status500InternalServerError);

        AssertOnlyAddAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task DeviceOutsideTheAllowedRules_IsTranslatedToBadRequest()
    {
        var service = new FakeDeviceChildrenService
        {
            ExceptionToThrow = new EntityException("La marca debe tener entre 2 y 50 caracteres.")
        };
        var dto = ValidDto();
        dto.Brand = "B";

        await HandlerTestSupport.AssertTranslationAsync<EntityException>(
            () => DeviceHandlers.InsertDeviceAsync(service, dto),
            StatusCodes.Status400BadRequest);

        AssertOnlyAddAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task BusinessFailure_IsTranslatedToBadRequest()
    {
        var service = new FakeDeviceChildrenService
        {
            ExceptionToThrow = new HexArchApplicationException("El equipo debe pertenecer a una empresa existente.")
        };

        await HandlerTestSupport.AssertTranslationAsync<HexArchApplicationException>(
            () => DeviceHandlers.InsertDeviceAsync(service, ValidDto()),
            StatusCodes.Status400BadRequest);

        AssertOnlyAddAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task EntityRuleViolation_DeliversTheMessageInSpanish()
    {
        var service = new FakeDeviceChildrenService
        {
            ExceptionToThrow = new EntityException("El modelo debe tener entre 5 y 100 caracteres.")
        };

        var exception = await Assert.ThrowsAnyAsync<EntityException>(
            () => DeviceHandlers.InsertDeviceAsync(service, ValidDto()));
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("El modelo debe tener entre 5 y 100 caracteres.", response.Body);
    }

    [Fact]
    public async Task RequestWithABodyThatIsNotADevice_IsAnsweredWithBadRequestAndTheUseCaseIsNotReached()
    {
        var service = new FakeDeviceChildrenService();
        await using var host = DeviceEndpointTestHost.Start(service);

        var response = await host.SendAsync(
            Route,
            Method,
            DeviceEndpointTestHost.SessionToken(AdminAuthorization.AdminRole),
            jsonBody: "{ \"enterpriseId\": ");

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }

    [Fact]
    public async Task ValidRequestThroughTheRoute_IsAnsweredWithCreatedAndReachesTheUseCase()
    {
        var service = new FakeDeviceChildrenService();
        await using var host = DeviceEndpointTestHost.Start(service);

        var response = await host.SendAsync(
            Route,
            Method,
            DeviceEndpointTestHost.SessionToken(AdminAuthorization.AdminRole),
            jsonBody: ValidBody);

        Assert.Equal(StatusCodes.Status201Created, response.StatusCode);
        Assert.Equal(string.Empty, response.Body);
        AssertOnlyAddAsyncChildWasCalled(service);
    }

    [Fact]
    public async Task RequestWithoutSession_IsAnsweredWithUnauthorizedAndTheUseCaseIsNotReached()
    {
        var service = new FakeDeviceChildrenService();
        await using var host = DeviceEndpointTestHost.Start(service);

        var response = await host.SendAsync(Route, Method, bearerToken: null, jsonBody: ValidBody);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal(0, service.TotalCalls);
    }
}
