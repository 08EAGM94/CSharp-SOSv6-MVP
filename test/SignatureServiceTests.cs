using HexArch.Application.DTOs;
using HexArch.Application.UseCases;
using HexArch.Data.Mappers.DtoToEntity;
using HexArch.Domain.Entities;
using HexArch.Domain.Exceptions;
using test.Fakes;

namespace test;

public class SignatureServiceTests
{
    private static (SignatureService<UserEntity, UserDTO> Service, FakeSignatureRepository Repository) BuildService()
    {
        var repository = new FakeSignatureRepository();
        var service = new SignatureService<UserEntity, UserDTO>(repository, new UserDTOtoEntityMapper());

        return (service, repository);
    }

    [Fact]
    public async Task InsertSignature_DelegatesTheMappedSignatureToTheRepository()
    {
        var (service, repository) = BuildService();

        await service.InsertSignature(new UserDTO { Id = 7, Signature = "Firma del operador" });

        Assert.Equal(1, repository.InsertSignatureCalls);

        var entity = Assert.IsType<UserEntity>(repository.LastInsertedEntity);
        Assert.Equal(7, entity.Id);
        Assert.Equal("Firma del operador", entity.Signature);
        Assert.Null(entity.Name);
        Assert.Null(entity.Nickname);
        Assert.Null(entity.Password);
    }

    [Fact]
    public async Task InsertSignature_ThrowsEntityExceptionWhenTheSignatureExceeds255CharactersDuringMapping()
    {
        var (service, repository) = BuildService();

        var exception = await Assert.ThrowsAsync<EntityException>(
            () => service.InsertSignature(new UserDTO { Id = 7, Signature = new string('f', 256) }));

        Assert.Equal("La firma debe tener un máximo de 255 caracteres.", exception.Message);
        Assert.Equal(0, repository.InsertSignatureCalls);
    }

    [Fact]
    public async Task InsertSignature_ThrowsEntityExceptionWhenTheIdIsLowerThanOneDuringMapping()
    {
        var (service, repository) = BuildService();

        var exception = await Assert.ThrowsAsync<EntityException>(
            () => service.InsertSignature(new UserDTO { Id = 0, Signature = "Firma del operador" }));

        Assert.Equal("El identificador debe ser un número mayor que cero.", exception.Message);
        Assert.Equal(0, repository.InsertSignatureCalls);
    }

    [Fact]
    public async Task InsertSignature_PropagatesTheKeyNotFoundExceptionOfTheRepository()
    {
        var (service, repository) = BuildService();
        repository.ExceptionToThrow = new KeyNotFoundException("No se encontró un usuario con la información solicitada.");

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.InsertSignature(new UserDTO { Id = 7, Signature = "Firma del operador" }));

        Assert.Equal("No se encontró un usuario con la información solicitada.", exception.Message);
        Assert.Equal(1, repository.InsertSignatureCalls);
    }

    [Fact]
    public async Task GetSignature_PropagatesTheKeyNotFoundExceptionOfTheRepository()
    {
        var (service, repository) = BuildService();
        repository.ExceptionToThrow = new KeyNotFoundException("No se encontró un usuario con la información solicitada.");

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.GetSignature(new UserDTO { Id = 7 }));

        Assert.Equal("No se encontró un usuario con la información solicitada.", exception.Message);
        Assert.Equal(1, repository.GetSignatureCalls);
    }

    [Fact]
    public async Task GetSignature_DelegatesTheMappedIdAndReturnsTheStoredSignature()
    {
        var (service, repository) = BuildService();
        repository.GetResult = new UserDTO { Id = 7, Signature = "Firma almacenada" };

        var result = await service.GetSignature(new UserDTO { Id = 7 });

        Assert.Same(repository.GetResult, result);
        Assert.Equal("Firma almacenada", result.Signature);
        Assert.Equal(1, repository.GetSignatureCalls);

        var entity = Assert.IsType<UserEntity>(repository.LastRequestedEntity);
        Assert.Equal(7, entity.Id);
    }
}
