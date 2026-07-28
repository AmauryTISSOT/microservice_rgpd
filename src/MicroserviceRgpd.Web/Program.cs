using MicroserviceRgpd.Infrastructure.Qualifications;
using MicroserviceRgpd.Web.Configurations;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults()    // This sets up OpenTelemetry logging
       .AddLoggerConfigs();     // This adds Serilog for console formatting

using var loggerFactory = LoggerFactory.Create(config => config.AddConsole());
var startupLogger = loggerFactory.CreateLogger<Program>();

startupLogger.LogInformation("Starting web host");

// Les deux appels sortants vers les moteurs se tracent sous une source à eux, en plus de
// l'instrumentation HTTP que ServiceDefaults pose déjà : les deux moteurs partagent une adresse et
// ne diffèrent que par leur chemin, si bien qu'une trace de transport seule obligerait à lire une
// URL pour savoir lequel des deux a échoué. C'est le seul canal qui comptera ces échecs — la trace
// d'audit, par construction, n'enregistre que les verdicts rendus.
builder.Services.AddOpenTelemetry()
       .WithTracing(tracing => tracing.AddSource(QualificationTelemetry.SourceName));

// Le second garde-fou d'entrée, et celui qui protège réellement : il agit au transport, avant
// toute désérialisation, là où le plafond de 10 000 caractères ne joue qu'une fois le corps lu.
// Au-delà, Kestrel rend un 413 sans que l'application soit atteinte.
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = 64 * 1024);

builder.Services.AddOptionConfigs(builder.Configuration, startupLogger, builder);
builder.Services.AddServiceConfigs(startupLogger, builder);

builder.Services.AddProblemDetailsConfigs();

builder.Services.AddFastEndpoints()
                .SwaggerDocument(o =>
                {
                  o.DocumentSettings = s =>
                  {
                    s.Title = "Clean Architecture API";
                    s.Version = "v1";
                    s.Description = "HTTP endpoints for the Clean Architecture sample application.";
                  };
                  o.ShortSchemaNames = true;
                });

var app = builder.Build();

await app.UseAppMiddlewareAndSeedDatabase();

app.MapDefaultEndpoints(); // Aspire health checks and metrics

app.Run();

// Make the implicit Program.cs class public, so integration tests can reference the correct assembly for host building
public partial class Program { }
