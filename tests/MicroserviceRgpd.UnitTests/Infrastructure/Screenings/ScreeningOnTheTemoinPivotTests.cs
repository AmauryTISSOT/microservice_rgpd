using System.Globalization;
using System.Reflection;
using System.Text;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings;

/// <summary>
/// Le contrôle de fumée du moteur réel : le <b>pivot témoin du corpus</b> entre, et ce que le
/// montage gelé du banc rendait sur ces 72 colonnes est écrit ici <b>en clair</b>, ligne par ligne.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ce n'est pas une re-mesure du banc, et ça ne doit jamais le devenir.</b> Le banc est clos :
/// aucun F2, aucun écart, aucune moyenne n'est recalculé ici, et un chiffre de qualité qui
/// apparaîtrait dans ce fichier serait une réouverture déguisée. La seule question posée est celle
/// du <b>portage</b> : le C# rend-il, mot pour mot, ce que le montage
/// <c>règles + lexique FR+EN</c> rendait ?
/// </para>
/// <para>
/// <b>Les attendus sortent du montage gelé, jamais du portage.</b> Ils ont été produits en exécutant
/// <c>exploration/banc-screening/banc.py</c> — la classe <c>ReglesLexique</c> montée sur les deux
/// dictionnaires de <c>d413d55</c> — sur ce même pivot. Les recopier depuis la sortie du C#
/// transformerait ce contrôle en tautologie, et il afficherait vert sur un portage faux.
/// </para>
/// <para>
/// <b>Le témoin est le bon banc d'essai pour cela</b> : le protocole du banc le tient hors des deux
/// côtés — ni entraînement, ni mesure — précisément pour qu'il reste disponible comme schéma de
/// fumée.
/// </para>
/// <para>
/// ⚠️ Deux propriétés du moteur se lisent en clair dans cette table, et ce sont des <b>propriétés
/// mesurées</b>, pas des défauts à corriger : <c>jour</c> rapproché de <c>journalisation</c> et
/// <c>passee_le</c> rapproché de <c>passe</c> sont des signalements faux — le degré
/// <c>Morphological</c> se trompe à 94 % au banc — et <b>aucune</b> ligne ne tombe sur
/// <c>PersonalDataUncategorised</c>, parce qu'un dictionnaire ne sait pas avouer. Ajouter une
/// heuristique hors gel pour corriger l'une ou l'autre romprait le gel des lexiques (#155).
/// </para>
/// </remarks>
public class ScreeningOnTheTemoinPivotTests
{
  /// <summary>
  /// Ce que le montage gelé rend sur le pivot témoin : une ligne par colonne, dans l'ordre du
  /// relevé — <c>table.colonne | catégorie | degré | motif</c>, et <c>—</c> là où la ligne n'a ni
  /// degré ni motif parce que rien n'a été vu.
  /// </summary>
  private static readonly string[] WhatTheFrozenMontageRendered =
  [
    "adresses.id | Unflagged | — | —",
    "adresses.client_id | Unflagged | — | —",
    "adresses.destinataire | Unflagged | — | —",
    "adresses.ligne1 | Unflagged | — | —",
    "adresses.ligne2 | Unflagged | — | —",
    "adresses.code_postal | ContactDetails | ExactName | jeton « postal » du nom de colonne, entrée du lexique",
    "adresses.ville | ContactDetails | ExactName | jeton « ville » du nom de colonne, entrée du lexique",
    "adresses.pays | ContactDetails | ExactName | jeton « pays » du nom de colonne, entrée du lexique",
    "adresses.principale | Unflagged | — | —",
    "annonces.id | Unflagged | — | —",
    "annonces.vendeur_id | Unflagged | — | —",
    "annonces.titre | Unflagged | — | —",
    "annonces.description | Unflagged | — | —",
    "annonces.prix_centimes | Unflagged | — | —",
    "annonces.etat | Unflagged | — | —",
    "annonces.publiee_le | Unflagged | — | —",
    "annonce_photos.id | Unflagged | — | —",
    "annonce_photos.annonce_id | Unflagged | — | —",
    "annonce_photos.fichier | Unflagged | — | —",
    "clients.id | Unflagged | — | —",
    "clients.email | ContactDetails | ExactName | jeton « email » du nom de colonne, entrée du lexique",
    "clients.prenom | Identity | ExactName | jeton « prenom » du nom de colonne, entrée du lexique",
    "clients.nom | Identity | ExactName | jeton « nom » du nom de colonne, entrée du lexique",
    "clients.telephone | ContactDetails | ExactName | jeton « telephone » du nom de colonne, entrée du lexique",
    "clients.mot_de_passe_hash | AuthenticationSecret | ExactName | jeton « hash » du nom de colonne, entrée du lexique; jeton « passe » du nom de colonne, entrée du lexique",
    "clients.cree_le | Unflagged | — | —",
    "clients.derniere_connexion | ConnectionData | ExactName | jeton « connexion » du nom de colonne, entrée du lexique",
    "clients_ancienne_boutique.id_ancien | ProfessionalLife | Morphological | jeton « ancien » rapproché de l'entrée « anciennete »",
    "clients_ancienne_boutique.mail | ContactDetails | ExactName | jeton « mail » du nom de colonne, entrée du lexique",
    "clients_ancienne_boutique.nom_complet | Identity | ExactName | jeton « nom » du nom de colonne, entrée du lexique",
    "clients_ancienne_boutique.ville | ContactDetails | ExactName | jeton « ville » du nom de colonne, entrée du lexique",
    "clients_ancienne_boutique.importe_le | Unflagged | — | —",
    "commandes.id | Unflagged | — | —",
    "commandes.reference | Unflagged | — | —",
    "commandes.client_id | Unflagged | — | —",
    "commandes.courriel_acheteur | ContactDetails | ExactName | jeton « courriel » du nom de colonne, entrée du lexique",
    "commandes.annonce_id | Unflagged | — | —",
    "commandes.nom_livraison | Identity | ExactName | jeton « nom » du nom de colonne, entrée du lexique",
    "commandes.adresse_livraison | ContactDetails | ExactName | jeton « adresse » du nom de colonne, entrée du lexique",
    "commandes.total_centimes | Unflagged | — | —",
    "commandes.statut | Unflagged | — | —",
    "commandes.passee_le | AuthenticationSecret | Morphological | jeton « passee » rapproché de l'entrée « passe »",
    "factures.id | Unflagged | — | —",
    "factures.numero | Unflagged | — | —",
    "factures.commande_id | Unflagged | — | —",
    "factures.emise_le | Unflagged | — | —",
    "factures.destinataire_nom | Identity | ExactName | jeton « nom » du nom de colonne, entrée du lexique",
    "factures.destinataire_adresse | ContactDetails | ExactName | jeton « adresse » du nom de colonne, entrée du lexique",
    "factures.destinataire_courriel | ContactDetails | ExactName | jeton « courriel » du nom de colonne, entrée du lexique",
    "factures.total_centimes | Unflagged | — | —",
    "messages.id | Unflagged | — | —",
    "messages.fil_id | Unflagged | — | —",
    "messages.auteur_courriel | ContactDetails | ExactName | jeton « courriel » du nom de colonne, entrée du lexique",
    "messages.auteur_role | Unflagged | — | —",
    "messages.corps | Unflagged | — | —",
    "messages.envoye_le | Unflagged | — | —",
    "newsletter.courriel | ContactDetails | ExactName | jeton « courriel » du nom de colonne, entrée du lexique",
    "newsletter.inscrit_le | Unflagged | — | —",
    "newsletter.source | Unflagged | — | —",
    "newsletter.desinscrit_le | Unflagged | — | —",
    "paiements.id | Unflagged | — | —",
    "paiements.commande_id | Unflagged | — | —",
    "paiements.prestataire | Unflagged | — | —",
    "paiements.reference_psp | Unflagged | — | —",
    "paiements.porteur_nom | Identity | ExactName | jeton « nom » du nom de colonne, entrée du lexique",
    "paiements.carte_4_derniers | Unflagged | — | —",
    "paiements.montant_centimes | Unflagged | — | —",
    "paiements.paye_le | FinancialData | ExactName | jeton « paye » du nom de colonne, entrée du lexique",
    "stats_annonces_jour.jour | ConnectionData | Morphological | jeton « jour » rapproché de l'entrée « journalisation »",
    "stats_annonces_jour.annonce_id | Unflagged | — | —",
    "stats_annonces_jour.vues | Unflagged | — | —",
    "stats_annonces_jour.visiteurs_uniques | Unflagged | — | —",
  ];

  /// <summary>
  /// Le portage rend, colonne par colonne et mot pour mot, ce que le montage gelé rendait.
  /// </summary>
  [Fact]
  public async Task RendersWhatTheFrozenMontageRenderedOnEveryColumnOfTheTemoin()
  {
    var listing = TheTemoinPivot();

    var screened = await AScreeningEngine.Wired().ScreenAsync(listing);

    screened.Columns.Select(Line).ShouldBe(WhatTheFrozenMontageRendered);
  }

  /// <summary>
  /// ⚠️ <b>Une ligne par colonne du relevé, sans exception.</b> C'est le mécanisme entier de
  /// l'<c>Omission relue</c> : une colonne absente du rapport est une colonne que personne ne relit
  /// jamais. Un moteur qui ne rendrait que ses signalements — 27 lignes sur 72 — laisserait le
  /// rapport se lire comme complet.
  /// </summary>
  [Fact]
  public async Task RendersOneLinePerListedColumnAndNotOnlyItsFlaggedOnes()
  {
    var listing = TheTemoinPivot();

    var screened = await AScreeningEngine.Wired().ScreenAsync(listing);

    screened.Columns.Count.ShouldBe(listing.ColumnCount);
    screened.Columns.Select(column => column.Identity).ShouldBe(listing.Columns.Select(column => column.Identity));
    screened.Columns.Count(column => column.IsFlagged).ShouldBeLessThan(screened.Columns.Count);
  }

  /// <summary>
  /// L'invariant du contexte tenu bout à bout sur un relevé réel : <b>motif présent ⇔ ce n'est pas
  /// <c>Unflagged</c></b>, et un degré exactement là où une règle a déclenché.
  /// </summary>
  [Fact]
  public async Task LeavesNoFlaggedLineWithoutAReasonAndNoUnflaggedLineWithOne()
  {
    var screened = await AScreeningEngine.Wired().ScreenAsync(TheTemoinPivot());

    foreach (var column in screened.Columns)
    {
      (column.Reason is not null).ShouldBe(column.IsFlagged, column.Identity.ToString());
      (column.Strength is not null).ShouldBe(column.IsFlagged, column.Identity.ToString());
    }
  }

  /// <summary>
  /// <b>Déterministe</b> : deux détections du même relevé rendent la même chose, mot pour mot. Le
  /// moteur n'a ni aléa, ni horloge, ni amont, et rien ne l'apprend en chemin.
  /// </summary>
  [Fact]
  public async Task RendersTheSameThingTwiceOnTheSameListing()
  {
    var engine = AScreeningEngine.Wired();
    var listing = TheTemoinPivot();

    var first = await engine.ScreenAsync(listing);
    var second = await engine.ScreenAsync(listing);

    second.Columns.Select(Line).ShouldBe(first.Columns.Select(Line));
    second.Engine.ShouldBe(first.Engine);
  }

  /// <summary>
  /// Le rapport de détection porte <b>qui</b> a détecté : le montage retenu par le banc, et le gel
  /// dont ses
  /// lexiques sortent. C'est ce qui dit à l'humain pourquoi un nouveau rapport diffère de l'ancien.
  /// </summary>
  [Fact]
  public async Task JoinsTheNameAndTheVersionOfTheEngineThatScreened()
  {
    var screened = await AScreeningEngine.Wired().ScreenAsync(TheTemoinPivot());

    screened.Engine.Name.ShouldBe("regles-lexique-fr-en");
    screened.Engine.Version.ShouldBe("regles-1+lexiques-d413d55");
  }

  /// <summary>
  /// Le pivot témoin du corpus, tel qu'il est versionné, ingéré par la frontière du domaine.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Deux détails de forme sont convertis, et seulement eux</b> : la nullabilité, écrite en
  /// <c>0</c>/<c>1</c>, et la ligne de fin, qui ferme sur <c>{"colonnes":72}</c> sans
  /// <c>"fin":true</c>. Le corpus est <b>gelé dans une forme antérieure à</b> <c>pivot-format.md</c>
  /// et le reste délibérément : <c>corpus/schemas/pivots/README.md</c> dit pourquoi, et c'est là
  /// qu'il faut lire avant de songer à le réextraire.
  /// <para>
  /// ⚠️ <b>La conversion vit ici, et nulle part ailleurs.</b> Il n'y a qu'un appelant ; la remonter
  /// dans l'ingestion ferait accepter au service, en production, la forme que <c>pivot-format.md</c>
  /// a précisément écartée.
  /// </para>
  /// </remarks>
  private static ColumnListing TheTemoinPivot()
  {
    using var stream = typeof(ScreeningOnTheTemoinPivotTests).GetTypeInfo().Assembly
      .GetManifestResourceStream("temoin.jsonl")
      ?? throw new InvalidOperationException("Le pivot témoin du corpus n'est pas embarqué dans les tests.");

    using var reader = new StreamReader(stream, Encoding.UTF8);

    var lines = reader.ReadToEnd()
      .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
      .Select(line => line
        .Replace("\"nullable\": 0", "\"nullable\": false", StringComparison.Ordinal)
        .Replace("\"nullable\": 1", "\"nullable\": true", StringComparison.Ordinal))
      .ToList();

    lines[^1] = lines[^1].Replace("{\"colonnes\"", "{\"fin\":true,\"colonnes\"", StringComparison.Ordinal);

    var outcome = ColumnListingIngestion.Ingest(string.Join('\n', lines));

    outcome.Refusal.ShouldBeNull();

    return outcome.Listing!;
  }

  /// <summary>Une ligne du rapport, écrite comme les attendus : lisible d'un coup d'œil en cas de rouge.</summary>
  private static string Line(ScreenedColumn column)
  {
    return string.Create(
      CultureInfo.InvariantCulture,
      $"{column.Identity.Table}.{column.Identity.Column} | {column.Category.Name} | {column.Strength?.Name ?? "—"} | {column.Reason ?? "—"}");
  }
}
