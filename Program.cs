using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SecureAPIGateway.Middleware;
using SecureAPIGateway.Services;
using Serilog;

// ── 1. Serilog Bootstrap (must be first) ─────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/gateway-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .MinimumLevel.Information()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// ── 2. Serilog for all .NET logging ──────────────────────────────────────────
builder.Host.UseSerilog();

// ── 3. Controllers ────────────────────────────────────────────────────────────
builder.Services.AddControllers();

// ── 4. Custom Services ────────────────────────────────────────────────────────
builder.Services.AddSingleton<IJwtService, JwtService>();

// ── 5. Swagger (Swashbuckle) ─────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title   = "Secure API Gateway",
        Version = "v1",
        Description = "Multi-layer security gateway with JWT, Rate Limiting, Input Validation, and AI Detection."
    });

    // Add JWT Bearer input box to Swagger UI
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name        = "Authorization",
        Type        = SecuritySchemeType.Http,
        Scheme      = "Bearer",
        BearerFormat= "JWT",
        In          = ParameterLocation.Header,
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
var jwtSecret   = builder.Configuration["JwtSettings:SecretKey"]!;
var jwtIssuer   = builder.Configuration["JwtSettings:Issuer"]!;
var jwtAudience = builder.Configuration["JwtSettings:Audience"]!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtIssuer,
            ValidAudience            = jwtAudience,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew                = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ── 7. AI Detection HttpClient ────────────────────────────────────────────────
var aiBaseUrl        = builder.Configuration["AiServiceSettings:BaseUrl"]!;
var aiTimeoutSeconds = builder.Configuration.GetValue<int>("AiServiceSettings:TimeoutSeconds", 5);

builder.Services.AddHttpClient<IAiDetectionService, AiDetectionService>(client =>
{
    client.BaseAddress = new Uri(aiBaseUrl);
    client.Timeout     = TimeSpan.FromSeconds(aiTimeoutSeconds);
});

// ── 8. Build ──────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── 9. Swagger UI (always on while in development) ───────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();       // serves:  /swagger/v1/swagger.json
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Secure API Gateway v1");
        c.RoutePrefix = "swagger"; // UI at: http://localhost:<port>/swagger
    });
}

// ── 10. Middleware Pipeline (ORDER MATTERS) ───────────────────────────────────
app.UseMiddleware<RequestLoggingMiddleware>();   // 1st — wraps everything for logging
app.UseMiddleware<RateLimitingMiddleware>();     // 2nd — block flood IPs early
app.UseMiddleware<InputValidationMiddleware>();  // 3rd — block SQLi / XSS
app.UseAuthentication();                        // 4th — validate JWT token
app.UseAuthorization();                         // 5th — enforce [Authorize]
app.MapControllers();                           // 6th — reach controllers

Log.Information("Secure API Gateway running. Swagger: http://localhost:5000/swagger");
app.Run();
