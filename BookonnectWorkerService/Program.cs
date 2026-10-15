using BookonnectWorkerService;
using BookonnectCore.Extensions;

var builder = Host.CreateApplicationBuilder(args);

// Registers options, DbContext and core services
builder.Services.AddBookonnectCore(builder.Configuration);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
