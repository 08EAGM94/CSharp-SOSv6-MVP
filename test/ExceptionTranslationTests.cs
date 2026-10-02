using System.Globalization;
using HexArch.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using test.Support;
using HexArchApplicationException = HexArch.Domain.Exceptions.ApplicationException;

namespace test;

/// <summary>
/// One case per row of table 4.2 of the plan: every failure origin produced by a use case is
/// translated into the status code the spec assigns to it, and the message reaches the consumer
/// in Spanish.
/// </summary>
public class ExceptionTranslationTests
{
    [Fact]
    public async Task RecordNotFound_IsTranslatedToNotFound()
    {
        var response = await TestHttp.TranslateAsync(
            new KeyNotFoundException("No se encontró un usuario con la información solicitada."));

        Assert.Equal(404, response.StatusCode);
        Assert.Equal("No se encontró un usuario con la información solicitada.", response.Body);
    }

    [Fact]
    public async Task EntityRuleViolation_IsTranslatedToBadRequest()
    {
        var response = await TestHttp.TranslateAsync(
            new EntityException("El apodo debe tener entre 10 y 100 caracteres."));

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("El apodo debe tener entre 10 y 100 caracteres.", response.Body);
    }

    [Fact]
    public async Task ApplicationRuleViolation_IsTranslatedToBadRequest()
    {
        var response = await TestHttp.TranslateAsync(
            new HexArchApplicationException("La visibilidad debe ser ENABLED o DISABLED."));

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("La visibilidad debe ser ENABLED o DISABLED.", response.Body);
    }

    [Fact]
    public async Task AnyOtherBusinessFailure_IsTranslatedToBadRequest()
    {
        var response = await TestHttp.TranslateAsync(
            new Exception("La contraseña escrita no corresponde a la del usuario."));

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("La contraseña escrita no corresponde a la del usuario.", response.Body);
    }

    [Fact]
    public async Task UniquenessConflictFromThePersistenceEngine_IsTranslatedToInternalServerError()
    {
        var response = await TestHttp.TranslateAsync(
            new DbUpdateException("No se pudo guardar el usuario porque el alias ya está registrado."));

        Assert.Equal(500, response.StatusCode);
        Assert.Equal("No se pudo guardar el usuario porque el alias ya está registrado.", response.Body);
    }

    [Fact]
    public async Task AnyPersistenceFailureFallsIntoInternalServerErrorAndNeverIntoBadRequest()
    {
        var failures = new Exception[]
        {
            new DbUpdateException("Fallo de infraestructura al guardar el usuario."),
            new DbUpdateException("Conflicto de unicidad sobre uq_alias.", new InvalidOperationException("ix_alias")),
            new DbUpdateException("Conexión cerrada contra el servidor de base de datos.")
        };

        foreach (var failure in failures)
        {
            var response = await TestHttp.TranslateAsync(failure);

            Assert.Equal(500, response.StatusCode);
        }
    }

    [Theory]
    [InlineData(typeof(KeyNotFoundException))]
    [InlineData(typeof(EntityException))]
    [InlineData(typeof(HexArchApplicationException))]
    [InlineData(typeof(Exception))]
    public async Task Translation_NeverFallsIntoServerErrorForBusinessFailures(Type exceptionType)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "Mensaje de negocio en español.")!;

        var response = await TestHttp.TranslateAsync(exception);

        Assert.NotEqual(500, response.StatusCode);
    }

    [Fact]
    public async Task Message_IsDeliveredAsPlainTextInSpanish()
    {
        var response = await TestHttp.TranslateAsync(new KeyNotFoundException("El usuario no existe."));

        Assert.Equal("text/plain; charset=utf-8", response.Headers.ContentType.ToString());
        Assert.Equal("El usuario no existe.", response.Body);
    }

    [Fact]
    public async Task Response_DoesNotUseProblemDetails()
    {
        var response = await TestHttp.TranslateAsync(new EntityException("El rol debe tener un máximo de 10 caracteres."));

        Assert.DoesNotContain("ProblemDetails", response.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"problemType\"", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IdentifierOfTheMissingRecord_IsNotLeakedByTheTranslation()
    {
        var response = await TestHttp.TranslateAsync(
            new KeyNotFoundException(string.Format(CultureInfo.InvariantCulture, "No se encontró el usuario {0}.", 42)));

        Assert.Equal(404, response.StatusCode);
        Assert.Equal("No se encontró el usuario 42.", response.Body);
    }
}