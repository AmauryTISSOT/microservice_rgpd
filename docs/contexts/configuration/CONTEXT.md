# Configuration

Ce contexte ne connaît que **ce qui vaut pour toutes les demandes à la fois** : le réglage du
service, sans date et sans dossier. Il tient le `Settings` — à l'écran, le **Paramétrage** — qui
associe à chacun des six droits RGPD l'adresse à laquelle le service l'exercera. **Un droit, une
adresse.**

Il **configure, il n'appelle pas.** Enregistrer une adresse est une écriture locale : aucune
requête ne part, ni à la saisie, ni plus tard. Le déclenchement de l'appel et le recâblage de
l'instruction d'un `Case` sur ces adresses sont des décisions à venir, et l'ADR-0016 les nomme
comme telles.

Il est le **troisième consommateur du noyau partagé**, aux côtés de
[Qualification](../qualification/CONTEXT.md) et de [Casework](../casework/CONTEXT.md) :
`DataSubjectRight` est la seule chose qu'il partage. Il ne touche à rien d'autre, et rien d'autre ne
le lit encore. [Screening](../screening/CONTEXT.md) ne communique avec lui en aucune façon. Voir
[`CONTEXT-MAP.md`](../../../CONTEXT-MAP.md).

Les identifiants du code sont en anglais (`Configuration`, `Settings`, `EndpointUrl`) ; les textes
destinés à l'humain sont en français (« Paramétrage », « non configuré »).

## Language

### Le Paramétrage et ce qu'il porte

**Settings** :
La configuration applicative du service, **unique et propriété du service** : une seule instance,
une seule ligne en base (table `settings`, clé fixe). Elle associe chacun des six droits du
périmètre à un `EndpointUrl` **ou à rien**. L'écran la nomme « Paramétrage ».
Elle **naît paresseusement** : un service vierge n'a rien de persisté, et c'est un état complet —
les six droits s'y lisent « non configuré » sans que personne ait eu à « créer » la configuration.
La règle « il y a six droits » vit dans le code, jamais dans la base.
_Avoid_ : Manifest, Configuration, Catalogue, Registre, Profile, Preferences, Options

`Configuration` est le nom du contexte, et il nomme aussi, au registre du développeur,
`appsettings.json` et les options de déploiement. Le donner à l'agrégat aurait fait lire les deux
comme une seule chose. `Manifest` nommait le modèle que celui-ci remplace — un catalogue de
systèmes — et le reprendre ferait croire que le modèle a survécu sous un autre grain.

**EndpointUrl** :
L'adresse à laquelle le service exercera un droit. Impossible à construire invalide : **absolue**,
en `http` ou `https`, au plus 2 048 caractères, sans **userinfo**. Une adresse relative n'a pas
d'hôte ; une adresse qui porte `user:pw@` porterait un secret, que ce contexte ne détient pas. Le
`http` est admis au même titre que le `https` : exiger le chiffrement relève du déploiement.
**La construire n'appelle rien** — ni connexion, ni résolution de nom. Une adresse bien formée peut
désigner un hôte injoignable, et ce n'est pas à la saisie que cela se découvre.
_Avoid_ : AdapterAddress, Webhook, Callback, lien, route

`AdapterAddress` est un homonyme de `Casework`, avec ses propres règles, attaché à un
`DeclaredSystem`. Les deux ne se convertissent pas l'un dans l'autre.

**RightEndpoint** :
Un droit et son adresse, ou son absence : ce que l'écran relit, droit par droit. Il ne recopie ni
le libellé ni l'article, qui se lisent sur le `DataSubjectRight`.
_Avoid_ : Binding, Mapping, Entry, ligne de configuration

### Les droits, et celui qui n'en est pas un

**Les six droits configurables** :
`DataSubjectRight.List` moins `OutOfScope`, rangés dans l'ordre des articles : accès (15),
rectification (16), effacement (17), limitation (18), portabilité (20), opposition (21). L'article
19 est absent délibérément : il n'ouvre pas de droit que la personne exerce. Libellé français et
article viennent du type partagé (`FrenchLabel`, `Article`), jamais d'une seconde table.

`OutOfScope` est le **verdict** qu'aucun droit n'est exercé, pas un droit : il n'a pas d'adresse, et
le demander au `Settings` est une programmation fautive, refusée comme telle.
_Avoid_ : sept droits, tous les droits, les droits RGPD (sans nombre)

**Non configuré** :
L'état d'un droit sans `EndpointUrl`. C'est un état **valide et normal** — celui d'un service qu'on
vient d'installer —, jamais un manque à combler. Il se dit en toutes lettres à l'écran. Effacer
l'adresse d'un droit le ramène à cet état, et n'a d'effet sur aucun autre.
_Avoid_ : manquant, incomplet, à compléter, vide, désactivé

### Ce que le contexte ne fait pas

**Il énumère, il ne compte pas.** L'écran liste les six droits et leur état ; il n'affiche ni
total, ni taux, ni ratio. Un « 4/6 configurés » se lirait comme une mesure d'avancement, et un
« 6/6 » comme une configuration **complète** — or une adresse enregistrée ne dit pas qu'un appel y
aboutira.

**Il ne détient aucun secret.** Le userinfo est refusé, et l'authentification de l'appel est hors
du périmètre de ce contexte.

**Il ne date pas.** Le `Settings` ne sait pas quand une adresse a été posée ni par qui. La datation
par droit est hors périmètre, et son absence est assumée : un réglage n'est pas une preuve.
