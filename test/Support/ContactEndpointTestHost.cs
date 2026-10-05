using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using HexArch.Application.Abstractions.PrimaryPorts;
using HexArch.Application.DTOs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Features.Authentication;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SosMVP.Extensions;
using SosMVP.Security;
using test.Fakes;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace test.Support;

/// <summary>
/// Runs one request through the seven routes of <c>MapContactEndpoints</c> and the very pipeline that
/// answers 401, 403, 400 and the success codes in production: the JwtBearer handler validates the
/// token, the production evaluator decides the policy the route declares, and only an open gate lets
/// the minimal api binding run the handler. Needed for the answers a fake based handler suite cannot
/// produce on its own, because they happen before the body of the handler executes.
/// </summary>
public sealed class ContactEndpointTestHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private ContactEndpointTestHost(WebApplication app)
    {
        _app = app;
    }

    public static ContactEndpointTestHost Start(FakeContactChildrenService childrenService, FakeContactSelectService selectService)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton(OptionsFactory.Create(JwtTestTokens.Options()));

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer();

        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwt) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = JwtTokenFactory.CreateValidationParameters(jwt.Value);
            });

        builder.Services
            .AddAuthorizationBuilder()
            .AddPolicy(AdminAuthorization.PolicyName, AdminAuthorization.BuildPolicy());

        builder.Services.AddScoped<IEnterpriseChildrenService<ContactDTO>>(_ => childrenService);
        builder.Services.AddScoped<ISelectService<ContactDTO>>(_ => selectService);

        var app = builder.Build();

        app.MapContactEndpoints();

        return new ContactEndpointTestHost(app);
    }

    public static string SessionToken(string? role)
    {
        var dto = JwtTestTokens.FullDto();
        dto.Role = role;

        return JwtTestTokens.Factory().CreateToken(dto);
    }

    public IReadOnlyList<RouteEndpoint> Endpoints()
    {
        return ((IEndpointRouteBuilder)_app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    public RouteEndpoint Find(string route, string method)
    {
        return Endpoints().Single(endpoint =>
            endpoint.RoutePattern.RawText == route &&
            endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!
                .HttpMethods.Contains(method, StringComparer.OrdinalIgnoreCase));
    }

    public Task<TestHttpResponse> SendAsync(
        string route,
        string method,
        string? bearerToken = null,
        string? jsonBody = null,
        string? idValue = "5",
        string? enterpriseIdValue = "5")
    {
        return DispatchAsync(Find(route, method), route, method, bearerToken, jsonBody, idValue, enterpriseIdValue);
    }

    private async Task<TestHttpResponse> DispatchAsync(
        RouteEndpoint endpoint,
        string route,
        string method,
        string? bearerToken,
        string? jsonBody,
        string? idValue,
        string? enterpriseIdValue)
    {
        using var scope = _app.Services.CreateScope();

        var context = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            Response = { Body = new MemoryStream() }
        };
        context.Request.Method = method;

        var (path, routeValues) = RequestOf(route, idValue, enterpriseIdValue);
        context.Request.Path = path;
        context.Request.RouteValues = routeValues;

        if (bearerToken is not null)
        {
            context.Request.Headers["Authorization"] = $"Bearer {bearerToken}";
        }

        if (jsonBody is not null)
        {
            SetJsonBody(context, jsonBody);
        }

        var authenticationService = context.RequestServices.GetRequiredService<IAuthenticationService>();
        var authenticateResult = await authenticationService.AuthenticateAsync(context, JwtBearerDefaults.AuthenticationScheme);

        context.Features.Set<IAuthenticateResultFeature>(new AuthenticateResultFeature { AuthenticateResult = authenticateResult });
        context.User = authenticateResult.Principal ?? new ClaimsPrincipal(new ClaimsIdentity());

        var policy = await AuthorizationPolicy.CombineAsync(
            context.RequestServices.GetRequiredService<IAuthorizationPolicyProvider>(),
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());

        Assert.NotNull(policy);

        var policyEvaluator = context.RequestServices.GetRequiredService<IPolicyEvaluator>();
        var authentication = await policyEvaluator.AuthenticateAsync(policy!, context);
        var authorization = await policyEvaluator.AuthorizeAsync(policy!, authentication, context, endpoint);

        if (authorization.Succeeded)
        {
            await Assert.IsAssignableFrom<RequestDelegate>(endpoint.RequestDelegate)(context);
        }
        else if (authorization.Challenged)
        {
            await context.ChallengeAsync(JwtBearerDefaults.AuthenticationScheme);
        }
        else if (authorization.Forbidden)
        {
            await context.ForbidAsync(JwtBearerDefaults.AuthenticationScheme);
        }

        context.Response.Body.Position = 0;

        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();

        return new TestHttpResponse(context.Response.StatusCode, responseBody, context.Response.Headers);
    }

    private static (string Path, RouteValueDictionary RouteValues) RequestOf(
        string route,
        string? idValue,
        string? enterpriseIdValue)
    {
        var routeValues = new RouteValueDictionary();
        var path = route;

        foreach (Match parameter in Regex.Matches(route, @"\{([^}]+)\}"))
        {
            var name = parameter.Groups[1].Value;
            var value = name switch
            {
                "id" => idValue,
                "enterpriseId" => enterpriseIdValue,
                _ => throw new InvalidOperationException($"El host de pruebas no conoce el parámetro de ruta '{name}'.")
            };

            routeValues[name] = value;
            path = path.Replace(parameter.Value, value, StringComparison.Ordinal);
        }

        return (path, routeValues);
    }

    private static void SetJsonBody(DefaultHttpContext context, string json)
    {
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetectionFeature());
    }

    private sealed class BodyDetectionFeature : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody { get; set; } = true;
    }

    private sealed class AuthenticateResultFeature : IAuthenticateResultFeature
    {
        public AuthenticateResult? AuthenticateResult { get; set; }
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
    }
}