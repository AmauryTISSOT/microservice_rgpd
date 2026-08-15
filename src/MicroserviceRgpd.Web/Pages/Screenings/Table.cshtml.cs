using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.UseCases.Screenings.ArbitrateColumn;
using MicroserviceRgpd.UseCases.Screenings.ArbitrateTableInBatch;
using MicroserviceRgpd.UseCases.Screenings.ReadScreeningTable;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroserviceRgpd.Web.Pages.Screenings;

/// <summary>
/// L'écran d'<b>une table</b> du rapport de détection courant : toutes ses colonnes, dans l'ordre du
/// relevé, et la <c>Clause d'incomplétude</c> qui accompagne obligatoirement la réponse.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>La table est l'unité de travail, et cet écran est cette décision rendue.</b> Le commentaire
/// de table éclaire toutes ses colonnes, et le voisinage de <c>adr_l1</c>, <c>adr_l2</c>, <c>cp</c>,
/// <c>ville</c> ne se lit pas colonne isolée.
/// </para>
/// <para>
/// ⚠️ <b>Aucun filtre, et pas de bouton pour en poser un.</b> Les <c>Unflagged</c> <b>sont</b>
/// l'écran — 92,5 % du contenu réel d'un relevé — et un écran qui ne rendrait que les signalées
/// serait un écran où l'omission a cessé d'être relisible.
/// </para>
/// <para>
/// ⚠️ <b>Le schéma et la table passent en paramètres de requête, jamais dans le chemin.</b> Un nom
/// d'objet peut porter un point ou une barre oblique, que la base rend tels quels : les coudre dans
/// une adresse aurait fait dépendre la lecture d'une table de la façon dont son nom se découpe.
/// </para>
/// </remarks>
public class TableModel(IMediator mediator) : PageModel
{
  /// <summary>Le schéma dont le relevé dit que la table vient.</summary>
  [BindProperty(SupportsGet = true)]
  public string? Schema { get; set; }

  /// <summary>La table, telle que le relevé la nomme.</summary>
  [BindProperty(SupportsGet = true)]
  public string? Table { get; set; }

  /// <summary>
  /// La colonne que le bouton cliqué désigne. <b>Elle ne vit que sur le POST</b> : l'écran rend
  /// toujours la table entière, jamais une colonne seule.
  /// </summary>
  [BindProperty]
  public string? Column { get; set; }

  /// <summary>
  /// L'issue rendue, sous le nom que porte le bouton. ⚠️ <b>Les deux boutons sont la seule source
  /// de ce champ</b> : rien n'est pré-coché, et il n'existe pas de liste où « retenue » attendrait
  /// déjà d'être validée.
  /// </summary>
  [BindProperty]
  public string? Ruling { get; set; }

  /// <summary>Le nom saisi par celui qui tranche. Non authentifié, et non facultatif.</summary>
  [BindProperty]
  public string? SignedBy { get; set; }

  /// <summary>
  /// Le rapport de détection que l'écran rendait quand l'humain a cliqué. ⚠️ <b>Il ne désigne pas
  /// où écrire</b> — le geste écrit toujours le courant — <b>il permet de refuser</b> quand le
  /// courant a changé entre le rendu et le clic.
  /// </summary>
  [BindProperty]
  public string? Screening { get; set; }

  /// <summary>
  /// La clé sous laquelle un geste refusé laisse au rapport de détection ce qu'il a à dire.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Cet écran ÉCRIT cette clé et ne la lit jamais</b>, et ce n'est pas un détail : une
  /// propriété <c>[TempData]</c> se charge — donc se consomme — à chaque requête de la page qui la
  /// porte. Posée ici, elle aurait été avalée par le premier affichage de la table, et la phrase
  /// n'aurait plus atteint le rapport de détection auquel elle est destinée.
  /// </remarks>
  internal const string NoticeKey = "Notice";

  /// <summary>
  /// La clé sous laquelle le <b>geste de lot</b> laisse à cet écran-ci ce qu'il vient de faire.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Elle est distincte de <see cref="NoticeKey"/> parce qu'elle s'écrit et se lit sur des
  /// écrans différents.</b> Le lot réussi revient ICI — il n'y a rien à faire ailleurs après avoir
  /// tranché une table — et sa phrase est le seul moyen de savoir combien de colonnes il a touchées :
  /// un renvoi muet aurait fait relire la table entière pour compter à la main, ce qui est
  /// exactement le coût que ce geste existe pour supprimer.
  /// </remarks>
  internal const string BatchNoticeKey = "BatchNotice";

  /// <summary>La table lue, et la clause qui l'accompagne obligatoirement.</summary>
  public ScreeningAnswer<ScreenedTable>? Answer { get; private set; }

  /// <summary>
  /// Ce que le geste de lot a fait, dit à celui qui vient de le poser. ⚠️ <b>La lecture consomme la
  /// phrase</b>, et cet écran est le seul qui la lise.
  /// </summary>
  public string? BatchNotice => TempData[BatchNoticeKey] as string;

  public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
  {
    return await ReadTheTableAsync(cancellationToken);
  }

  /// <summary>
  /// Porte sur une colonne l'issue qu'un humain vient de rendre, puis <b>redirige vers la table</b>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Le succès redirige, il ne rend pas la page.</b> Un rechargement rejouerait sinon le
  /// dernier arbitrage — inoffensif ici, l'écriture étant idempotente, mais l'<c>Operator</c>
  /// travaille trois jours sur cet écran et le navigateur lui demanderait de renvoyer le formulaire
  /// à chaque retour en arrière.
  /// </para>
  /// <para>
  /// ⚠️ <b>Aucune date ne circule.</b> Le formulaire n'en porte pas de champ, et il ne doit jamais
  /// en porter : l'instant est posé par le service, sur la seule trace que ce contexte garde d'un
  /// acte humain.
  /// </para>
  /// </remarks>
  public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(Schema)
      || string.IsNullOrWhiteSpace(Table)
      || string.IsNullOrWhiteSpace(Column))
    {
      return NothingWasArbitrated(NothingDesignated);
    }

    ColumnIdentity column;

    try
    {
      column = ColumnIdentity.Of(Schema, Table, Column);
    }
    catch (ArgumentException)
    {
      // Un triplet démesuré ou porteur d'un caractère de contrôle n'a pu venir d'aucun bouton de
      // cet écran : le domaine tient qu'un tel triplet est une programmation fautive, jamais une
      // saisie, et on ne fabrique donc pas un refus en français pour un formulaire forgé.
      return NothingWasArbitrated(NothingDesignated);
    }

    // ⚠️ Un nom d'état inconnu se traite comme le triplet forgé, et pour la même raison : les deux
    // boutons sont la seule source de ce champ. Lui rédiger un refus aurait écrit une seconde fois,
    // ici, une règle que le domaine énonce déjà — mais le silence est exclu : la réponse du succès
    // est une redirection, et deux redirections identiques ne se distinguent pas.
    if (!ScreenedColumnState.TryFromName(Ruling ?? string.Empty, out var ruling))
    {
      return NothingWasArbitrated(NothingDesignated);
    }

    if (!Guid.TryParse(Screening, out var read) || read == Guid.Empty)
    {
      return NothingWasArbitrated(NothingDesignated);
    }

    var arbitrated = await mediator.Send(
      new ArbitrateColumnCommand(column, ScreeningId.From(read), ruling, SignedBy),
      cancellationToken);

    // Aucun rapport de détection courant, ou un courant qui ne porte pas cette colonne : l'écran
    // affiché est périmé. Le rapport de détection dit ce que le déploiement a réellement.
    if (arbitrated.Status == ResultStatus.NotFound)
    {
      return NothingWasArbitrated(
        "La colonne que vous veniez de trancher n'est plus dans le rapport de détection courant : "
        + "rien n'a été enregistré. Voici le rapport de détection tel qu'il est.");
    }

    // ⚠️ Le rapport de détection a changé pendant votre lecture — un relevé a été déposé entre le
    // rendu de l'écran et le clic. Écrire aurait posé votre nom sur un rapport de détection que vous
    // n'avez pas lu.
    if (arbitrated.Status == ResultStatus.Conflict)
    {
      return NothingWasArbitrated(
        "Un rapport de détection plus récent a été déposé pendant que vous lisiez cette table : "
        + "votre arbitrage n'a pas été enregistré, pour qu'il ne soit pas porté sur un rapport de "
        + "détection que vous n'avez pas lu. Voici le rapport de détection courant.");
    }

    if (!arbitrated.IsSuccess)
    {
      // Les refus viennent du domaine, où la règle est écrite — l'écran les redit, il n'en rédige
      // aucun. La clé est celle du champ lié, SANS préfixe : une clé préfixée aurait désigné un
      // champ inexistant.
      foreach (var refusal in arbitrated.ValidationErrors)
      {
        ModelState.AddModelError(refusal.Identifier, refusal.ErrorMessage);
      }

      // ⚠️ La table se relit AVANT d'être rendue : le refus s'affiche au-dessus de l'état réel des
      // colonnes, et non au-dessus de celui qu'elles avaient au chargement précédent.
      return await ReadTheTableAsync(cancellationToken);
    }

    return RedirectToPage(new { Schema, Table });
  }

  /// <summary>
  /// Pose le <b>geste de lot</b> sur la table ouverte, puis <b>revient à cette table</b> en disant ce
  /// qu'il a fait.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Le formulaire ne poste aucun nom de colonne, et il ne doit jamais pouvoir en poster.</b>
  /// Il nomme une table ; ce que le lot atteint dedans est décidé par le domaine. Une liste de
  /// colonnes ici aurait ouvert le seul chemin par lequel un formulaire forgé écarte en masse des
  /// colonnes signalées — <c>une suspicion ne s'écarte jamais sans avoir été lue une par une</c>.
  /// </para>
  /// <para>
  /// ⚠️ <b>Le succès revient à la table, et il le dit.</b> Un renvoi muet aurait obligé
  /// l'<c>Operator</c> à recompter à la main ce que le geste venait de trancher, ce qui est
  /// exactement le coût qu'il achète.
  /// </para>
  /// </remarks>
  public async Task<IActionResult> OnPostBatchAsync(CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(Schema) || string.IsNullOrWhiteSpace(Table))
    {
      return NothingWasArbitrated(NothingDesignated);
    }

    // ⚠️ Un nom d'état inconnu se traite comme un formulaire forgé, comme à l'unité : les deux
    // boutons sont la seule source de ce champ.
    if (!ScreenedColumnState.TryFromName(Ruling ?? string.Empty, out var ruling))
    {
      return NothingWasArbitrated(NothingDesignated);
    }

    if (!Guid.TryParse(Screening, out var read) || read == Guid.Empty)
    {
      return NothingWasArbitrated(NothingDesignated);
    }

    var batch = await mediator.Send(
      new ArbitrateTableInBatchCommand(
        new TableIdentity(Schema, Table), ScreeningId.From(read), ruling, SignedBy),
      cancellationToken);

    if (batch.Status == ResultStatus.NotFound)
    {
      return NothingWasArbitrated(
        "La table que vous veniez de trancher n'est plus dans le rapport de détection courant : "
        + "rien n'a été enregistré. Voici le rapport de détection tel qu'il est.");
    }

    // ⚠️ Un relevé a été déposé entre le rendu de l'écran et le clic : écrire aurait posé votre nom,
    // d'un seul coup, sur des dizaines de colonnes d'un rapport de détection que vous n'avez pas lu.
    if (batch.Status == ResultStatus.Conflict)
    {
      return NothingWasArbitrated(
        "Un rapport de détection plus récent a été déposé pendant que vous lisiez cette table : "
        + "votre geste de lot n'a pas été enregistré, pour qu'il ne soit pas porté sur un rapport de "
        + "détection que vous n'avez pas lu. Voici le rapport de détection courant.");
    }

    if (!batch.IsSuccess)
    {
      foreach (var refusal in batch.ValidationErrors)
      {
        ModelState.AddModelError(refusal.Identifier, refusal.ErrorMessage);
      }

      return await ReadTheTableAsync(cancellationToken);
    }

    TempData[BatchNoticeKey] = WhatTheBatchDid(batch.Value, ruling);

    return RedirectToPage(new { Schema, Table });
  }

  /// <summary>
  /// Ce que le lot vient de faire, en toutes lettres — <b>et ce qu'il n'a pas touché</b>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>La seconde phrase est la moitié utile.</b> Un lot qui dirait seulement « 38 colonnes
  /// tranchées » laisserait l'<c>Operator</c> devant une table qu'il croit finie alors que ses
  /// suspicions, elles, n'ont pas bougé — et c'est l'épuisement même que ce geste existe pour éviter
  /// qui les lui ferait oublier.
  /// </remarks>
  private static string WhatTheBatchDid(BatchArbitration batch, ScreenedColumnState ruling)
  {
    var done = batch.Arbitrated == 0
      ? "Aucune colonne n'a été tranchée par ce geste : cette table n'avait plus de colonne où rien "
        + "n'a été vu en attente."
      : $"{batch.Arbitrated} colonne{(batch.Arbitrated == 1 ? "" : "s")} où rien n'avait été vu "
        + $"{(batch.Arbitrated == 1 ? "a été" : "ont été")} {ruling.FrenchLabel}"
        + $"{(batch.Arbitrated == 1 ? "" : "s")} sous votre nom, dans cette table et nulle part "
        + "ailleurs — chacune signée et datée pour elle-même.";

    return batch.FlaggedStillAwaiting == 0
      ? done
      : $"{done} {batch.FlaggedStillAwaiting} colonne{(batch.FlaggedStillAwaiting == 1 ? "" : "s")} "
        + $"signalée{(batch.FlaggedStillAwaiting == 1 ? "" : "s")} y "
        + $"{(batch.FlaggedStillAwaiting == 1 ? "attend" : "attendent")} toujours : aucun geste de "
        + "lot ne les atteint, elles se lisent une par une.";
  }

  /// <summary>
  /// Ce que dit un renvoi au rapport de détection quand le formulaire ne désignait rien
  /// d'arbitrable. ⚠️ <b>Une seule phrase pour toutes ces branches</b> : elles n'ont qu'une seule
  /// cause réelle — un formulaire qui n'est pas celui de cet écran — et les distinguer aurait dit à
  /// l'humain ce que son navigateur a mal fait, ce dont il ne peut rien faire.
  /// ⚠️ <b>Elle ne nomme ni colonne ni table</b> : elle sert aussi le <b>geste de lot</b>, qui
  /// désigne une table et jamais une colonne. Lui faire chercher « une colonne » dans un formulaire
  /// qui n'en porte aucune l'enverrait chercher ce qui n'existe pas.
  /// </summary>
  private const string NothingDesignated =
    "Aucun arbitrage n'a été enregistré : le formulaire envoyé ne désignait pas ce qu'il fallait "
    + "trancher. Rouvrez la table et reprenez le geste.";

  /// <summary>
  /// Le renvoi au rapport de détection, <b>et la phrase qui dit que rien n'a été écrit</b>. ⚠️ Sans elle, un
  /// renvoi se lit comme une navigation ordinaire, et l'<c>Operator</c> repart en croyant avoir
  /// tranché — ce qui est le seul mode de panne que cet écran ne doit jamais avoir.
  /// </summary>
  private IActionResult NothingWasArbitrated(string notice)
  {
    TempData[NoticeKey] = notice;

    return RedirectToPage("Report");
  }

  /// <summary>
  /// La lecture que les deux gestes partagent : un refus d'arbitrage rend le <b>même</b> écran qu'un
  /// GET, aux refus près.
  /// </summary>
  private async Task<IActionResult> ReadTheTableAsync(CancellationToken cancellationToken)
  {
    // ⚠️ Une adresse sans ses deux membres ne désigne aucune table : on renvoie au rapport de
    // détection, qui les nomme toutes. Forger une identité sur un membre vide aurait levé au fond
    // d'un domaine dont la règle est qu'un triplet mal formé est une programmation fautive, jamais
    // une saisie.
    if (string.IsNullOrWhiteSpace(Schema) || string.IsNullOrWhiteSpace(Table))
    {
      return RedirectToPage("Report");
    }

    Answer = await mediator.Send(
      new ReadScreeningTableQuery(new TableIdentity(Schema, Table)), cancellationToken);

    // Aucun rapport de détection courant, ou un courant qui ne porte pas cette table : le rapport
    // de détection la nommerait s'il l'avait. Une table vide portant la clause aurait fait passer
    // une adresse mal recopiée pour une table réellement dépourvue de colonnes.
    return Answer is null ? RedirectToPage("Report") : Page();
  }
}
