using FluentValidation;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Web.Casework;

/// <summary>
/// La frontière d'entrée : ce qui ne peut pas devenir un dossier est arrêté ici, <b>nommé à
/// l'appelant</b> plutôt que levé sur lui.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle refuse peu.</b> Un sac vide passe — une demande qu'on ne saura chercher nulle part est
/// un fait que l'écran doit montrer, pas une requête malformée. Une liste de droits vide passe
/// aussi : la reconnaissance des droits viendra dans le dossier, pendant que le compteur tourne.
/// </para>
/// <para>
/// Ce qu'elle refuse est ce que le service ne saurait pas <b>écrire</b> : un mot que le vocabulaire
/// fermé ignore, une valeur vide ou démesurée. Ces règles redisent ce que portent déjà les types du
/// domaine, et la redite est voulue — le type protège le domaine en levant, la frontière parle à
/// l'appelant en lui nommant ce qu'il a mal rempli.
/// </para>
/// </remarks>
public sealed class OpenCaseRequestValidator : Validator<OpenCaseRequest>
{
  public OpenCaseRequestValidator()
  {
    RuleForEach(request => request.Designations)
      .ChildRules(designation =>
      {
        designation.RuleFor(one => one.Kind)
          .Must(kind => DesignationKind.FromToken(kind) is not null)
          .WithMessage(
            "La nature d'une désignation est l'une de : "
            + string.Join(", ", DesignationKind.List.Select(kind => kind.Token)) + ".");

        designation.RuleFor(one => one.Value)
          .Must(value => !string.IsNullOrWhiteSpace(value))
          .WithMessage("La valeur d'une désignation est absente ou vide.");

        // Le plafond se mesure après nettoyage des bordures, sur la valeur qui sera effectivement
        // stockée et qui partira sur le fil vers un Adapter.
        designation.RuleFor(one => one.Value)
          .Must(value => value!.Trim().Length <= Designation.MaxValueLength)
          .WithMessage($"La valeur d'une désignation dépasse {Designation.MaxValueLength} caractères.")
          .Must(value => !value!.Trim().Any(char.IsControl))
          .WithMessage("La valeur d'une désignation contient un caractère de contrôle.")
          .When(one => !string.IsNullOrWhiteSpace(one.Value));
      })
      .When(request => request.Designations is not null);

    RuleForEach(request => request.Rights)
      .Must(right => DataSubjectRight.TryFromName(right, out _))
      .WithMessage(
        "Ce droit n'appartient pas à la taxonomie : "
        + string.Join(", ", DataSubjectRight.List.Select(right => right.Name)) + ".")
      .When(request => request.Rights is not null);
  }
}
