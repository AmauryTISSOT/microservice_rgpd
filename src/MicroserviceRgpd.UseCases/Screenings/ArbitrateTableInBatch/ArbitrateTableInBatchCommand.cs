using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.ArbitrateTableInBatch;

/// <summary>
/// Un <c>Operator</c> pose le <b>geste de lot</b> sur la table qu'il a ouverte : il tranche d'un coup
/// toutes ses colonnes où rien n'a été vu et que personne n'a encore lues.
/// </summary>
/// <remarks>
/// <para>
/// <b>Il existe pour qu'un rapport de cinq mille colonnes reste tenable.</b> Un écran intenable
/// rétablit l'<c>Omission silencieuse</c> par l'épuisement, et personne n'abandonne en déclarant
/// qu'il abandonne.
/// </para>
/// <para>
/// ⚠️ <b>Il ne désigne aucune colonne, et il ne doit jamais pouvoir en désigner.</b> Il nomme une
/// table ; ce qu'il atteint dedans est décidé par le domaine, jamais par ce qui est posté. Une liste
/// de colonnes dans ce message aurait ouvert le seul chemin par lequel un formulaire forgé écarte en
/// masse des colonnes signalées — <c>une suspicion ne s'écarte jamais sans avoir été lue une par
/// une</c>.
/// </para>
/// <para>
/// ⚠️ <b>Il ne porte pas de date, et il ne doit jamais en porter</b> : l'instant est posé par le
/// service, à l'horloge injectée. Ici plus qu'ailleurs — une date choisie par l'appelant se poserait
/// d'un coup sur des dizaines de lignes.
/// </para>
/// </remarks>
/// <param name="Table">La table ouverte. C'est la borne entière du geste.</param>
/// <param name="ReadScreening">
/// Le rapport que l'humain <b>avait sous les yeux</b>. ⚠️ <b>Il ne désigne pas où écrire</b> — le
/// geste écrit toujours le courant — <b>il permet de refuser</b> quand un collègue a déposé un relevé
/// pendant la lecture. Sans lui, un geste humain atterrit d'un coup sur des dizaines de lignes d'un
/// rapport que personne n'a lu.
/// </param>
/// <param name="Ruling">L'issue portée sur chacune : retenue, ou écartée. Jamais <c>Awaiting</c>.</param>
public sealed record ArbitrateTableInBatchCommand(
  TableIdentity Table,
  ScreeningId ReadScreening,
  ScreenedColumnState Ruling) : ICommand<Result<BatchArbitration>>;
