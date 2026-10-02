using HexArch.Domain.Entities;
using HexArch.Domain.Exceptions;

namespace test;

public class UserEntityTests
{
    [Fact]
    public void Id_RejectsAnyValueBelowOne()
    {
        var entity = new UserEntity();

        Assert.Throws<EntityException>(() => entity.Id = 0);
        Assert.Throws<EntityException>(() => entity.Id = -3);
        Assert.Equal(7, entity.Id = 7);
        Assert.Null(entity.Id = null);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(101)]
    public void Name_RejectsLengthsOutsideTenToOneHundredCharacters(int length)
    {
        var entity = new UserEntity();

        var exception = Assert.Throws<EntityException>(() => entity.Name = new string('a', length));

        Assert.Equal("El nombre debe tener entre 10 y 100 caracteres.", exception.Message);
    }

    [Fact]
    public void Name_AcceptsLengthsWithinTenToOneHundredCharacters()
    {
        var entity = new UserEntity();

        Assert.Equal(new string('a', 10), entity.Name = new string('a', 10));
        Assert.Equal(new string('a', 100), entity.Name = new string('a', 100));
    }

    [Theory]
    [InlineData(14)]
    [InlineData(151)]
    public void Surname_RejectsLengthsOutsideFifteenToOneHundredFiftyCharacters(int length)
    {
        var entity = new UserEntity();

        var exception = Assert.Throws<EntityException>(() => entity.Surname = new string('a', length));

        Assert.Equal("El apellido debe tener entre 15 y 150 caracteres.", exception.Message);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(101)]
    public void Nickname_RejectsLengthsOutsideTenToOneHundredCharacters(int length)
    {
        var entity = new UserEntity();

        var exception = Assert.Throws<EntityException>(() => entity.Nickname = new string('a', length));

        Assert.Equal("El apodo debe tener entre 10 y 100 caracteres.", exception.Message);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(256)]
    public void Password_RejectsLengthsOutsideTwelveToTwoHundredFiftyFiveCharacters(int length)
    {
        var entity = new UserEntity();

        var exception = Assert.Throws<EntityException>(() => entity.Password = new string('a', length));

        Assert.Equal("La contraseña debe tener entre 12 y 255 caracteres.", exception.Message);
    }

    [Fact]
    public void Role_RejectsMoreThanTenCharacters()
    {
        var entity = new UserEntity();

        var exception = Assert.Throws<EntityException>(() => entity.Role = new string('a', 11));

        Assert.Equal("El rol debe tener un máximo de 10 caracteres.", exception.Message);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("user")]
    public void Role_AcceptsThePredefinedRoleValues(string role)
    {
        var entity = new UserEntity();

        Assert.Equal(role, entity.Role = role);
    }

    [Fact]
    public void Role_OnlyLimitsLengthSoTheClosedListIsEnforcedByTheApi()
    {
        var entity = new UserEntity();

        Assert.Equal("root", entity.Role = "root");
    }

    [Fact]
    public void Signature_RejectsMoreThanTwoHundredFiftyFiveCharacters()
    {
        var entity = new UserEntity();

        var exception = Assert.Throws<EntityException>(() => entity.Signature = new string('a', 256));

        Assert.Equal("La firma debe tener un máximo de 255 caracteres.", exception.Message);
    }

    [Fact]
    public void EveryPropertyAcceptsNull()
    {
        var entity = new UserEntity();

        entity.Id = null;
        entity.Name = null;
        entity.Surname = null;
        entity.Nickname = null;
        entity.Password = null;
        entity.Role = null;
        entity.Signature = null;

        Assert.Null(entity.Id);
        Assert.Null(entity.Name);
        Assert.Null(entity.Surname);
        Assert.Null(entity.Nickname);
        Assert.Null(entity.Password);
        Assert.Null(entity.Role);
        Assert.Null(entity.Signature);
    }

    [Fact]
    public void RejectedValue_LeavesThePropertyUntouched()
    {
        var entity = new UserEntity { Role = "admin" };

        Assert.Throws<EntityException>(() => entity.Role = new string('a', 20));

        Assert.Equal("admin", entity.Role);
    }
}