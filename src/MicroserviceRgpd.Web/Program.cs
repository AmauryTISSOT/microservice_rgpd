using System.Text.Encodings.Web;
using System.Text.Unicode;
using MicroserviceRgpd.Infrastructure.Qualifications;
using MicroserviceRgpd.Web.Configurations;
using MicroserviceRgpd.Web.Pages.Shared;
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

// La surface de l'Operator est livrée par le service lui-même, server-rendered et sans framework
// client : ce qui doit rester visible par construction — un recensement qui ne se présente jamais
// comme complet — ne peut pas dépendre d'un écran qu'un tiers réécrirait.
builder.Services.AddRazorPages();

// L'échappement par défaut d'ASP.NET Core replie tout ce qui sort du latin de base en entités
// numériques : « contient » y devient `&#xAB; contient &#xBB;`, et la prose que l'Operator vient
// d'écrire cesse d'être lisible dans la source de sa propre page. Sur une surface entièrement
// française, c'est la totalité du texte. L'encodeur reste un encodeur — il continue d'échapper
// `<`, `>`, `&` et `'` —, on lui dit seulement que l'Unicode n'est pas un danger.
builder.Services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.All));

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

// La version du produit, dite une fois au démarrage — par le journal de l'application et non par
// le journal de bootstrap : c'est celui-là qui passe par OpenTelemetry, donc celui que le tableau
// de bord Aspire montre, en plus de la console. Même source que la barre : ProductVersion.
app.Logger.LogInformation("Version du produit {Version}", ProductVersion.Display);

await app.UseAppMiddlewareAndSeedDatabase();

app.MapDefaultEndpoints(); // Aspire health checks and metrics

app.Run();

// Make the implicit Program.cs class public, so integration tests can reference the correct assembly for host building
public partial class Program { }
