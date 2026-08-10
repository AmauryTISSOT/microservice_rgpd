# L'ensemble des catégories de la macro F2

> La macro est moyennée sur les catégories peuplées dans au moins cinq plis sur six, ensemble fixé et publié à l'issue de #142, avant tout chiffre de moteur.

Dérivé mécaniquement de `annotation/` par `outils/macro-f2.py` (#157). Plis de #130 : `dolibarr` · `glpi` · `openemr` · `sacoche` · `paheko` · `galette` (`paheko` réunit ses trois états ; `galette-pg` est contrôle de dialecte, ses étiquettes se transfèrent par nom ; `temoin` est exclu des deux côtés). Aucun chiffre de moteur ici.

## Catégories retenues

- `AuthenticationSecret` — peuplée dans 5 plis sur 6
- `ConnectionData` — peuplée dans 6 plis sur 6
- `Identity` — peuplée dans 6 plis sur 6
- `ContactDetails` — peuplée dans 6 plis sur 6
- `PersonalDataUncategorised` — peuplée dans 6 plis sur 6

## Catégories exclues de la macro — rapportées séparément, jamais fondues dans un score

| Catégorie | Plis peuplés | `dolibarr` | `glpi` | `openemr` | `sacoche` | `paheko` | `galette` |
|---|---:|---:|---:|---:|---:|---:|---:|
| `CriminalOffenceData` | 0/6 | 0 | 0 | 0 | 0 | 0 | 0 |
| `HealthData` | 2/6 | 0 | 0 | 207 | 1 | 0 | 0 |
| `SpecialCategoryData` | 0/6 | 0 | 0 | 0 | 0 | 0 | 0 |
| `NationalIdentifier` | 2/6 | 0 | 0 | 1 | 1 | 0 | 0 |
| `FinancialData` | 4/6 | 7 | 0 | 9 | 0 | 5 | 3 |
| `LocationData` | 0/6 | 0 | 0 | 0 | 0 | 0 | 0 |
| `ProfessionalLife` | 3/6 | 3 | 0 | 2 | 0 | 0 | 2 |

- `CriminalOffenceData` : zéro attendu sur les 15 048 colonnes (#137 : propriété de la population, pas trou de corpus).
- `HealthData` : mono-schéma attendu — 509 des 521 candidats chez OpenEMR (#129).
- `SpecialCategoryData` : de l'ordre de dix instances attendues (#129).

⚠️ Le banc publie la F2 par catégorie **y compris** pour les exclues, avec ce motif d'exclusion nommé (#130).

## `Unflagged` n'est pas une catégorie de la macro

Classe négative de la F2 un-contre-tous, pas une catégorie de données (#127) : peuplée dans les six plis, elle franchirait le seuil trivialement et fondrait la classe majoritaire dans la moyenne que la macro existe pour protéger.

## Peuplement complet par pli

| Catégorie | `dolibarr` | `glpi` | `openemr` | `sacoche` | `paheko` | `galette` | Plis peuplés |
|---|---:|---:|---:|---:|---:|---:|---:|
| `CriminalOffenceData` | 0 | 0 | 0 | 0 | 0 | 0 | 0/6 |
| `HealthData` | 0 | 0 | 207 | 1 | 0 | 0 | 2/6 |
| `SpecialCategoryData` | 0 | 0 | 0 | 0 | 0 | 0 | 0/6 |
| `AuthenticationSecret` | 2 | 0 | 3 | 3 | 8 | 5 | 5/6 |
| `NationalIdentifier` | 0 | 0 | 1 | 1 | 0 | 0 | 2/6 |
| `FinancialData` | 7 | 0 | 9 | 0 | 5 | 3 | 4/6 |
| `LocationData` | 0 | 0 | 0 | 0 | 0 | 0 | 0/6 |
| `ConnectionData` | 4 | 2 | 4 | 6 | 5 | 1 | 6/6 |
| `Identity` | 59 | 14 | 56 | 60 | 31 | 23 | 6/6 |
| `ContactDetails` | 24 | 5 | 25 | 10 | 9 | 10 | 6/6 |
| `ProfessionalLife` | 3 | 0 | 2 | 0 | 0 | 2 | 3/6 |
| `PersonalDataUncategorised` | 10 | 5 | 46 | 36 | 13 | 18 | 6/6 |
| `Unflagged` | 493 | 584 | 250 | 431 | 554 | 132 | — |

Totaux par pli : `dolibarr` 602 · `glpi` 610 · `openemr` 603 · `sacoche` 548 · `paheko` 625 · `galette` 194.
