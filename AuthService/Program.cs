using AuthService.Api.Extensions;
using AuthService.Api.Middleware;
using AuthService.Application.Interfaces;
using AuthService.Application.Options;
using AuthService.Application.Services;
using AuthService.Domain.Enums;
using AuthService.Grpc.Interceptors;
using AuthService.Grpc.Services;
using AuthService.Infrastructure.Data;
using AuthService.Infrastructure.Data.Interfaces;
using AuthService.Infrastructure.Interfaces;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using System.Text;
using static EmailService.Grpc.EmailService;

var builder = WebApplication.CreateBuilder(args);

Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;


Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console(theme: AnsiConsoleTheme.Code)
    .WriteTo.File("logs/authservice-.log",
        rollingInterval: RollingInterval.Day,
        fileSizeLimitBytes: 10 * 1024 * 1024,
        retainedFileCountLimit: 30,
        rollOnFileSizeLimit: true,
        shared: true,
        flushToDiskInterval: TimeSpan.FromSeconds(1))
    .CreateLogger();

builder.WebHost.ConfigureKestrel(options =>
{
    // Allow both HTTP/1.1 and HTTP/2 protocols for gRPC-Web support
    options.ConfigureEndpointDefaults(listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
    });
});

builder.Host.UseSerilog();

builder.Services.AddControllers();

builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<ExceptionInterceptor>();
    options.Interceptors.Add<AuthInterceptor>();
    options.Interceptors.Add<RateLimitInterceptor>();
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});


builder.Services.AddEndpointsApiExplorer();



builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Auth API (REST & gRPC)", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT Bearer token **_only_** (Example: `Bearer eyJhbGci...`)"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] {}
        }
    });
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddDatabase(builder.Configuration);

// Options Configuration
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("JwtOptions"));
var jwtOptions = builder.Configuration.GetSection("JwtOptions").Get<JwtOptions>();

if (jwtOptions == null || string.IsNullOrWhiteSpace(jwtOptions.Secret))
    throw new InvalidOperationException("JWT Secret is missing from appsettings.json!");

Console.WriteLine($"JWT Secret Loaded: (len={jwtOptions.Secret.Length})");

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = true;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ClockSkew = TimeSpan.Zero
        };
    });
	var emailServiceUrl = builder.Configuration["GrpcClients:EmailService"];
	if (string.IsNullOrWhiteSpace(emailServiceUrl))
	{
		Log.Warning("GrpcClients:EmailService is not configured. Registering a placeholder client; email sending will fail if invoked.");
		emailServiceUrl = "http://localhost:5001"; 
	}

	
            
    builder.Services.AddGrpcClient<EmailServiceClient>(o =>
    {
        o.Address = new Uri(emailServiceUrl);
    })
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        return new GrpcWebHandler(GrpcWebMode.GrpcWebText, new HttpClientHandler());
    });


var connectionString = builder.Configuration.GetConnectionString("AuthDb");

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string 'AuthDb' not found.");
}

// This part is PERFECT. It creates the correctly configured data source.
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.MapEnum<RoleType>("auth.role_type_enum");
var dataSource = dataSourceBuilder.Build();
builder.Services.AddSingleton(dataSource);
builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();

builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection("RateLimitOptions"));

// Register Application Services (after all dependencies are registered)
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IDigitalFingerprintService, DigitalFingerprintService>();
builder.Services.AddScoped<IAuthService, AuthService.Application.Services.AuthService>();

// Register AccountService with all dependencies verified
try
{
    builder.Services.AddScoped<IAccountService, AccountService>();
    Log.Information("✅ IAccountService registered successfully");
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ Failed to register IAccountService");
    throw;
}

builder.Services.AddHttpClient();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

	var apiAuthDb = builder.Configuration.GetConnectionString("AuthDb")
		?? throw new InvalidOperationException("ConnectionStrings:AuthDb is missing or empty for API host.");
	builder.Services.AddHealthChecks()
		.AddNpgSql(
			apiAuthDb,
			name: "postgres",
			tags: new[] { "db", "postgres" });

builder.Services.AddGrpcHealthChecks()
    .AddCheck("Sample", () => HealthCheckResult.Healthy());

var app = builder.Build();

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Auth Service API v1");
        c.RoutePrefix = string.Empty;
    });


app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseSerilogRequestLogging();

app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();
app.UseMiddleware<DigitalFingerprintMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });

// Map gRPC and HTTP endpoints (top-level mapping)
app.MapGrpcService<AuthGrpcService>().EnableGrpcWeb();
app.MapGrpcHealthChecksService();
app.MapGet("/", () => "gRPC Server running...");


// Verify Dependency Injection Setup
using (var scope = app.Services.CreateScope())
{
    try
    {
        // Test database connection
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (db.Database.CanConnect())
            Log.Information("✅ Successfully connected to Neon PostgreSQL Database!");
        else
            Log.Error("❌ Failed to connect to Neon Database!");
        
        // Verify critical services can be resolved
        Log.Information("Verifying DI registrations...");
        
        var accountService = scope.ServiceProvider.GetRequiredService<IAccountService>();
        Log.Information("✅ IAccountService resolved successfully");
        
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        Log.Information("✅ IAuthService resolved successfully");
        
        var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        Log.Information("✅ IPasswordService resolved successfully");
        
        var userCredRepo = scope.ServiceProvider.GetRequiredService<IUserCredentialRepository>();
        Log.Information("✅ IUserCredentialRepository resolved successfully");
        
        var tokenRepo = scope.ServiceProvider.GetRequiredService<ISecurityTokenRepository>();
        Log.Information("✅ ISecurityTokenRepository resolved successfully");
        
        Log.Information("✅ All critical services verified!");
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "❌ Dependency Injection verification failed!");
        throw; // Fail fast if DI is broken
    }
}

try
{
    Log.Information("🚀 Starting Auth API...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ API terminated unexpectedly!");
}
finally
{
    Log.CloseAndFlush();
}