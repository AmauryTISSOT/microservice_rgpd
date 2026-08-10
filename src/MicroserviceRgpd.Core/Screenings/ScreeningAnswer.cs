namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Ce que rend toute lecture portant un <see cref="Screening"/> ou une <see cref="ScreenedColumn"/> :
/// le contenu demandé, <b>et la <see cref="IncompletenessClause"/></b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>La clause est structurelle : le type est inconstruisible sans elle.</b> Un seul constructeur,
/// qui l'exige ; aucun constructeur sans paramètre ; scellé, donc aucun héritier n'en ouvre un
/// autre ; et une classe plutôt qu'un <c>record</c>, pour qu'aucun <c>with</c> ne puisse en détacher
/// la clause après coup. C'est le seul mécanisme qui rende vérifiable la décision selon laquelle
/// l'incomplétude est <b>visible par construction</b> — sans lui, elle redevient une intention, et
/// l'argument qui a fait préférer une clause structurée à un paragraphe de prose tombe
/// rétroactivement.
/// </para>
/// <para>
/// ⚠️ <b>Ce n'est pas une réponse HTTP.</b> <c>Screening</c> n'a aucune route : son contrat est un jeu
/// de commandes et de requêtes, et l'<c>Operator</c> déclenche depuis un écran que nous écrivons. La
/// cible de l'exigence est donc le <b>type rendu</b>, et non un contrat de fil.
/// </para>
/// <para>
/// ⚠️ <b>Et ce n'est pas une mention en pied de page.</b> C'est une propriété de la réponse, au même
/// rang que le contenu : un appelant qui lit <see cref="Content"/> a la clause dans la main, qu'il la
/// regarde ou non.
/// </para>
/// </remarks>
/// <typeparam name="T">Ce que la lecture demandait.</typeparam>
public sealed class ScreeningAnswer<T>
{
  /// <summary>
  /// Assemble une réponse. <b>C'est le seul constructeur du type</b>, et il n'existe aucun chemin qui
  /// produise une réponse sans clause.
  /// </summary>
  /// <param name="content">Ce que la lecture demandait.</param>
  /// <param name="clause">Ce que le rapport dit de ce qu'il n'a pas regardé.</param>
  /// <exception cref="ArgumentNullException"><paramref name="content"/> ou <paramref name="clause"/> est absent.</exception>
  public ScreeningAnswer(T content, IncompletenessClause clause)
  {
    ArgumentNullException.ThrowIfNull(content);
    ArgumentNullException.ThrowIfNull(clause);

    Content = content;
    Clause = clause;
  }

  /// <summary>Ce que la lecture demandait.</summary>
  public T Content { get; }

  /// <summary>Ce que le rapport dit de ce qu'il n'a pas regardé. Jamais absent.</summary>
  public IncompletenessClause Clause { get; }
}
