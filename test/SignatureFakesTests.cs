using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.Abstractions.SecondaryPorts;
using HexArch.Application.DTOs;
using HexArch.Domain.Entities;
using HexArch.Domain.Exceptions;
using test.Fakes;

namespace test;

public class SignatureFakesTests
{
    [Fact]
    public void FakeSignatureService_ExposesThePrimaryPortContract()
    {
        var fake = new FakeSignatureService();

        Assert.IsAssignableFrom<ISignatureService<UserDTO>>(fake);
        Assert.IsNotAssignableFrom<ICommonService<UserDTO>>(fake);
    }

    [Fact]
    public void FakeSignatureRepository_ExposesTheSecondaryPortContract()
    {
        var fake = new FakeSignatureRepository();

        Assert.IsAssignableFrom<ISignatureRepository<UserEntity, UserDTO>>(fake);
        Assert.IsNotAssignableFrom<IRepository<UserEntity, UserDTO>>(fake);
    }

    [Fact]
    public async Task FakeSignatureService_RecordsTheExactDtoReceivedByEveryMethod()
    {
        var service = new FakeSignatureService();
        var inserted = new UserDTO { Id = 7, Signature = "Firma escrita" };
        var requested = new UserDTO { Id = 8 };

        await service.InsertSignature(inserted);
        await service.GetSignature(requested);

        Assert.Equal(1, service.InsertSignatureCalls);
        Assert.Same(inserted, service.LastInsertedDto);
        Assert.Equal(1, service.GetSignatureCalls);
        Assert.Same(requested, service.LastRequestedDto);
    }

    [Fact]
    public async Task FakeSignatureService_ReturnsTheProgrammedResult()
    {
        var service = new FakeSignatureService();
        var stored = new UserDTO { Id = 7, Signature = "Firma almacenada" };
        service.GetResult = stored;

        var result = await service.GetSignature(new UserDTO { Id = 7 });

        Assert.Same(stored, result);
    }

    [Fact]
    public async Task FakeSignatureService_PropagatesTheProgrammedFailure()
    {
        var service = new FakeSignatureService
        {
            ExceptionToThrow = new KeyNotFoundException("No existe un usuario con ese identificador.")
        };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.InsertSignature(new UserDTO { Id = 404 }));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetSignature(new UserDTO { Id = 404 }));
        Assert.Equal(1, service.InsertSignatureCalls);
        Assert.Equal(1, service.GetSignatureCalls);
    }

    [Fact]
    public async Task FakeSignatureRepository_RecordsTheExactEntityReceivedByEveryMethod()
    {
        var repository = new FakeSignatureRepository();
        var inserted = new UserEntity { Id = 7, Signature = "Firma escrita" };
        var requested = new UserEntity { Id = 8 };

        await repository.InsertSignature(inserted);
        await repository.GetSignature(requested);

        Assert.Equal(1, repository.InsertSignatureCalls);
        Assert.Same(inserted, repository.LastInsertedEntity);
        Assert.Equal(1, repository.GetSignatureCalls);
        Assert.Same(requested, repository.LastRequestedEntity);
    }

    [Fact]
    public async Task FakeSignatureRepository_ReturnsTheProgrammedResult()
    {
        var repository = new FakeSignatureRepository();
        var stored = new UserDTO { Id = 7, Signature = "Firma almacenada" };
        repository.GetResult = stored;

        var result = await repository.GetSignature(new UserEntity { Id = 7 });

        Assert.Same(stored, result);
    }

    [Fact]
    public async Task FakeSignatureRepository_PropagatesTheProgrammedFailure()
    {
        var repository = new FakeSignatureRepository
        {
            ExceptionToThrow = new EntityException("La firma no puede superar los 255 caracteres.")
        };

        var exception = await Assert.ThrowsAsync<EntityException>(
            () => repository.InsertSignature(new UserEntity { Id = 7 }));

        Assert.Equal(1, repository.InsertSignatureCalls);
        Assert.Contains("255", exception.Message, StringComparison.Ordinal);
    }
}
