using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SecureAPIGateway.Middleware;
using SecureAPIGateway.Services;
using Serilog;

// ── 1. Serilog Bootstrap ──────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/gateway-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .MinimumLevel.Information()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// ── 2. Serilog ────────────────────────────────────────────────────────────────
builder.Host.UseSerilog();

// ── 3. Controllers ────────────────────────────────────────────────────────────
builder.Services.AddControllers();

// ── 4. Custom Services ────────────────────────────────────────────────────────
builder.Services.AddSingleton<IJwtService, JwtService>();

// ── 5. Swagger ────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Secure API Gateway",
        Version = "v1",
        Description = "Multi-layer security gateway with JWT, Rate Limiting, Input Validation, and AI Detection."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste your JWT token. Example: eyJhbGci..."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ── 6. JWT Authentication ─────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["JwtSettings:SecretKey"]
                  ?? throw new InvalidOperationException("JwtSettings:SecretKey is not configured.");
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"]
                  ?? throw new InvalidOperationException("JwtSettings:Issuer is not configured.");
var jwtAudience = builder.Configuration["JwtSettings:Audience"]
                  ?? throw new InvalidOperationException("JwtSettings:Audience is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ── 7. AI Detection HttpClient ────────────────────────────────────────────────
var aiBaseUrl = builder.Configuration["AiServiceSettings:BaseUrl"]
                       ?? "http://localhost:5000"; // fallback so app doesn't crash if not set
var aiTimeoutSeconds = builder.Configuration.GetValue<int>("AiServiceSettings:TimeoutSeconds", 5);

builder.Services.AddHttpClient<IAiDetectionService, AiDetectionService>(client =>
{
    client.BaseAddress = new Uri(aiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(aiTimeoutSeconds);
});

// ── 8. Build ──────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── 9. Swagger UI — enabled in ALL environments for demo purposes ─────────────
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Secure API Gateway v1");
    c.RoutePrefix = "swagger";
});

// ── 10. Middleware Pipeline ───────────────────────────────────────────────────
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();
app.UseMiddleware<InputValidationMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
Log.Information("Secure API Gateway running on port {Port}. Swagger: /swagger", port);

app.Run($"http://0.0.0.0:{port}");