using System.Globalization;
using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using BusinessJournal.Api.ExceptionHandling;
using BusinessJournal.Api.RateLimiting;
using BusinessJournal.Application.Interfaces;
using BusinessJournal.Application.Services;
using BusinessJournal.Domain.Entities;
using BusinessJournal.Infrastructure.Data.SqlServer;
using BusinessJournal.Infrastructure.Repositories;
using BusinessJournal.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ForwardedIpNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

ConfigureSwagger(builder.Services);
ConfigureOptions(builder.Services, builder.Configuration);
ConfigureForwardedHeaders(builder.Services, builder.Configuration);
ConfigureRateLimiting(builder.Services);
ConfigureAuthentication(builder.Services);
RegisterDependencies(builder.Services);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    SeedDevelopmentAdmin(app);
}

app.UseExceptionHandler();
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static void ConfigureSwagger(IServiceCollection services)
{
    services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "BusinessJournal API",
            Version = "1.0"
        });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter: Bearer {your JWT token}"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });
}

static void ConfigureOptions(IServiceCollection services, IConfiguration configuration)
{
    services
        .AddOptions<SqlServerOptions>()
        .Bind(configuration.GetSection("SqlServer"))
        .Validate(
            options => !string.IsNullOrWhiteSpace(options.ConnectionString),
            "SqlServer:ConnectionString is required.")
        .ValidateOnStart();

    services
        .AddOptions<JwtOptions>()
        .Bind(configuration.GetSection("Jwt"))
        .Validate(
            options => !string.IsNullOrWhiteSpace(options.Issuer),
            "Jwt:Issuer is required.")
        .Validate(
            options => !string.IsNullOrWhiteSpace(options.Audience),
            "Jwt:Audience is required.")
        .Validate(
            options => !string.IsNullOrWhiteSpace(options.SigningKey),
            "Jwt:SigningKey is required.")
        .Validate(
            options => options.ExpirationMinutes > 0,
            "Jwt:ExpirationMinutes must be greater than zero.")
        .ValidateOnStart();
}

static void ConfigureForwardedHeaders(IServiceCollection services, IConfiguration configuration)
{
    services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedProto;

        options.ForwardLimit = 1;

        ConfigureKnownProxies(options, configuration);
        ConfigureKnownNetworks(options, configuration);
    });
}

static void ConfigureRateLimiting(IServiceCollection services)
{
    services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));

                context.HttpContext.Response.Headers.RetryAfter =
                    seconds.ToString(CultureInfo.InvariantCulture);
            }

            var problem = new ProblemDetails
            {
                Title = "Too Many Requests",
                Detail = "Too many login attempts. Please try again later.",
                Status = StatusCodes.Status429TooManyRequests
            };

            await context.HttpContext.Response.WriteAsJsonAsync(
                problem,
                cancellationToken: cancellationToken);
        };

        options.AddPolicy(RateLimitingPolicies.Login, httpContext =>
        {
            var partitionKey = GetClientIpPartitionKey(httpContext);

            return RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey,
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(5),
                    SegmentsPerWindow = 5,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        });
    });
}

static void ConfigureAuthentication(IServiceCollection services)
{
    services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer();

    services
        .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
        .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptionsAccessor) =>
        {
            var jwtOptions = jwtOptionsAccessor.Value;

            bearerOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });

    services.AddAuthorization();
}

static void RegisterDependencies(IServiceCollection services)
{
    services.AddSingleton<SqlServerConnectionFactory>();

    services.AddScoped<ICustomerRepository, SqlCustomerRepository>();
    services.AddScoped<IAppointmentRepository, SqlAppointmentRepository>();

    services.AddSingleton<IUserRepository, InMemoryUserRepository>();
    services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
    services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

    services.AddScoped<CustomerService>();
    services.AddScoped<AppointmentService>();
    services.AddScoped<AuthService>();
}

static string GetClientIpPartitionKey(HttpContext httpContext)
{
    return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

static void ConfigureKnownProxies(
    ForwardedHeadersOptions options,
    IConfiguration configuration)
{
    var knownProxies = configuration
        .GetSection("ReverseProxy:KnownProxies")
        .Get<string[]>();

    if (knownProxies is null)
    {
        return;
    }

    foreach (var proxy in knownProxies)
    {
        if (!IPAddress.TryParse(proxy, out var ipAddress))
        {
            throw new InvalidOperationException(
                $"Invalid reverse proxy IP address: '{proxy}'.");
        }

        options.KnownProxies.Add(ipAddress);
    }
}

static void ConfigureKnownNetworks(
    ForwardedHeadersOptions options,
    IConfiguration configuration)
{
    var knownNetworks = configuration
        .GetSection("ReverseProxy:KnownNetworks")
        .Get<string[]>();

    if (knownNetworks is null)
    {
        return;
    }

    foreach (var network in knownNetworks)
    {
        var parts = network.Split(
            '/',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2
            || !IPAddress.TryParse(parts[0], out var prefix)
            || !int.TryParse(parts[1], out var prefixLength))
        {
            throw new InvalidOperationException(
                $"Invalid reverse proxy network: '{network}'. Expected format 'address/prefixLength'.");
        }

        options.KnownNetworks.Add(new ForwardedIpNetwork(prefix, prefixLength));
    }
}

static void SeedDevelopmentAdmin(WebApplication app)
{
    using var scope = app.Services.CreateScope();

    var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

    const string adminEmail = "admin@businessjournal.com";
    const string adminPassword = "Admin123!";

    if (userRepository.FindByEmail(adminEmail) is not null)
    {
        return;
    }

    var adminUser = AppUser.Create(
        adminEmail,
        passwordHasher.Hash(adminPassword),
        "Admin");

    userRepository.Add(adminUser);
}

public partial class Program
{
}