using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SosMVP.Security;

namespace test.Support;

public static class HandlerTestSupport
{
    public static async Task AssertTranslationAsync<TException>(Func<Task<IResult>> action, int expectedStatusCode)
        where TException : Exception
    {
        var exception = await Assert.ThrowsAnyAsync<TException>(action);
        var response = await TestHttp.TranslateAsync(exception);

        Assert.Equal(expectedStatusCode, response.StatusCode);
    }

    public static ClaimsPrincipal CreateSession(int? userId = null, string? role = AdminAuthorization.AdminRole)
    {
        var claims = new List<Claim>();

        if (userId is not null)
        {
            claims.Add(new Claim(JwtTokenFactory.IdClaimType, userId.Value.ToString()));
        }

        if (role is not null)
        {
            claims.Add(new Claim(JwtTokenFactory.RoleClaimType, role));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "TestSession"));
    }

    public static async Task AssertEmptyBodyAsync(IResult result)
    {
        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(string.Empty, response.Body);
    }

    public static async Task AssertBodyIsInSpanishAsync(IResult result)
    {
        var response = await TestHttp.ExecuteAsync(result);

        Assert.False(string.IsNullOrWhiteSpace(response.Body));
        Assert.Contains(".", response.Body, StringComparison.Ordinal);
    }

    public static async Task AssertCreatedWithoutLocationAsync(IResult result)
    {
        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status201Created, response.StatusCode);
        Assert.Equal(string.Empty, response.Body);
        Assert.False(response.HasHeader("Location"));
    }

    public static async Task AssertNoContentAsync(IResult result)
    {
        var response = await TestHttp.ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status204NoContent, response.StatusCode);
        Assert.Equal(string.Empty, response.Body);
    }

    public static JsonElement ReadJsonBody(string body)
    {
        using var document = JsonDocument.Parse(body);

        return document.RootElement.Clone();
    }

    public static string ReadToken(string body)
    {
        return ReadJsonBody(body).GetProperty("token").GetString()!;
    }
}