using HexArch.Data.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SosMVP.Extensions;
using SosMVP.Security;

var builder = WebApplication.CreateBuilder(args);

string connectionString = builder.Configuration.GetConnectionString("Sosv6Db")
    ?? throw new InvalidOperationException("No se encontró la cadena de conexión 'Sosv6Db'.");

builder.Services.AddDbContext<Sosv6DbContext>(options => options.UseSqlServer(connectionString));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services.AddUserModule();

builder.Services.AddTypeModule();

builder.Services.AddScoped<ApiExceptionHandler>();

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

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapUserEndpoints();

app.MapTypeEndpoints();

app.Run();
