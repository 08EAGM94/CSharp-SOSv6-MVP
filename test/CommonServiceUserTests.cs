using HexArch.Application.DTOs;
using HexArch.Application.UseCases;
using HexArch.Data.Mappers.DtoToEntity;
using HexArch.Domain.Entities;
using test.Fakes;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace test;

public class CommonServiceUserTests
{
    private static (CommonService<UserEntity, UserDTO> Service, FakeCommonRepository Repository, FakeContactMapper ContactMapper) BuildService()
    {
        var repository = new FakeCommonRepository();
        var userMapper = new FakeUserMapper { MapFunc = dto => new UserDTOtoEntityMapper().Map(dto) };
        var contactMapper = new FakeContactMapper { MapFunc = dto => new ContactDTOtoEntityMapper().Map(dto) };

        return (new CommonService<UserEntity, UserDTO>(repository, userMapper, contactMapper), repository, contactMapper);
    }

    private static UserDTO ValidDto()
    {
        return new UserDTO
        {
            Name = "Nombre Completo",
            Surname = "Apellido Completo Del Usuario",
            Nickname = "nuevoUsuario",
            Password = "clave-de-prueba-123",
            Role = "user",
            Signature = "Firma del usuario"
        };
    }

    [Fact]
    public async Task AddAsyncInfo_MapsTheDtoAndDelegatesToTheRepository()
    {
        var (service, repository, _) = BuildService();
        var dto = ValidDto();

        await service.AddAsyncInfo(dto);

        Assert.Equal(1, repository.AddAsyncInfoCalls);
        Assert.Null(repository.LastAddedContact);

        var entity = Assert.IsType<UserEntity>(repository.LastAddedEntity);
        Assert.Equal(dto.Name, entity.Name);
        Assert.Equal(dto.Nickname, entity.Nickname);
        Assert.Equal(dto.Password, entity.Password);
        Assert.Equal(dto.Role, entity.Role);
    }

    [Fact]
    public async Task AddAsyncInfo_MapsTheContactWhenItArrives()
    {
        var (service, repository, contactMapper) = BuildService();
        var contact = new ContactDTO { FullName = "Nombre Completo Del Contacto" };

        await service.AddAsyncInfo(ValidDto(), contact);

        Assert.Equal(1, contactMapper.MapCalls);
        Assert.NotNull(repository.LastAddedContact);
        Assert.Equal(contact.FullName, repository.LastAddedContact!.FullName);
    }

    [Fact]
    public async Task AddAsyncInfo_PropagatesTheRepositoryFailure()
    {
        var (service, repository, _) = BuildService();
        repository.ExceptionToThrow = new Microsoft.EntityFrameworkCore.DbUpdateException("El alias ya está registrado.");

        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => service.AddAsyncInfo(ValidDto()));
    }

    [Fact]
    public async Task GetAsyncInfo_MapsTheDtoAndReturnsWhatTheRepositoryReturns()
    {
        var (service, repository, _) = BuildService();
        var stored = new UserDTO { Id = 7, Name = "Nombre Completo" };
        repository.InfoResult = stored;

        var result = await service.GetAsyncInfo(new UserDTO { Id = 7 });

        Assert.Same(stored, result);
        Assert.Equal(7, Assert.IsType<UserEntity>(repository.LastRequestedEntity).Id);
    }

    [Fact]
    public async Task GetAsyncInfo_PropagatesTheNotFoundFailure()
    {
        var (service, repository, _) = BuildService();
        repository.ExceptionToThrow = new KeyNotFoundException("No se encontró un usuario con la información solicitada.");

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsyncInfo(new UserDTO { Id = 404 }));
    }

    [Fact]
    public async Task GetAsyncAllInfo_DelegatesToTheRepositoryWithoutAnyFilter()
    {
        var (service, repository, _) = BuildService();
        var stored = new List<UserDTO> { new() { Id = 1, Name = "Nombre Completo" } };
        repository.AllResult = stored;

        var result = await service.GetAsyncAllInfo();

        Assert.Same(stored, result);
        Assert.Equal(1, repository.GetAsyncAllInfoCalls);
    }

    [Fact]
    public async Task UpdateAsyncInfo_MapsTheDtoAndDelegatesToTheRepository()
    {
        var (service, repository, _) = BuildService();
        var dto = ValidDto();
        dto.Id = 7;

        await service.UpdateAsyncInfo(dto);

        Assert.Equal(1, repository.UpdateAsyncInfoCalls);
        Assert.Equal(7, Assert.IsType<UserEntity>(repository.LastUpdatedEntity).Id);
    }

    [Fact]
    public async Task UpdateAsyncInfo_PropagatesTheNotFoundFailure()
    {
        var (service, repository, _) = BuildService();
        repository.ExceptionToThrow = new KeyNotFoundException("No se pudo actualizar la información del usuario porque no existe un usuario con ese identificador.");

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateAsyncInfo(new UserDTO { Id = 404 }));
    }

    [Theory]
    [InlineData(7, "ENABLED")]
    [InlineData(7, "DISABLED")]
    public async Task UpdateAsyncVisibility_AcceptsTheAllowedVisibilityValues(int id, string visibility)
    {
        var (service, repository, _) = BuildService();

        await service.UpdateAsyncVisibility(new UserDTO { Id = id, Visibility = visibility });

        Assert.Equal(1, repository.UpdateAsyncVisibilityCalls);
        Assert.Equal(id, repository.LastVisibilityDto!.Id);
        Assert.Equal(visibility, repository.LastVisibilityDto.Visibility);
    }

    [Theory]
    [InlineData(0, "ENABLED")]
    [InlineData(-1, "ENABLED")]
    [InlineData(null, "ENABLED")]
    public async Task UpdateAsyncVisibility_RejectsAnIdentifierOutsideTheBusinessRules(int? id, string? visibility)
    {
        var (service, repository, _) = BuildService();

        var exception = await Assert.ThrowsAsync<HexArchApplicationException>(
            () => service.UpdateAsyncVisibility(new UserDTO { Id = id, Visibility = visibility }));

        Assert.Contains("identificador", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, repository.UpdateAsyncVisibilityCalls);
    }

    [Theory]
    [InlineData(7, "HABILITADO")]
    [InlineData(7, "enabled")]
    [InlineData(7, "")]
    [InlineData(7, null)]
    public async Task UpdateAsyncVisibility_RejectsAVisibilityOutsideTheBusinessRules(int? id, string? visibility)
    {
        var (service, repository, _) = BuildService();

        var exception = await Assert.ThrowsAsync<HexArchApplicationException>(
            () => service.UpdateAsyncVisibility(new UserDTO { Id = id, Visibility = visibility }));

        Assert.Contains("visibilidad", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, repository.UpdateAsyncVisibilityCalls);
    }

    [Fact]
    public async Task UpdateAsyncVisibility_PropagatesTheNotFoundFailure()
    {
        var (service, repository, _) = BuildService();
        repository.ExceptionToThrow = new KeyNotFoundException("No se pudo cambiar la visibilidad del usuario porque no existe un usuario con ese identificador.");

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateAsyncVisibility(new UserDTO { Id = 404, Visibility = "ENABLED" }));
    }
}