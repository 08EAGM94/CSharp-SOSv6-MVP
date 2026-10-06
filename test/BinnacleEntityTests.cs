using HexArch.Domain.Entities;
using HexArch.Domain.Exceptions;

namespace test;

public class BinnacleEntityTests
{
    [Fact]
    public void Id_RejectsAnyValueBelowOne()
    {
        var entity = new BinnacleEntity();

        var exception = Assert.Throws<EntityException>(() => entity.Id = 0);

        Assert.Equal("El identificador debe ser un número mayor que cero.", exception.Message);
        Assert.Throws<EntityException>(() => entity.Id = -3);
        Assert.Equal(7, entity.Id = 7);
        Assert.Null(entity.Id = null);
    }

    [Fact]
    public void UserId_RejectsAnyValueBelowOne()
    {
        var entity = new BinnacleEntity();

        var exception = Assert.Throws<EntityException>(() => entity.UserId = 0);

        Assert.Equal("El identificador del usuario debe ser un número mayor que cero.", exception.Message);
        Assert.Throws<EntityException>(() => entity.UserId = -1);
        Assert.Equal(1, entity.UserId = 1);
        Assert.Null(entity.UserId = null);
    }

    [Fact]
    public void ContactId_RejectsAnyValueBelowOne()
    {
        var entity = new BinnacleEntity();

        var exception = Assert.Throws<EntityException>(() => entity.ContactId = 0);

        Assert.Equal("El identificador del contacto debe ser un número mayor que cero.", exception.Message);
        Assert.Throws<EntityException>(() => entity.ContactId = -5);
        Assert.Equal(2, entity.ContactId = 2);
        Assert.Null(entity.ContactId = null);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10.5f)]
    public void Amount_RejectsAnyValueBelowOne(float amount)
    {
        var entity = new BinnacleEntity();

        var exception = Assert.Throws<EntityException>(() => entity.Amount = amount);

        Assert.Equal("El monto debe ser un número mayor que cero.", exception.Message);
        Assert.Equal(1f, entity.Amount = 1f);
        Assert.Null(entity.Amount = null);
    }

    [Fact]
    public void Service_RejectsLengthsBelowFifteenCharacters()
    {
        var entity = new BinnacleEntity();

        var exception = Assert.Throws<EntityException>(() => entity.Service = new string('a', 14));

        Assert.Equal("El servicio debe tener al menos 15 caracteres.", exception.Message);
        Assert.Equal(new string('a', 15), entity.Service = new string('a', 15));
        Assert.Null(entity.Service = null);
    }

    [Fact]
    public void CustomerSignature_RejectsMoreThanTwoHundredFiftyFiveCharacters()
    {
        var entity = new BinnacleEntity();

        var exception = Assert.Throws<EntityException>(() => entity.CustomerSignature = new string('a', 256));

        Assert.Equal("La firma del cliente debe tener un máximo de 255 caracteres.", exception.Message);
        Assert.Equal(new string('a', 255), entity.CustomerSignature = new string('a', 255));
        Assert.Null(entity.CustomerSignature = null);
    }

    [Fact]
    public void ActivitiesDone_RejectsLengthsBelowFifteenCharacters()
    {
        var entity = new BinnacleEntity();

        var exception = Assert.Throws<EntityException>(() => entity.ActivitiesDone = new string('a', 14));

        Assert.Equal("Las actividades realizadas deben tener al menos 15 caracteres.", exception.Message);
        Assert.Equal(new string('a', 15), entity.ActivitiesDone = new string('a', 15));
        Assert.Null(entity.ActivitiesDone = null);
    }

    [Fact]
    public void Hints_RejectsLengthsBelowFifteenCharacters()
    {
        var entity = new BinnacleEntity();

        var exception = Assert.Throws<EntityException>(() => entity.Hints = new string('a', 14));

        Assert.Equal("Las observaciones deben tener al menos 15 caracteres.", exception.Message);
        Assert.Equal(new string('a', 15), entity.Hints = new string('a', 15));
        Assert.Null(entity.Hints = null);
    }

    [Fact]
    public void TextProperties_AcceptNull()
    {
        var entity = new BinnacleEntity
        {
            Service = "Mantenimiento preventivo",
            ActivitiesDone = "Actividades realizadas en campo",
            Hints = "Observaciones del operador en sitio",
            CustomerSignature = "Firma del cliente"
        };

        entity.Service = null;
        entity.ActivitiesDone = null;
        entity.Hints = null;
        entity.CustomerSignature = null;
        entity.Status = null;

        Assert.Null(entity.Service);
        Assert.Null(entity.ActivitiesDone);
        Assert.Null(entity.Hints);
        Assert.Null(entity.CustomerSignature);
        Assert.Null(entity.Status);
    }

    [Fact]
    public void RejectedValue_LeavesThePropertyUntouched()
    {
        var entity = new BinnacleEntity { Service = "Mantenimiento preventivo" };

        Assert.Throws<EntityException>(() => entity.Service = "corto");

        Assert.Equal("Mantenimiento preventivo", entity.Service);
    }
}
