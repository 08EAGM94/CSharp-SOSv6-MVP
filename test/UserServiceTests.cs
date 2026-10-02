using HexArch.Application.DTOs;using HexArch.Application.UseCases;using HexArch.Data.Mappers.DtoToEntity;using HexArch.Domain.Entities;using HexArch.Domain.Exceptions;using test.Fakes;namespace test;public class UserServiceTests{    private static (UserService Service, FakeUserRepository Repository, FakeUserMapper Mapper) BuildService()    {        var repository = new FakeUserRepository();        var mapper = new FakeUserMapper { MapFunc = dto => new UserDTOtoEntityMapper().Map(dto) };        return (new UserService(repository, mapper), repository, mapper);    }    [Fact]
    public async Task Login_DelegatesTheMappedCredentialsToTheRepository()
    {
        var (service, repository, mapper) = BuildService();
        var stored = new UserDTO
        {
            Id = 7,
            Name = "Nombre Completo",
            Surname = "Apellido Completo Del Usuario",
            Nickname = "operador",
            Role = "user",
            Signature = "Firma del operador"
        };
        repository.LoginResult = stored;

        var result = await service.Login(new UserDTO { Nickname = "administrador12345", Password = "clave-de-prueba-123" });

        Assert.Same(stored, result);
        Assert.Equal(1, repository.LoginCalls);
        Assert.Equal(1, mapper.MapCalls);

        var entity = Assert.IsType<UserEntity>(repository.LastLoginEntity);
        Assert.Equal("administrador12345", entity.Nickname);
        Assert.Equal("clave-de-prueba-123", entity.Password);
        Assert.Null(entity.Role);
    }    [Fact]    public async Task Login_PropagatesTheRepositoryFailureWithoutSwallowingIt()    {        var (service, repository, _) = BuildService();        repository.ExceptionToThrow = new KeyNotFoundException("No se encontró un usuario con la información solicitada.");        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(            () => service.Login(new UserDTO { Nickname = "deshabilitado", Password = "clave-de-prueba-123" }));        Assert.Equal("No se encontró un usuario con la información solicitada.", exception.Message);    }    [Fact]    public async Task Login_PropagatesTheWrongPasswordFailure()    {        var (service, repository, _) = BuildService();        repository.ExceptionToThrow = new Exception("La contraseña escrita no corresponde a la del usuario.");        var exception = await Assert.ThrowsAsync<Exception>(
            () => service.Login(new UserDTO { Nickname = "administrador12345", Password = "clave-equivocada-123" }));        Assert.Equal("La contraseña escrita no corresponde a la del usuario.", exception.Message);    }    [Fact]    public void AdminPwdConfirmation_DelegatesTheAdminCredentialsToTheRepository()    {        var (service, repository, _) = BuildService();        repository.AdminPwdConfirmationResult = true;        var result = service.AdminPwdConfirmation(new UserDTO        {            AdminNickname = "adminPrincipal",            AdminPwd = "clave-del-admin-123"        });        Assert.True(result);        Assert.Equal(1, repository.AdminPwdConfirmationCalls);        var entity = Assert.IsType<UserEntity>(repository.LastAdminPwdEntity);        Assert.Equal("adminPrincipal", entity.Nickname);        Assert.Equal("clave-del-admin-123", entity.Password);    }    [Fact]    public void AdminPwdConfirmation_ReturnsFalseWhenThePasswordDoesNotMatch()    {        var (service, repository, _) = BuildService();        repository.AdminPwdConfirmationResult = false;        var result = service.AdminPwdConfirmation(new UserDTO        {            AdminNickname = "adminPrincipal",            AdminPwd = "clave-equivocada-123"        });        Assert.False(result);        Assert.Equal(1, repository.AdminPwdConfirmationCalls);    }    [Fact]    public void AdminPwdConfirmation_PropagatesTheRepositoryFailure()    {        var (service, repository, _) = BuildService();        repository.ExceptionToThrow = new KeyNotFoundException("No se encontró un usuario con la información solicitada.");        var exception = Assert.Throws<KeyNotFoundException>(            () => service.AdminPwdConfirmation(new UserDTO { AdminNickname = "inexistente", AdminPwd = "clave-del-admin-123" }));        Assert.Equal("No se encontró un usuario con la información solicitada.", exception.Message);    }    [Fact]    public void Mapper_GivesPrecedenceToTheAdminCredentialsOverTheUserCredentials()    {        var mapper = new UserDTOtoEntityMapper();        var entity = mapper.Map(new UserDTO        {            Nickname = "operador",            Password = "clave-de-prueba-123",            AdminNickname = "adminPrincipal",            AdminPwd = "clave-del-admin-123"        });        Assert.Equal("adminPrincipal", entity.Nickname);        Assert.Equal("clave-del-admin-123", entity.Password);    }    [Fact]    public void Mapper_FallsBackToTheUserCredentialsWhenTheAdminOnesAreAbsent()    {        var mapper = new UserDTOtoEntityMapper();        var entity = mapper.Map(new UserDTO { Nickname = "administrador12345", Password = "clave-de-prueba-123" });

        Assert.Equal("administrador12345", entity.Nickname);
        Assert.Equal("clave-de-prueba-123", entity.Password);    }    [Fact]    public void Mapper_RejectsCredentialsThatViolateTheEntityRules()    {        var mapper = new UserDTOtoEntityMapper();        Assert.Throws<EntityException>(() => mapper.Map(new UserDTO { Password = "corta" }));    }}