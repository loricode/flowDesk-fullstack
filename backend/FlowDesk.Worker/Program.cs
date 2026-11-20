using FlowDesk.Application;
using FlowDesk.Application.Abstractions;
using FlowDesk.Infrastructure;
using FlowDesk.Infrastructure.Services;

// FlowDesk.Worker: proceso independiente y escalable horizontalmente.
// Publica el Outbox (en SQS o en proceso), consume la cola y ejecuta el procesamiento pesado
// (hash, validación profunda, duplicados, notificaciones) sin bloquear a los usuarios.
var builder = Host.CreateApplicationBuilder(args);

if (!builder.Environment.IsDevelopment())
    builder.Logging.AddJsonConsole();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddMessageProcessing(builder.Configuration)
    .AddHttpRealtimeRelay(builder.Configuration);

builder.Services.AddScoped<SystemCurrentUser>();
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<SystemCurrentUser>());

var host = builder.Build();
host.Run();
