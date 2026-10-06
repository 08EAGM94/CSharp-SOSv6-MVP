using HexArch.Application.DTOs;
using HexArch.Application.UseCases;
using HexArch.Data.Mappers.DtoToEntity;
using test.Fakes;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace test;

public class BinnacleServiceTests
{
    private static (BinnacleService Service, FakeBinnacleRepository Repository) BuildService()
    {
        var repository = new FakeBinnacleRepository();
        var service = new BinnacleService(repository, new BinnacleDTOtoEntityMapper());
        return (service, repository);
    }

    [Fact]
    public async Task AddAsyncInfo_SendsTheDtoWithTheSystemFixedFieldsEmpty()
    {
        var (service, repository) = BuildService();
        var dto = new BinnacleDTO
        {
            UserId = 42,
            ContactId = 5,
            Service = "Mantenimiento preventivo",
            Amount = 150,
            Status = "finalizado",
            ActivitiesDone = "Actividades realizadas en campo",
            Hints = "Observaciones previas del operador",
            CancelDesc = "Motivo de cancelación del cliente",
            CustomerSignature = "Firma del cliente"
        };

        await service.AddAsyncInfo(dto);

        Assert.Equal(1, repository.AddCalls);
        var entity = repository.LastAddedEntity;
        Assert.NotNull(entity);
        Assert.Null(entity.Status);
        Assert.Null(entity.ActivitiesDone);
        Assert.Null(entity.Hints);
        Assert.Equal(42, entity.UserId);
        Assert.Equal(5, entity.ContactId);
        Assert.Equal("Mantenimiento preventivo", entity.Service);
        Assert.Equal(150f, entity.Amount);
        Assert.Equal("Firma del cliente", entity.CustomerSignature);
    }

    [Fact]
    public async Task AddAsyncInfo_IgnoresCancelDescSoItNeverReachesTheObservations()
    {
        var (service, repository) = BuildService();
        var dto = new BinnacleDTO
        {
            UserId = 42,
            ContactId = 5,
            Service = "Mantenimiento preventivo",
            Amount = 150,
            Hints = null,
            CancelDesc = "Motivo de cancelación del cliente"
        };

        await service.AddAsyncInfo(dto);

        var entity = repository.LastAddedEntity;
        Assert.NotNull(entity);
        Assert.Null(entity.Hints);
    }

    [Theory]
    [InlineData(null, 10)]
    [InlineData(1, null)]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(-1, 10)]
    [InlineData(1, -1)]
    public async Task GetAsyncAllInfo_ThrowsApplicationExceptionWhenPaginationIsNotGreaterThanZero(int? page, int? elemsKey)
    {
        var (service, repository) = BuildService();

        var exception = await Assert.ThrowsAsync<HexArchApplicationException>(
            () => service.GetAsyncAllInfo(new BinnacleDTO { UserId = 7 }, page, elemsKey, null, "FollowupList"));

        Assert.Contains("mayores que cero", exception.Message);
        Assert.Equal(0, repository.GetAllCalls);
    }

    [Fact]
    public async Task GetAsyncAllInfo_WhenPaginationIsValid_DelegatesEveryArgumentToTheRepository()
    {
        var (service, repository) = BuildService();
        var filter = new Dictionary<string, string> { { "visibility", "ENABLED" } };
        var expected = new PaginationResult { ActualPage = 2, TotalPages = 3, TotalRegisters = 12 };
        repository.GetAllResult = expected;

        var result = await service.GetAsyncAllInfo(
            new BinnacleDTO { UserId = 7 },
            2,
            5,
            filter,
            "FollowupList");

        Assert.Same(expected, result);
        Assert.Equal(1, repository.GetAllCalls);
        Assert.NotNull(repository.LastGetAllEntity);
        Assert.Equal(7, repository.LastGetAllEntity.UserId);
        Assert.Null(repository.LastGetAllEntity.Id);
        Assert.Equal(2, repository.LastGetAllPage);
        Assert.Equal(5, repository.LastGetAllElemsKey);
        Assert.Same(filter, repository.LastGetAllBinnFilter);
        Assert.Equal("FollowupList", repository.LastGetAllControllerAction);
    }

    [Fact]
    public async Task UpdateAsyncInfo_ThrowsApplicationExceptionWhenStatusIsNull()
    {
        var (service, repository) = BuildService();
        var dto = new BinnacleDTO { Id = 3, UserId = 9, Status = null };

        var exception = await Assert.ThrowsAsync<HexArchApplicationException>(
            () => service.UpdateAsyncInfo(dto));

        Assert.Contains("en proceso", exception.Message);
        Assert.Equal(0, repository.UpdateCalls);
    }

    [Theory]
    [InlineData("procesando")]
    [InlineData("en Proceso")]
    [InlineData("finalizada")]
    [InlineData("")]
    public async Task UpdateAsyncInfo_ThrowsApplicationExceptionWhenStatusIsNotAdmitted(string status)
    {
        var (service, repository) = BuildService();
        var dto = new BinnacleDTO { Id = 3, UserId = 9, Status = status };

        var exception = await Assert.ThrowsAsync<HexArchApplicationException>(
            () => service.UpdateAsyncInfo(dto));

        Assert.Contains("en proceso", exception.Message);
        Assert.Equal(0, repository.UpdateCalls);
    }

    [Fact]
    public async Task UpdateAsyncInfo_WhenStatusIsValid_DelegatesTheMappedEntity()
    {
        var (service, repository) = BuildService();
        var dto = new BinnacleDTO { Id = 3, UserId = 9, Status = "falta confirmar" };

        await service.UpdateAsyncInfo(dto);

        Assert.Equal(1, repository.UpdateCalls);
        var entity = repository.LastUpdatedEntity;
        Assert.NotNull(entity);
        Assert.Equal(3, entity.Id);
        Assert.Equal(9, entity.UserId);
        Assert.Equal("falta confirmar", entity.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("enable")]
    [InlineData("HIDDEN")]
    public async Task UpdateAsyncVisibility_ThrowsApplicationExceptionWhenVisibilityIsNotAdmitted(string? visibility)
    {
        var (service, repository) = BuildService();
        var dto = new BinnacleDTO { Id = 4, Visibility = visibility };

        var exception = await Assert.ThrowsAsync<HexArchApplicationException>(
            () => service.UpdateAsyncVisibility(dto));

        Assert.Contains("ENABLED o DISABLED", exception.Message);
        Assert.Equal(0, repository.UpdateVisibilityCalls);
    }

    [Fact]
    public async Task UpdateAsyncVisibility_WhenVisibilityIsValid_DelegatesTheDto()
    {
        var (service, repository) = BuildService();
        var dto = new BinnacleDTO { Id = 4, Visibility = "DISABLED" };

        await service.UpdateAsyncVisibility(dto);

        Assert.Equal(1, repository.UpdateVisibilityCalls);
        Assert.Same(dto, repository.LastUpdatedVisibilityDto);
    }
}
