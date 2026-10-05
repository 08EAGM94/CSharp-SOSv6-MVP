using System.Security.Claims;
using System.Text;
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
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SosMVP.Extensions;
using SosMVP.Security;
using test.Fakes;
using test.Support;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace test;

/// <summary>
/// Subject: the authorization each of the six routes of <c>MapEnterpriseEndpoints</c> declares, run
/// through the very pipeline that answers 401 and 403 in production. Covers RF-1.1 to RF-1.7, CE-7,
/// CE-8, CE-9 and CE-15.
/// <para>
/// No host is started, as the plan requires: the routes are materialized for real and each request
/// walks the three production steps that decide and write the answer:
/// </para>
/// <list type="number">
/// <item><description>authentication: the JwtBearer handler validates the token of the
/// <c>Authorization</c> header with the production validation parameters;</description></item>
/// <item><description>authorization: the production <see cref="IPolicyEvaluator"/> evaluates the
/// policy the route really declares, combined with the production
/// <see cref="IAuthorizationPolicyProvider"/>;</description></item>
/// <item><description>the result handler writes <c>401</c> when the decision is challenged, <c>403</c>
/// when it is forbidden, and only an open gate lets the endpoint delegate run.</description></item>
/// </list>
/// <para>
/// The endpoint delegate being behind that gate is what makes "no endpoint function runs"
/// (RF-1.2, RF-1.4, CE-7, CE-8) observable: the fakes report whether any use case was reached.
/// </para>
/// </summary>
public class EnterpriseAuthorizationTests
{
    private const string UserRole = "user";
    private const string UnknownRole = "supervisor";

    // RF-2.1, RF-2.2: the single wrapper body, with the enterprise as its first part.
    private const string InsertBody =
        """
        {
          "enterprise": {
            "commercialName": "Nombre comercial de la empresa",
            "tradeName": "Nombre de la empresa"
          },
          "contact": {
            "fullName": "Nombre de contacto inicial",
            "visibility": "ENABLED"
          }
        }
        """;

    private const string UpdateBody =
        """
        {
          "commercialName": "Nombre comercial de la empresa",
          "tradeName": "Nombre de la empresa"
        }
        """;

    private const string VisibilityBody =
        """
        {
          "visibility": "ENABLED"
        }
        """;

    #region Test subject: the six routes and the production pipeline that guards them

    /// <summary>
    /// Builds the same application services Program.cs registers for authentication and
    /// authorization, plus the enterprise use case ports backed by fakes, and maps the six routes.
    /// </summary>
    private static WebApplication BuildTestApplication(
        FakeEnterpriseCommonService commonService,
        FakeEnterpriseSelectService selectService)
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

        builder.Services.AddScoped<ICommonService<EnterpriseDTO>>(_ => commonService);
        builder.Services.AddScoped<ISelectService<EnterpriseDTO>>(_ => selectService);

        var app = builder.Build();

        app.MapEnterpriseEndpoints();

        return app;
    }

    /// <summary>
    /// Reading the endpoints is what makes RequestDelegateFactory bind every handler, so the delegate
    /// under test here is the one the server would invoke (guarded by EnterpriseRoutingTests).
    /// </summary>
    private static IReadOnlyList<RouteEndpoint> EndpointsOf(WebApplication app)
    {
        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    private static RouteEndpoint Find(WebApplication app, string route, string method)
    {
        return EndpointsOf(app).Single(endpoint =>
            endpoint.RoutePattern.RawText == route &&
            endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!
                .HttpMethods.Contains(method, StringComparer.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<IAuthorizeData> AuthorizationsOf(RouteEndpoint endpoint)
    {
        return endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().ToList();
    }

    private static RequestDelegate EndpointDelegateOf(RouteEndpoint endpoint)
    {
        return Assert.IsAssignableFrom<RequestDelegate>(endpoint.RequestDelegate);
    }

    /// <summary>
    /// Puts a JSON body on the hand built request. The body detection feature is what the web server
    /// publishes on a real request; without it the minimal API binding answers 400 instead of reading
    /// the body, which would hide the answer of the endpoint behind a binding failure.
    /// </summary>
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

    /// <summary>
    /// Runs one request through the production gate and returns what the client would receive.
    /// </summary>
    private static async Task<TestHttpResponse> DispatchAsync(
        WebApplication app,
        RouteEndpoint endpoint,
        string? bearerToken,
        string method,
        string route)
    {
        using var scope = app.Services.CreateScope();

        var context = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            Response = { Body = new MemoryStream() }
        };
        context.Request.Method = method;
        context.Request.Path = ConcretePathOf(route);
        context.Request.RouteValues = RouteValuesOf(route);

        if (bearerToken is not null)
        {
            context.Request.Headers["Authorization"] = $"Bearer {bearerToken}";
        }

        if (method == HttpMethods.Post)
        {
            SetJsonBody(context, InsertBody);
        }
        else if (method == HttpMethods.Put)
        {
            SetJsonBody(context, route.StartsWith("/enterprisev/", StringComparison.Ordinal) ? VisibilityBody : UpdateBody);
        }

        // UseAuthentication: the JwtBearer handler really validates the token of the header, and the
        // result is published exactly as the middleware publishes it.
        var authenticationService = context.RequestServices.GetRequiredService<IAuthenticationService>();
        var authenticateResult = await authenticationService.AuthenticateAsync(context, JwtBearerDefaults.AuthenticationScheme);

        context.Features.Set<IAuthenticateResultFeature>(new TestAuthenticateResultFeature { AuthenticateResult = authenticateResult });
        context.User = authenticateResult.Principal ?? new ClaimsPrincipal(new ClaimsIdentity());

        // UseAuthorization: combine the data the route declares into the policy the middleware would
        // build, then let the production evaluator decide challenged, forbidden or succeeded.
        var policy = await AuthorizationPolicy.CombineAsync(
            context.RequestServices.GetRequiredService<IAuthorizationPolicyProvider>(),
            AuthorizationsOf(endpoint));

        Assert.NotNull(policy);

        var policyEvaluator = context.RequestServices.GetRequiredService<IPolicyEvaluator>();

        var authentication = await policyEvaluator.AuthenticateAsync(policy!, context);
        var authorization = await policyEvaluator.AuthorizeAsync(policy!, authentication, context, endpoint);

        // AuthorizationMiddlewareResultHandler: the decision becomes the status code, and the endpoint
        // delegate only runs when the gate opens.
        if (authorization.Succeeded)
        {
            await EndpointDelegateOf(endpoint)(context);
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

    /// <summary>
    /// Publishes the authentication outcome the way the authentication middleware does, so the policy
    /// evaluator reads the very result the JwtBearer handler produced.
    /// </summary>
    private sealed class TestAuthenticateResultFeature : IAuthenticateResultFeature
    {
        public AuthenticateResult? AuthenticateResult { get; set; }
    }

    private static int EndpointFunctionCalls(
        FakeEnterpriseCommonService commonService,
        FakeEnterpriseSelectService selectService)
    {
        return commonService.AddAsyncInfoCalls +
            commonService.GetAsyncInfoCalls +
            commonService.GetAsyncAllInfoCalls +
            commonService.UpdateAsyncInfoCalls +
            commonService.UpdateAsyncVisibilityCalls +
            selectService.GetAsyncInfoForSelectsCalls;
    }

    #endregion

    #region Sessions, produced by the real JwtTokenFactory

    private static UserDTO SessionWithRole(string? role)
    {
        var dto = JwtTestTokens.FullDto();
        dto.Role = role;

        return dto;
    }

    private static IReadOnlyList<Claim> ClaimsOfSession(string? role)
    {
        return JwtTokenFactory.BuildClaims(SessionWithRole(role));
    }

    private static string SessionToken(string? role)
    {
        return JwtTestTokens.Factory().CreateToken(SessionWithRole(role));
    }

    /// <summary>
    /// CE-15: a session issued thirty minutes ago and never renewed. It is produced by the production
    /// factory, so what the pipeline rejects is a real token whose window has already closed.
    /// </summary>
    private static string ExpiredSessionToken()
    {
        return JwtTestTokens.Factory(-JwtTestTokens.SessionMinutes).CreateToken(SessionWithRole(AdminAuthorization.AdminRole));
    }

    /// <summary>
    /// CE-9: a manipulated session, signed with the real key so it IS authenticated and the answer
    /// cannot be a 401. What was manipulated are the claims, which is precisely what makes the role
    /// comparison impossible.
    /// </summary>
    private static string ManipulatedSessionToken(IEnumerable<Claim> claims)
    {
        var options = JwtTestTokens.Options();
        var now = DateTime.UtcNow;

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(options.SessionMinutes),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)),
                SecurityAlgorithms.HmacSha256)
        });
    }

    /// <summary>
    /// A session whose signature no longer matches its content. Unlike a manipulated claim, this is
    /// not a session at all, so the answer is 401 and not the 403 of CE-9.
    /// </summary>
    private static string TokenWithBrokenSignature(string token)
    {
        var signatureStart = token.LastIndexOf('.') + 1;
        var signature = token[signatureStart..];
        var broken = (signature[^1] == 'A' ? 'B' : 'A') + signature[..^1];

        return string.Concat(token.AsSpan(0, signatureStart), broken);
    }

    #endregion

    #region Requests

    private static string ConcretePathOf(string route)
    {
        return route.Replace("{id}", "5", StringComparison.Ordinal);
    }

    private static RouteValueDictionary RouteValuesOf(string route)
    {
        return route.Contains("{id}", StringComparison.Ordinal)
            ? new RouteValueDictionary(new Dictionary<string, object?> { ["id"] = "5" })
            : new RouteValueDictionary();
    }

    #endregion

    #region Tests

    [Fact]
    public async Task TheSixRoutesAreMappedAndNoneOfThemIsPublic()
    {
        // RF-1.7: every route of this use case demands a session; none of them is reachable anonymously.
        await using var app = BuildTestApplication(new FakeEnterpriseCommonService(), new FakeEnterpriseSelectService());

        var endpoints = EndpointsOf(app);

        Assert.Equal(6, endpoints.Count);
        Assert.All(endpoints, endpoint =>
        {
            Assert.NotEmpty(AuthorizationsOf(endpoint));
            Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
        });
    }

    [Theory]
    [InlineData("/enterprise/", "POST")]
    [InlineData("/enterprise/{id}", "GET")]
    [InlineData("/enterprises/", "GET")]
    [InlineData("/enterprise/{id}", "PUT")]
    [InlineData("/enterprisev/{id}", "PUT")]
    [InlineData("/enterprisesct/", "GET")]
    public async Task AllEndpoints_RequireValidSession_401WhenMissing(string route, string method)
    {
        var commonService = new FakeEnterpriseCommonService();
        var selectService = new FakeEnterpriseSelectService();

        await using var app = BuildTestApplication(commonService, selectService);
        var endpoint = Find(app, route, method);

        // RF-1.1: the route verifies the session before anything else.
        Assert.NotEmpty(AuthorizationsOf(endpoint));

        // RF-1.2, CE-7: no session at all is a challenge, which is the decision the pipeline answers
        // as 401. A forbid here would be a 403, reserved for a session that exists but is not admin.
        var response = await DispatchAsync(app, endpoint, bearerToken: null, method, route);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);

        // RF-1.2, CE-7: and no function of the endpoint ran.
        Assert.Equal(0, EndpointFunctionCalls(commonService, selectService));
    }

    [Theory]
    [InlineData("/enterprise/", "POST")]
    [InlineData("/enterprise/{id}", "GET")]
    [InlineData("/enterprises/", "GET")]
    [InlineData("/enterprise/{id}", "PUT")]
    [InlineData("/enterprisev/{id}", "PUT")]
    [InlineData("/enterprisesct/", "GET")]
    public async Task AllEndpoints_RequireValidSession_401WhenExpired(string route, string method)
    {
        var commonService = new FakeEnterpriseCommonService();
        var selectService = new FakeEnterpriseSelectService();

        await using var app = BuildTestApplication(commonService, selectService);
        var endpoint = Find(app, route, method);

        // CE-15: the session lives thirty minutes from its emission and is not renewed on activity, so
        // a token inside that window is a live session and one that crossed it is not. This pins the
        // window the request below depends on.
        var factory = JwtTestTokens.Factory();
        var issuedNow = factory.CreateToken(SessionWithRole(AdminAuthorization.AdminRole));

        Assert.True((await JwtTestTokens.ValidateAsync(factory, issuedNow)).IsValid);
        Assert.True((await JwtTestTokens.ValidateAsync(factory, issuedNow, DateTime.UtcNow.AddMinutes(JwtTestTokens.SessionMinutes).AddSeconds(-1))).IsValid);
        Assert.False((await JwtTestTokens.ValidateAsync(factory, issuedNow, DateTime.UtcNow.AddMinutes(JwtTestTokens.SessionMinutes))).IsValid);

        // RF-1.2, CE-7, CE-15: an expired session leaves the request with no authenticated identity, so
        // the route answers exactly as it does when the session is missing.
        var response = await DispatchAsync(app, endpoint, ExpiredSessionToken(), method, route);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal(0, EndpointFunctionCalls(commonService, selectService));
    }

    [Theory]
    [InlineData("/enterprise/{id}", "PUT")]
    [InlineData("/enterprisev/{id}", "PUT")]
    public async Task UpdateEndpoints_RequireAdminRole_403WhenUser(string route, string method)
    {
        var commonService = new FakeEnterpriseCommonService();
        var selectService = new FakeEnterpriseSelectService();

        await using var app = BuildTestApplication(commonService, selectService);
        var endpoint = Find(app, route, method);

        // RF-1.3: these two routes are the only ones that compare the role of the session.
        Assert.Contains(AuthorizationsOf(endpoint), data => data.Policy == AdminAuthorization.PolicyName);

        // RF-1.4, CE-8: the session exists, so the decision is a forbid, answered as 403.
        var response = await DispatchAsync(app, endpoint, SessionToken(UserRole), method, route);

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, response.StatusCode);

        // RF-1.4, CE-8: and no vital function of the endpoint ran, the use case was never reached.
        Assert.Equal(0, EndpointFunctionCalls(commonService, selectService));

        // RF-1.3: the role is what closed the gate, so the very same request with an admin session
        // issued at login goes through and the endpoint answers its success code.
        var asAdmin = await DispatchAsync(app, endpoint, SessionToken(AdminAuthorization.AdminRole), method, route);

        Assert.Equal(StatusCodes.Status204NoContent, asAdmin.StatusCode);
        Assert.Equal(1, EndpointFunctionCalls(commonService, selectService));
    }

    [Theory]
    [InlineData("/enterprise/{id}", "PUT")]
    [InlineData("/enterprisev/{id}", "PUT")]
    public async Task UpdateEndpoints_RequireAdminRole_403WhenCorruptedSession(string route, string method)
    {
        var commonService = new FakeEnterpriseCommonService();
        var selectService = new FakeEnterpriseSelectService();

        await using var app = BuildTestApplication(commonService, selectService);
        var endpoint = Find(app, route, method);

        Assert.Contains(AuthorizationsOf(endpoint), data => data.Policy == AdminAuthorization.PolicyName);

        // RF-1.5, CE-9: a manipulated session keeps a valid signature, so it is authenticated and the
        // answer cannot be a 401. What was broken is the Role claim the comparison reads: absent,
        // empty, or holding a role the system does not know. None of them can be compared, so the
        // route answers 403.
        // A role moved to a differently cased claim is NOT one of them: ClaimsIdentity compares claim
        // types ignoring case, so "role" would still satisfy the requirement and the session would
        // simply be an admin one.
        var withoutRoleClaim = ManipulatedSessionToken(
            ClaimsOfSession(role: null).Where(claim => claim.Type != JwtTokenFactory.RoleClaimType));

        var withEmptyRoleClaim = ManipulatedSessionToken(
            ClaimsOfSession(role: null).Append(new Claim(JwtTokenFactory.RoleClaimType, string.Empty)));

        var withUnknownRole = ManipulatedSessionToken(ClaimsOfSession(UnknownRole));

        var manipulatedSessions = new[]
        {
            withoutRoleClaim,
            withEmptyRoleClaim,
            withUnknownRole
        };

        foreach (var manipulatedSession in manipulatedSessions)
        {
            var response = await DispatchAsync(app, endpoint, manipulatedSession, method, route);

            Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
            Assert.NotEqual(StatusCodes.Status401Unauthorized, response.StatusCode);
        }

        // RF-1.5, CE-9: no vital function of the endpoint ran for any of them.
        Assert.Equal(0, EndpointFunctionCalls(commonService, selectService));
    }

    [Fact]
    public async Task UpdateEndpoints_WithABrokenSignature_IsNotASessionAtAll_401()
    {
        // The boundary of CE-9: a session whose signature does not match its content is not an
        // authenticated session, so there is no role to compare and the answer is the 401 of RF-1.2
        // and CE-7, never the 403 of CE-9.
        var commonService = new FakeEnterpriseCommonService();
        var selectService = new FakeEnterpriseSelectService();

        await using var app = BuildTestApplication(commonService, selectService);
        var endpoint = Find(app, "/enterprise/{id}", "PUT");

        var response = await DispatchAsync(
            app,
            endpoint,
            TokenWithBrokenSignature(SessionToken(AdminAuthorization.AdminRole)),
            "PUT",
            "/enterprise/{id}");

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal(0, EndpointFunctionCalls(commonService, selectService));

        // The very same session with an intact signature is the 204 below, so the 401 comes from the
        // signature and from nothing else.
        var withIntactSignature = await DispatchAsync(
            app,
            endpoint,
            SessionToken(AdminAuthorization.AdminRole),
            "PUT",
            "/enterprise/{id}");

        Assert.Equal(StatusCodes.Status204NoContent, withIntactSignature.StatusCode);
    }

    [Theory]
    [InlineData("/enterprise/", "POST", StatusCodes.Status201Created)]
    [InlineData("/enterprise/{id}", "GET", StatusCodes.Status200OK)]
    [InlineData("/enterprisesct/", "GET", StatusCodes.Status200OK)]
    public async Task ReadEndpoints_AcceptAnyAuthenticatedRole_Not403(string route, string method, int expectedStatusCode)
    {
        var commonService = new FakeEnterpriseCommonService
        {
            InfoResult = StoredEnterprise(),
            AllResult = [StoredEnterprise()]
        };
        var selectService = new FakeEnterpriseSelectService { SelectResult = [StoredEnterprise()] };

        await using var app = BuildTestApplication(commonService, selectService);
        var endpoint = Find(app, route, method);

        // RF-1.6: these four routes demand a session but never the admin role, whatever it is.
        Assert.NotEmpty(AuthorizationsOf(endpoint));
        Assert.DoesNotContain(AuthorizationsOf(endpoint), data => data.Policy == AdminAuthorization.PolicyName);

        // A non admin session issued at login opens the gate, so the answer is neither 401 nor 403...
        var response = await DispatchAsync(app, endpoint, SessionToken(UserRole), method, route);

        Assert.NotEqual(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal(expectedStatusCode, response.StatusCode);

        // ...and the endpoint really ran, answering its success code.
        Assert.Equal(1, EndpointFunctionCalls(commonService, selectService));
    }

    #endregion

    private static EnterpriseDTO StoredEnterprise()
    {
        return new EnterpriseDTO
        {
            Id = 5,
            CommercialName = "Nombre comercial de la empresa",
            TradeName = "Nombre de la empresa",
            Visibility = "ENABLED"
        };
    }
}
