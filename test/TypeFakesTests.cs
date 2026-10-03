using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.Abstractions.SecondaryPorts;
using HexArch.Application.DTOs;
using HexArch.Application.UseCases;
using HexArch.Data.Mappers.DtoToEntity;
using HexArch.Domain.Entities;
using HexArch.Domain.Exceptions;
using test.Fakes;

namespace test;

public class TypeFakesTests
{
    private static (CommonService<TypeEntity, TypeDTO> Service, FakeTypeRepository Repository, FakeTypeMapper Mapper) BuildCommonService()
    {
        var repository = new FakeTypeRepository();
        var typeMapper = new FakeTypeMapper { MapFunc = dto => new TypeDTOtoEntityMapper().Map(dto) };
        var contactMapper = new FakeContactMapper();

        return (new CommonService<TypeEntity, TypeDTO>(repository, typeMapper, contactMapper), repository, typeMapper);
    }

    [Fact]
    public void FakeTypeRepository_ExposesBothSecondaryPortContracts()
    {
        var fake = new FakeTypeRepository();

        Assert.IsAssignableFrom<IRepository<TypeEntity, TypeDTO>>(fake);
        Assert.IsAssignableFrom<ISelectRepository<TypeDTO>>(fake);
    }

    [Fact]
    public void FakeTypeMapper_DelegatesToTheInjectedMapFunc()
    {
        var mapper = new FakeTypeMapper { MapFunc = dto => new TypeEntity { Type = dto.Type } };
        var dto = new TypeDTO { Type = "Mapeado" };

        var entity = mapper.Map(dto);

        Assert.Equal(1, mapper.MapCalls);
        Assert.Same(dto, mapper.LastMappedDto);
        Assert.Equal("Mapeado", entity.Type);
    }

    [Fact]
    public void FakeTypeSelectService_ExposesTheSelectContractAndNotTheUserServiceOne()
    {
        var selectService = new FakeTypeSelectService();

        Assert.IsAssignableFrom<ISelectService<TypeDTO>>(selectService);
        Assert.IsNotAssignableFrom<IUserService>(selectService);
    }

    [Fact]
    public async Task CommonServiceAddAsyncInfo_LetsTheTestReadTheExactEntityTheUseCaseReceived()
    {
        var (service, repository, mapper) = BuildCommonService();
        var dto = new TypeDTO { Type = "Tipo de prueba" };

        await service.AddAsyncInfo(dto);

        Assert.Equal(1, repository.AddAsyncInfoCalls);
        Assert.Equal(1, mapper.MapCalls);
        Assert.Same(dto, mapper.LastMappedDto);
        Assert.Null(repository.LastAddedContact);

        var entity = Assert.IsType<TypeEntity>(repository.LastAddedEntity);
        Assert.Null(entity.Id);
        Assert.Equal("Tipo de prueba", entity.Type);
    }

    [Fact]
    public async Task CommonServiceGetAsyncInfo_ReturnsTheProgrammedResultAndRecordsTheRequestedEntity()
    {
        var (service, repository, _) = BuildCommonService();
        var stored = new TypeDTO { Id = 7, Type = "Tipo almacenado", Visibility = "ENABLED" };
        repository.InfoResult = stored;

        var result = await service.GetAsyncInfo(new TypeDTO { Id = 7 });

        Assert.Same(stored, result);
        Assert.Equal(1, repository.GetAsyncInfoCalls);
        Assert.Equal(7, Assert.IsType<TypeEntity>(repository.LastRequestedEntity).Id);
    }

    [Fact]
    public async Task CommonServiceGetAsyncAllInfo_ReturnsEveryStoredTypeIncludingTheDisabledOnes()
    {
        var (service, repository, _) = BuildCommonService();
        var stored = new List<TypeDTO>
        {
            new() { Id = 1, Type = "Habilitado", Visibility = "ENABLED" },
            new() { Id = 2, Type = "Deshabilitado", Visibility = "DISABLED" }
        };
        repository.AllResult = stored;

        var result = await service.GetAsyncAllInfo();

        Assert.Same(stored, result);
        Assert.Equal(1, repository.GetAsyncAllInfoCalls);
        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task CommonServiceUpdateAsyncInfo_RecordsTheMappedEntity()
    {
        var (service, repository, _) = BuildCommonService();

        await service.UpdateAsyncInfo(new TypeDTO { Id = 7, Type = "Tipo actualizado" });

        Assert.Equal(1, repository.UpdateAsyncInfoCalls);

        var entity = Assert.IsType<TypeEntity>(repository.LastUpdatedEntity);
        Assert.Equal(7, entity.Id);
        Assert.Equal("Tipo actualizado", entity.Type);
    }

    [Fact]
    public async Task CommonServiceUpdateAsyncVisibility_RecordsTheExactDtoReceivedByTheUseCase()
    {
        var (service, repository, _) = BuildCommonService();
        var dto = new TypeDTO { Id = 7, Visibility = "DISABLED" };

        await service.UpdateAsyncVisibility(dto);

        Assert.Equal(1, repository.UpdateAsyncVisibilityCalls);
        Assert.Same(dto, repository.LastVisibilityDto);
    }

    [Fact]
    public async Task CommonServicePropagatesTheProgrammedRepositoryFailure()
    {
        var (service, repository, _) = BuildCommonService();
        repository.ExceptionToThrow = new EntityException("El tipo debe tener entre 5 y 50 caracteres.");

        var exception = await Assert.ThrowsAsync<EntityException>(() => service.AddAsyncInfo(new TypeDTO { Type = "Tipo válido" }));

        Assert.Equal(1, repository.AddAsyncInfoCalls);
        Assert.Contains("entre 5 y 50", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SelectServiceGetAsyncInfoForSelects_ReturnsTheProgrammedResultThroughTheSecondaryPort()
    {
        var repository = new FakeTypeRepository();
        var stored = new List<TypeDTO> { new() { Id = 1, Type = "Habilitado", Visibility = "ENABLED" } };
        repository.SelectResult = stored;
        var service = new SelectService<TypeDTO>(repository);

        var result = await service.GetAsyncInfoForSelects();

        Assert.Same(stored, result);
        Assert.Equal(1, repository.GetAsyncInfoForSelectsCalls);
    }

    [Fact]
    public async Task FakeTypeCommonService_RecordsTheDtoReceivedByEveryMethod()
    {
        var service = new FakeTypeCommonService();
        var added = new TypeDTO { Type = "Nuevo tipo" };
        var requested = new TypeDTO { Id = 3 };
        var updated = new TypeDTO { Id = 4, Type = "Tipo editado" };
        var visibility = new TypeDTO { Id = 5, Visibility = "DISABLED" };

        await service.AddAsyncInfo(added);
        await service.GetAsyncInfo(requested);
        await service.GetAsyncAllInfo();
        await service.UpdateAsyncInfo(updated);
        await service.UpdateAsyncVisibility(visibility);

        Assert.Equal(1, service.AddAsyncInfoCalls);
        Assert.Same(added, service.LastAddedDto);
        Assert.Null(service.LastAddedContact);
        Assert.Equal(1, service.GetAsyncInfoCalls);
        Assert.Same(requested, service.LastRequestedDto);
        Assert.Equal(1, service.GetAsyncAllInfoCalls);
        Assert.Equal(1, service.UpdateAsyncInfoCalls);
        Assert.Same(updated, service.LastUpdatedDto);
        Assert.Equal(1, service.UpdateAsyncVisibilityCalls);
        Assert.Same(visibility, service.LastVisibilityDto);
    }

    [Fact]
    public async Task FakeTypeCommonService_ReturnsTheProgrammedResults()
    {
        var service = new FakeTypeCommonService();
        var info = new TypeDTO { Id = 1, Type = "Buscado", Visibility = "ENABLED" };
        var all = new List<TypeDTO> { info, new() { Id = 2, Type = "Otro tipo", Visibility = "DISABLED" } };
        service.InfoResult = info;
        service.AllResult = all;

        Assert.Same(info, await service.GetAsyncInfo(new TypeDTO { Id = 1 }));
        Assert.Same(all, await service.GetAsyncAllInfo());
    }

    [Fact]
    public async Task FakeTypeCommonService_PropagatesTheProgrammedFailure()
    {
        var service = new FakeTypeCommonService { ExceptionToThrow = new KeyNotFoundException("No existe un tipo con ese identificador.") };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateAsyncInfo(new TypeDTO { Id = 404 }));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsyncInfo(new TypeDTO { Id = 404 }));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsyncAllInfo());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AddAsyncInfo(new TypeDTO { Type = "Nuevo tipo" }));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateAsyncVisibility(new TypeDTO { Id = 1, Visibility = "ENABLED" }));
    }

    [Fact]
    public async Task FakeTypeSelectService_CountsTheCallAndReturnsTheProgrammedResult()
    {
        var service = new FakeTypeSelectService();
        var stored = new List<TypeDTO> { new() { Id = 1, Type = "Habilitado", Visibility = "ENABLED" } };
        service.SelectResult = stored;

        var result = await service.GetAsyncInfoForSelects();

        Assert.Equal(1, service.GetAsyncInfoForSelectsCalls);
        Assert.Same(stored, result);
    }

    [Fact]
    public async Task FakeTypeSelectService_PropagatesTheProgrammedFailure()
    {
        var service = new FakeTypeSelectService { ExceptionToThrow = new InvalidOperationException("fallo programado") };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAsyncInfoForSelects());
        Assert.Equal(1, service.GetAsyncInfoForSelectsCalls);
    }
}