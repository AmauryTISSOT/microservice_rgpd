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
| Toutes colonnes | 300 | 73.7 % | 0.298 |
| Colonnes signalées | 97 | 18.6 % | 0.040 |

⚠️ L'accord *toutes colonnes* est gonflé par la masse des `Unflagged` et par les familles mécaniques du § 3.1 ; c'est la seconde ligne qu'il faut citer.

## Désaccords — 79 colonnes

| Étiquette machine | Référence humaine | n |
|---|---|---:|
| `HealthData` | `Unflagged` | 18 |
| `Identity` | `Unflagged` | 14 |
| `PersonalDataUncategorised` | `Unflagged` | 8 |
| `Unflagged` | `PersonalDataUncategorised` | 6 |
| `Identity` | `ConnectionData` | 5 |
| `ContactDetails` | `Unflagged` | 5 |
| `Unflagged` | `FinancialData` | 3 |
| `Unflagged` | `ContactDetails` | 3 |
| `FinancialData` | `Unflagged` | 2 |
| `ContactDetails` | `LocationData` | 2 |
| `Unflagged` | `HealthData` | 2 |
| `Identity` | `PersonalDataUncategorised` | 1 |
| `PersonalDataUncategorised` | `FinancialData` | 1 |
| `ProfessionalLife` | `Unflagged` | 1 |
| `Unflagged` | `LocationData` | 1 |
| `ContactDetails` | `PersonalDataUncategorised` | 1 |
| `Unflagged` | `ConnectionData` | 1 |
| `ProfessionalLife` | `ConnectionData` | 1 |
| `AuthenticationSecret` | `Unflagged` | 1 |
| `ConnectionData` | `Unflagged` | 1 |
| `Unflagged` | `ProfessionalLife` | 1 |
| `Identity` | `LocationData` | 1 |

## Matrice des désaccords

```

machine \ humain           Health  Authen  Financ  Locati  Connec  Identi  Contac  Profes  Person  Unflag
HealthData                      9       ·       ·       ·       ·       ·       ·       ·       ·      18
AuthenticationSecret            ·       ·       ·       ·       ·       ·       ·       ·       ·       1
FinancialData                   ·       ·       1       ·       ·       ·       ·       ·       ·       2
LocationData                    ·       ·       ·       ·       ·       ·       ·       ·       ·       ·
ConnectionData                  ·       ·       ·       ·       ·       ·       ·       ·       ·       1
Identity                        ·       ·       ·       1       5       4       ·       ·       1      14
ContactDetails                  ·       ·       ·       2       ·       ·       3       ·       1       5
ProfessionalLife                ·       ·       ·       ·       1       ·       ·       ·       ·       1
PersonalDataUncategorised       ·       ·       1       ·       ·       ·       ·       ·       1       8
Unflagged                       2       ·       3       1       1       ·       3       1       6     203
```
