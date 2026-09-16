using System.Text;
using System.Text.Json.Serialization;
using BudgetPrevisionnel.Api.Auth;
using BudgetPrevisionnel.Api.ErrorHandling;
using BudgetPrevisionnel.Api.Validation;
using BudgetPrevisionnel.Application.Auth;
using BudgetPrevisionnel.Infrastructure;
using BudgetPrevisionnel.Infrastructure.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting BudgetPrevisionnel.Api");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14));

    builder.Services.AddInfrastructure(builder.Configuration);

    var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
    if (string.IsNullOrWhiteSpace(jwtOptions.Key))
    {
        throw new InvalidOperationException(
            "Jwt:Key is not configured. In Development, set it with " +
            "`dotnet user-secrets set \"Jwt:Key\" \"<a long random string>\" --project src/BudgetPrevisionnel.Api`. " +
            "In other environments, set it via an environment variable or a secret manager - never commit it.");
    }

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            // Keeps claim names ("sub", "email") as issued instead of ASP.NET Core's
            // default remapping to the long ClaimTypes.* URIs. CurrentUser relies on this.
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });

    builder.Services.AddAuthorization();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();

    // Frontend (Lot 10+) runs on a different origin in dev (Vite's default :5173) and
    // will again in production once deployed - configured, not hardcoded, since the
    // production frontend origin isn't known yet. Empty by default (no origins allowed)
    // rather than a permissive fallback.
    const string FrontendCorsPolicy = "Frontend";
    var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(FrontendCorsPolicy, policy =>
        {
            if (corsOrigins.Length > 0)
            {
                // Content-Disposition isn't in the browser's CORS-safelisted response
                // headers by default - without exposing it explicitly, the frontend's
                // import file download couldn't read the original file name back out.
                policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()
                    .WithExposedHeaders("Content-Disposition");
            }
        });
    });

    // Scans this assembly for every AbstractValidator<T> in Api/Contracts/**/*Validators.cs.
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    // FluentValidation's built-in messages localize to the server OS's culture by
    // default (caught in Development on a fr-FR machine) - every other error message in
    // this API is a fixed English string (see Application/Common/AppException), so pin
    // this the same way instead of leaving it to depend on the deployment machine's locale.
    FluentValidation.ValidatorOptions.Global.LanguageManager.Enabled = false;

    builder.Services.AddControllers(options =>
        {
            // Runs FluentValidation for every action argument that has a registered
            // validator, ahead of the action - see ValidationActionFilter's doc comment
            // for why this is a hand-written filter instead of a third-party package.
            options.Filters.Add<ValidationActionFilter>();
        })
        // "Monthly" over "1" in requests/responses - readable, and immune to the enum being
        // reordered later (RecurrenceFrequency is the first enum this API exposes).
        .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    // Translates every AppException (see Application/Common) into its ProblemDetails
    // response, replacing the try/catch that used to live in every controller action.
    builder.Services.AddExceptionHandler<AppExceptionHandler>();
    builder.Services.AddProblemDetails();

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        var bearerScheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Paste the token returned by /api/auth/login or /api/auth/register."
        };
        options.AddSecurityDefinition("Bearer", bearerScheme);
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
        });
    });

    var app = builder.Build();

    // Request logging goes first (outermost middleware) so it always logs the FINAL
    // outcome of a request. Registered after UseExceptionHandler instead, it would sit
    // *inside* the exception handler's scope in the middleware pipeline - an exception
    // propagating up through it gets logged there as its own "responded 500", even
    // though UseExceptionHandler goes on to correctly turn it into a 404/400/etc: two
    // conflicting log lines for one request, the wrong one at ERROR level.
    app.UseSerilogRequestLogging();

    // Nothing downstream should be able to throw past this unhandled.
    app.UseExceptionHandler();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    app.UseCors(FrontendCorsPolicy);

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
        .WithName("Health")
        .ExcludeFromDescription();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "BudgetPrevisionnel.Api terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Exposed for WebApplicationFactory-based integration tests (Lot 9).
public partial class Program;
