using BookonnectWorkerService;
using BookonnectWorkerService.Configuration;
using BookonnectWorkerService.Services;
using BookonnectWorkerService.Models;
using Microsoft.EntityFrameworkCore;
using BookonnectCore.Extensions;

var builder = Host.CreateApplicationBuilder(args);
// Register options.
builder.Services.Configure<VectorDBOptions>(builder.Configuration.GetSection(VectorDBOptions.SectionName));
builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection(ApiKeyOptions.SectionName));
var relationalDBSection = builder.Configuration.GetSection(RelationalDBOptions.SectionName);
builder.Services.Configure<RelationalDBOptions>(relationalDBSection);

// Register DBContext
var connectionString = relationalDBSection["Uri"] 
    ?? throw new InvalidOperationException("Connection string 'RelationalDB:Uri' not found.");
builder.Services.AddDbContext<DatabaseContext>(options =>
{
  options.UseNpgsql(connectionString);
});

builder.Services.AddScoped<IDatabaseSeedService, DatabaseSeedService>();

// Configure core library
builder.Services.AddBookonnectCore(builder.Configuration);

// Register worker service
builder.Services.AddHostedService<Worker>();


// Register http clients
builder.Services.AddHttpClient<ILLMService, LLMService>();
builder.Services.AddHttpClient<IChromaService, ChromaService>();

var host = builder.Build();
host.Run();
