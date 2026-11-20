using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using FlowDesk.API.Endpoints;
using FlowDesk.API.Infrastructure;
using FlowDesk.Application;
using FlowDesk.Application.Abstractions;
using FlowDesk.Infrastructure;
using FlowDesk.Infrastructure.Messaging;
using FlowDesk.Infrastructure.Security;
using FlowDesk.Realtime;
using FlowDesk.Shared.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment())
    builder.Logging.AddJsonConsole(); // Logs estructurados para CloudWatch.

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddFlowDeskRealtime();

builder.Services.Configure<RealtimeRelayOptions>(builder.Configuration.GetSection(RealtimeRelayOptions.SectionName));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

// Modo "todo en uno" para demos: la API también procesa el Outbox (sin Worker separado).
if (builder.Configuration.GetValue<bool>("Worker:RunInApi"))
    builder.Services.AddMessageProcessing(builder.Configuration);

// ---------- Autenticación JWT ----------
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new();
if (jwt.SigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey debe tener al menos 32 caracteres.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            NameClaimType = "name",
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
        // SignalR (WebSockets) envía el token en la query string.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = token;
                return Task.CompletedTask;
            },
        };
    });

// ---------- Autorización basada en permisos ----------
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in FlowDeskPermissions.All)
        options.AddPolicy(permission, p => p.RequireAuthenticatedUser().RequireClaim(FlowDeskClaims.Permission, permission));
});

// Protección contra fuerza bruta en el login (configurable: las pruebas E2E inician muchas sesiones).
var loginPermits = builder.Configuration.GetValue("RateLimiting:LoginPermitsPerMinute", 10);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = loginPermits, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    .WithExposedHeaders(CorrelationIdMiddleware.HeaderName, "Content-Disposition")));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<FlowDeskExceptionHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapFlowDeskEndpoints();
app.MapHub<NotificationsHub>(NotificationsHub.Route);

app.Run();

/// <summary>Expuesto para pruebas de integración con WebApplicationFactory.</summary>
public partial class Program;
