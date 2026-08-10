# Accord machine–humain

300 colonnes codées **à la main** par l'annotateur humain, en aveugle, confrontées aux étiquettes produites par sous-agents sur les 3254 colonnes du corpus.

> ⚠️ **Ce n'est pas du bruit d'annotation.** Amendement n° 2 du 2026-08-09 au § 4 : la première passe est machine. Ce tableau ne borne pas le banc — il dit si la vérité terrain est **utilisable**. Les 300 colonnes humaines font **référence** ; les étiquettes machine sont **provisoires** jusqu'à ce chiffre.
>
> ⚠️ **Seule la ligne « colonnes signalées » veut dire quelque chose.** L'accord global est gonflé par la masse des `Unflagged` et par les 436 colonnes des familles mécaniques du § 3.1, qui s'accordent par simple application de la règle.
>
> ⚠️ **Circularité à déclarer par #134.** Si le moteur retenu est un modèle de langue, la vérité terrain et le concurrent relèvent de la même technologie et le banc se mesure en partie lui-même. Si le moteur est lexical et morphologique, le problème est bien plus faible.
>
> ⚠️ **Ces 300 colonnes valident en moyenne, pas colonne par colonne.** Une famille où les agents se trompent systématiquement, et que l'échantillon touche peu, passera au travers. La matrice des désaccords est le seul endroit où ça se verra.

| Mesure | Effectif | Accord brut | Kappa de Cohen |
|---|---:|---:|---:|
| Toutes colonnes | 300 | 90.7 % | 0.793 |
| Colonnes signalées | 93 | 69.9 % | 0.631 |

⚠️ L'accord *toutes colonnes* est gonflé par la masse des `Unflagged` et par les familles mécaniques du § 3.1 ; c'est la seconde ligne qu'il faut citer.

## Désaccords — 28 colonnes

| Étiquette machine | Référence humaine | n |
|---|---|---:|
| `Unflagged` | `FinancialData` | 8 |
| `Unflagged` | `PersonalDataUncategorised` | 7 |
| `Unflagged` | `HealthData` | 4 |
| `Identity` | `Unflagged` | 2 |
| `PersonalDataUncategorised` | `ConnectionData` | 2 |
| `Unflagged` | `ProfessionalLife` | 2 |
| `PersonalDataUncategorised` | `LocationData` | 1 |
| `Unflagged` | `ContactDetails` | 1 |
| `PersonalDataUncategorised` | `HealthData` | 1 |

## Matrice des désaccords

```

machine \ humain           Health  Authen  Financ  Locati  Connec  Identi  Contac  Profes  Person  Unflag
HealthData                     28       ·       ·       ·       ·       ·       ·       ·       ·       ·
AuthenticationSecret            ·       1       ·       ·       ·       ·       ·       ·       ·       ·
FinancialData                   ·       ·       ·       ·       ·       ·       ·       ·       ·       ·
LocationData                    ·       ·       ·       ·       ·       ·       ·       ·       ·       ·
ConnectionData                  ·       ·       ·       ·       2       ·       ·       ·       ·       ·
Identity                        ·       ·       ·       ·       ·      19       ·       ·       ·       2
ContactDetails                  ·       ·       ·       ·       ·       ·      10       ·       ·       ·
ProfessionalLife                ·       ·       ·       ·       ·       ·       ·       ·       ·       ·
PersonalDataUncategorised       1       ·       ·       1       2       ·       ·       ·       5       ·
Unflagged                       4       ·       8       ·       ·       ·       1       2       7     207
```
