using MicroserviceRgpd.Core.Screenings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroserviceRgpd.Web.Pages.Screenings;

/// <summary>
/// L'écran de la <b>voie connectée</b> : l'<c>Operator</c> y choisit son SGBD, donne une chaîne de
/// connexion, et le service va lire le schéma lui-même.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Les trois phrases se lisent avant qu'on ait tapé quoi que ce soit.</b> Les droits, le sort
/// de la chaîne, et le rapport courant qui part à l'archive : elles sont à côté du champ et le
/// précèdent dans le document — au-dessus de lui sur un écran étroit —, parce qu'une conséquence
/// annoncée sous le bouton est une conséquence annoncée après coup.
/// </para>
/// <para>
/// ⚠️ <b>La phrase des droits est la même quel que soit le SGBD.</b> Un texte qui varierait par
/// moteur laisserait croire que l'un des trois signale les tables masquées — aucun ne le fait, et la
/// propriété tient au fait que le service relève ce que le compte lui présente, pas au pilote.
/// </para>
/// <para>
/// ⚠️ <b>Il n'y a pas de champ de téléversement, et il n'y en aura pas.</b> Sous SQLite, le fichier
/// doit être joignable <i>par le service</i> : un <c>&lt;input type="file"&gt;</c> ferait entrer une
/// base entière — des valeurs réelles, en masse — dans un service dont la propriété première est de
/// n'en conserver aucune.
/// </para>
/// <para>
/// ⚠️ <b>La chaîne de connexion n'est pas réaffichée, jamais.</b> Elle traverse le lanceur jusqu'au
/// port, et elle ne revient pas dans le HTML — pas même après un refus : un secret d'accès recopié
/// dans une réponse se retrouve dans le cache du navigateur, dans un cliché d'écran, et dans le
/// signalement de bogue qui l'accompagne.
/// </para>
/// <para>
/// ⚠️ <b>Le dépôt collé reste, à côté, inchangé.</b> Il n'est pas une voie de secours : c'est celui
/// qu'on prend quand le service n'a pas le droit de joindre la base, ce qui est le cas le plus
/// fréquent.
/// </para>
/// </remarks>
/// <param name="launcher">Le port qui fait partir un scan, et refuse le second.</param>
public class ConnectionModel(IScanLauncher launcher) : PageModel
{
  /// <summary>
  /// La phrase des droits, <b>identique pour les trois SGBD</b>, et elle vit ici en une seule copie.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Elle dit ce que le compte doit avoir <i>et</i> ce que le service ne saura pas.</b> « Le
  /// service ne relève que ce que ce compte lui montre » se lit comme un truisme technique, dont
  /// personne ne tire qu'un rapport peut être entier et pourtant amputé d'un schéma entier.
  /// </remarks>
  public const string RightsSentence =
    "Le compte de connexion doit disposer d'un droit de lecture sur l'ensemble de la base, et non "
    + "sur une sélection de tables. Le service relève le schéma tel que ce compte le lui présente ; "
    + "il ne peut ni détecter ni signaler qu'une partie lui a été masquée.";

  /// <summary>Le SGBD choisi, par son nom pivot. Par défaut, celui du corpus d'épreuve.</summary>
  [BindProperty]
  public string? Dialect { get; set; } = DatabaseDialect.PostgreSql.PivotName;

  /// <summary>
  /// La chaîne de connexion. ⚠️ <b>Elle n'est jamais réécrite dans la réponse</b> : le champ repart
  /// vide, y compris après un refus.
  /// </summary>
  [BindProperty]
  public string? ConnectionString { get; set; }

  /// <summary>
  /// Ce que l'écran dit du scan qui courait déjà, quand un second lancement a été refusé.
  /// </summary>
  public ScanRefusalScreen? AlreadyRunning { get; private set; }

  /// <summary>Les trois SGBD que le service sait joindre, dans l'ordre où l'écran les propose.</summary>
  public static IReadOnlyList<DatabaseDialect> Dialects => DatabaseDialect.List.ToList();

  public void OnGet()
  {
    // Rien à charger : l'écran ne relit aucun rapport, il en fait naître un.
  }

  public IActionResult OnPost()
  {
    var dialect = DatabaseDialect.List.FirstOrDefault(known => known.PivotName == Dialect);

    if (dialect is null)
    {
      ModelState.AddModelError(
        nameof(Dialect),
        "Choisissez l'un des trois SGBD que le service sait joindre.");
    }

    if (string.IsNullOrWhiteSpace(ConnectionString))
    {
      ModelState.AddModelError(
        nameof(ConnectionString),
        "La chaîne de connexion est vide : sans elle, il n'y a pas de base à joindre.");
    }

    if (!ModelState.IsValid)
    {
      return Page();
    }

    var launch = launcher.Launch(dialect!, ConnectionString!);

    if (!launch.TookOff)
    {
      // ⚠️ Le refus NOMME le scan en cours, et l'écran qui le dit lui donne son identité, son SGBD,
      // sa phase et le lien vers son attente. Un « réessayez plus tard » laisserait l'Operator
      // ignorer si le service travaille pour lui ou pour quelqu'un d'autre — et il relancerait, sur
      // la production d'un tiers, une lecture déjà en cours.
      //
      // ⚠️ Ce n'est PAS une erreur de saisie, et cela ne passe donc pas par le ModelState : rien de
      // ce que l'Operator a écrit n'est en cause, et le lui dire au même endroit que « la chaîne est
      // vide » l'enverrait relire un champ qui n'a rien.
      AlreadyRunning = ScanRefusalScreen.Of(launch.AlreadyRunning!);

      return Page();
    }

    // ⚠️ Une redirection, et non l'écran d'attente rendu ici : un rechargement de la page de
    // connexion relancerait sinon un second scan sur la base du client.
    return RedirectToPage("Scan", new { scanId = launch.Started!.Value.Value });
  }
}
