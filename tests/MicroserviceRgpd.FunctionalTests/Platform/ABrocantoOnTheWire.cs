using System.Net;
using System.Text;
using System.Text.Json;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Infrastructure.Casework.Adapters;

namespace MicroserviceRgpd.FunctionalTests.Platform;

/// <summary>
/// L'<c>Adapter</c> du témoin, posé sur le fil : <b>deux systèmes de deux natures</b>, et le cas dur
/// qui a fait naître la forme de la réponse de <c>locate</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le cas dur, en toutes lettres.</b> La base porte <b>deux « Jean Dupont »</b> — l'un est le
/// demandeur, l'autre un homonyme —, et le journal applicatif est <b>sensible à la casse</b> là où la
/// base ne l'est pas. Chercher « Jean Dupont » en base rend donc deux lignes qu'aucune machine ne
/// peut départager, et le journal ne rend rien tant qu'on ne lui donne pas l'adresse <b>dans la casse
/// où elle y est écrite</b>.
/// </para>
/// <para>
/// <b>La doublure est posée sur le fil, jamais sur le port.</b> L'en-tête de secret, le
/// <c>system_id</c> en paramètre et le corps du sac <b>sont</b> le contrat : un port doublé aurait
/// prouvé le comportement d'une doublure, et ces tests-là ne prouveraient plus rien de ce que le
/// service envoie chez le client.
/// </para>
/// </remarks>
public sealed class ABrocantoOnTheWire : HttpMessageHandler
{
  /// <summary>Le système de nature « base » : deux homonymes, casse indifférente.</summary>
  public const string Boutique = "brocanto-boutique";

  /// <summary>Le système de nature « fichiers plats » : rien n'y est indexé, et la casse compte.</summary>
  public const string Journal = "brocanto-journal";

  /// <summary>L'adresse déclarée dans le <c>Manifest</c> pour les deux systèmes.</summary>
  public const string Address = "https://brocanto.example.fr/rgpd";

  /// <summary>
  /// L'adresse telle que le journal l'a <b>écrite</b>. La base la porte en minuscules ; le demandeur
  /// l'a donnée en minuscules ; c'est la réserve de la base, une fois arbitrée, qui la fait entrer au
  /// sac dans cette casse-là — et c'est elle seule qui ouvre le journal.
  /// </summary>
  public const string AsTheJournalWroteIt = "Jean.Dupont@Example.fr";

  private readonly List<string> _bodies = [];

  /// <summary>Les corps envoyés, lus au passage : c'est là que se voit le sac qui s'enrichit.</summary>
  public IReadOnlyList<string> Bodies => _bodies;

  /// <summary>Ce qui est parti vers un système donné, dans l'ordre.</summary>
  public IReadOnlyList<string> BodiesSentTo(string system)
  {
    return [.. _sentTo.Where(sent => sent.System == system).Select(sent => sent.Body)];
  }

  private readonly List<(string System, string Body)> _sentTo = [];

  protected override async Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request,
    CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var body = request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken);
    var system = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query)[AdapterWire.SystemParameter] ?? string.Empty;

    _bodies.Add(body);
    _sentTo.Add((system, body));

    return new HttpResponseMessage(HttpStatusCode.OK)
    {
      Content = new StringContent(AnswerFor(system, body), Encoding.UTF8, "application/json"),
    };
  }

  /// <summary>
  /// Ce que chaque système répond au sac qu'on lui présente, dans <b>son</b> vocabulaire.
  /// </summary>
  private static string AnswerFor(string system, string body)
  {
    var designations = Designations(body);

    if (system == Boutique)
    {
      // La base ne distingue pas la casse. Sous le seul nom, elle trouve deux lignes et n'en tranche
      // aucune : elle rend une réserve MOTIVÉE, et propose l'adresse de chacune — c'est le seul champ
      // que le service interprétera, et seulement après qu'un humain aura tranché.
      if (!designations.Any(designation => designation.Kind == "name"))
      {
        return """{"certain": [], "reserved": []}""";
      }

      return $$"""
        {
          "certain": [],
          "reserved": [
            {
              "reference": "clients#1203",
              "reason": "Deux comptes portent le nom « Jean Dupont ». Celui-ci a commandé en mars 2026 et son adresse est jean.dupont@example.fr.",
              "designations": [{ "kind": "email", "value": "{{AsTheJournalWroteIt}}" }]
            },
            {
              "reference": "clients#4417",
              "reason": "Le second compte au nom de « Jean Dupont » : créé en 2019, aucune commande, adresse j.dupont1954@example.fr.",
              "designations": [{ "kind": "email", "value": "j.dupont1954@example.fr" }]
            }
          ]
        }
        """;
    }

    // Le journal est un fichier plat, sensible à la casse : il ne connaît que l'adresse telle qu'il
    // l'a écrite. Le nom ne s'y cherche pas — une ligne n'en porte aucun.
    var found = designations.Any(designation =>
      designation.Kind == "email" && designation.Value == AsTheJournalWroteIt);

    return found
      ? """{"certain": ["brocanto.log:2026-03"], "reserved": []}"""
      : """{"certain": [], "reserved": []}""";
  }

  private static (string Kind, string Value)[] Designations(string body)
  {
    using var read = JsonDocument.Parse(body);

    if (!read.RootElement.TryGetProperty("designations", out var bag))
    {
      return [];
    }

    return
    [
      .. bag.EnumerateArray().Select(one => (
        one.GetProperty("kind").GetString() ?? string.Empty,
        one.GetProperty("value").GetString() ?? string.Empty)),
    ];
  }
}
