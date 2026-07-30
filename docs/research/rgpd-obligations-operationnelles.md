# Ce que le RGPD exige opérationnellement de qui honore un droit

> Recherche du ticket [#58](https://github.com/AmauryTISSOT/microservice_rgpd/issues/58).
> **Ce document donne la matière d'un choix ; il ne le fait pas.** L'arbitrage « prendre en charge ou accompagner » n'est pas tranché ici. Ce qui est tranché, c'est la frontière entre ce qu'un service **doit garantir ou refuser de promettre** et ce qui reste **l'organisation interne du responsable de traitement**.

## Ce que la question demande

Le ticket ne porte pas sur les six droits mais sur **le traitement de la demande elle-même** : le délai, la gratuité, l'identité, le refus motivé, la répercussion aux destinataires, la forme de la réponse, la preuve d'avoir honoré. Ces obligations sont **transversales** — elles pèsent identiquement que la demande relève de l'accès, de l'effacement ou de l'opposition. C'est précisément ce qui les rend pertinentes pour un service qui ne connaît pas encore le droit exercé au moment où il reçoit le texte.

## Convention de lecture

Chaque affirmation porte sa source. Trois niveaux, hiérarchisés :

| Niveau | Nature | Poids |
| --- | --- | --- |
| **Texte** | RGPD, version française du *Journal officiel* L 119/1 du 4.5.2016 | Contraignant |
| **CJUE** | Arrêts de la Cour | Contraignant, interprète le texte |
| **CEPD / CNIL** | Lignes directrices, rapports d'action coordonnée, doctrine | Non contraignant, mais c'est la grille de lecture des autorités de contrôle |

Le texte du RGPD est cité d'après la version française officielle obtenue du service *cellar* de l'Office des publications ([`celex/32016R0679`](http://publications.europa.eu/resource/celex/32016R0679), ELI [`reg/2016/679/oj/fra`](https://eur-lex.europa.eu/eli/reg/2016/679/oj/fra)) ; la CNIL en publie la même version article par article ([chapitre III](https://www.cnil.fr/fr/reglement-europeen-protection-donnees/chapitre3)).

⚠️ **« Obligation de résultat » est ici une catégorie du projet, pas du RGPD.** Le règlement ne classe pas ses obligations ainsi. La colonne signifie : *le service devra le garantir contractuellement, ou refuser explicitement de le promettre*.

---

# Partie 0 — L'état des sources, avant tout le reste

Trois constats de méthode qui conditionnent la confiance à accorder à la suite.

**1. Une seule ligne directrice du CEPD couvre en profondeur ces obligations transversales, et elle est écrite pour l'accès.** Les [lignes directrices 01/2022 sur le droit d'accès](https://www.edpb.europa.eu/system/files/2023-04/edpb_guidelines_202201_data_subject_rights_access_v2_en.pdf) (version 2.1, adoptées le 28 mars 2023, 63 pages) traitent longuement du délai (§ 157-164), de l'authentification (§ 58-79), de l'art. 12.5 (§ 175-195) et de la forme (§ 148-156). **Mais leur objet formel est l'art. 15.** Les développements sur les art. 12.3, 12.4, 12.5 et 12.6 sont transversaux par nature — ces articles s'appliquent aux art. 15 à 22 sans distinction — et le CEPD les traite comme tels ; leur transposition aux cinq autres droits est raisonnable mais **n'est pas explicitement validée** par le CEPD.

**2. Il n'existe aucune ligne directrice du CEPD sur l'article 19.** Recherche faite : ni ligne directrice dédiée, ni section dédiée dans les documents examinés. L'action coordonnée 2025 sur l'effacement a pourtant **interrogé** les responsables sur « *the steps taken to inform other controllers and data recipients about the erasure request* » ([rapport CEF 2025](https://www.edpb.europa.eu/system/files/documents/2026-02/edpb_cef-report_2025_right-to-erasure_en.pdf), § 2.2) — et **n'a publié aucun constat sur ce point** : aucune des sept difficultés retenues ne porte sur l'art. 19. L'obligation la plus lourde pour l'architecture est celle sur laquelle la doctrine est la plus mince.

**3. Les deux rapports d'action coordonnée sont la source la plus utile sur « ce qu'il faut conserver ».** [CEF 2024 sur le droit d'accès](https://www.edpb.europa.eu/system/files/2025-01/edpb_cef-report-2024_20250116_rightofaccess_en.pdf) (adopté le 16 janvier 2025, 1 185 responsables interrogés, 30 autorités) et [CEF 2025 sur le droit à l'effacement](https://www.edpb.europa.eu/system/files/documents/2026-02/edpb_cef-report_2025_right-to-erasure_en.pdf) (adopté le 18 février 2026, 32 autorités). Ce sont des constats de terrain, pas de la doctrine — et c'est ce qui leur donne leur valeur ici.

---

# Partie 1 — Le délai (art. 12.3)

## 1.1 Le texte

> « Le responsable du traitement fournit à la personne concernée des informations sur les mesures prises à la suite d'une demande formulée en application des articles 15 à 22, **dans les meilleurs délais et en tout état de cause dans un délai d'un mois à compter de la réception de la demande**. Au besoin, ce délai peut être prolongé de deux mois, compte tenu de la complexité et du nombre de demandes. Le responsable du traitement informe la personne concernée de cette prolongation et des motifs du report dans un délai d'un mois à compter de la réception de la demande. »
> — art. 12.3, phrases 1 à 3

Quatre choses s'y lisent, dont deux sont régulièrement manquées :

- L'obligation n'est pas de **satisfaire** la demande en un mois, mais de **fournir des informations sur les mesures prises**. La nuance est faible en pratique, forte en droit : elle rend concevable une réponse qui expose un traitement en cours.
- « Dans les meilleurs délais » **et** « en tout état de cause dans un délai d'un mois » sont deux obligations cumulatives. Le CEPD est explicite : « *if it is possible to provide the requested information in a shorter amount of time than one month, the controller should do so* » (LD 01/2022, § 158). **Un mois n'est pas un délai d'exécution, c'est un plafond.**
- La prolongation est de **deux mois supplémentaires** (soit trois au total), et non de « deux mois ». La CNIL formule le total : « *1 mois maximum pour une demande simple* », « *3 mois maximum pour une demande complexe* » ([CNIL, *Professionnels : comment répondre à une demande de droit d'accès ?*](https://www.cnil.fr/fr/repondre-une-demande-de-droit-dacces)).
- **La prolongation doit être notifiée dans le premier mois.** Passé un mois de silence, la prolongation n'est plus disponible. C'est une échéance dure, distincte de celle de la réponse.

## 1.2 Le point de départ

**Le compteur part à la réception par le responsable, pas à la prise de connaissance.**

> « *The time limit starts when the controller has received an Art. 15 request, meaning when the request reaches the controller through one of its official channels. It is not necessary that the controller is in fact aware of the request.* »
> — LD 01/2022, § 159

Et « canal officiel » ne veut pas dire « canal désigné ». Le CEPD est net (§ 52-56) : le RGPD n'impose aucune forme à la demande, aucun canal ; si la personne écrit à l'adresse générale plutôt qu'à l'adresse dédiée, la demande est valable et — passage décisif —

> « *the controller is not entitled to extend the period for responding to a request, merely because the data subject has sent a request to the controller's general e-mail address, not the controller's data protection contact point e-mail address.* »
> — LD 01/2022, exemple 8

Limite : le responsable n'est **pas** tenu d'agir sur une demande envoyée à une adresse aléatoire, manifestement incorrecte, ou à un canal qui n'est visiblement pas destiné à cela — l'exemple donné est l'adresse de l'équipe de nettoyage d'une salle de sport (§ 54, 55, exemple 9). Mais si un interlocuteur habituel est saisi (gestionnaire de compte, conseiller attitré), la demande compte et doit être réacheminée dans les délais (§ 55).

**Conséquence pour un service placé en aval : le compteur tourne déjà quand il reçoit le texte.** Le service ne peut pas dater la réception ; il peut seulement enregistrer une date qu'on lui donne.

## 1.3 Le calcul

Le délai se calcule selon le [règlement (CEE, Euratom) n° 1182/71](https://eur-lex.europa.eu/eli/reg/1971/1182/oj) (LD 01/2022, § 160). Trois règles à retenir :

- Demande reçue le 5 mars → échéance le **5 avril inclus** (exemple 32).
- Demande reçue le 31 août, le mois suivant n'ayant pas de 31 → échéance le **30 septembre** (exemple 33).
- Si l'échéance tombe un week-end ou un jour férié, elle glisse au **jour ouvrable suivant** (§ 161).

Le CEPD signale que certains droits nationaux définissent eux-mêmes le moment où un message est réputé reçu (§ 159, note 90) — donc le point de départ peut varier d'un État membre à l'autre.

## 1.4 La suspension pendant la vérification d'identité — et son incertitude

C'est le point le plus important pour l'architecture, et **le moins solidement établi**.

> « *when the controller needs to communicate with the data subject due to the uncertainty regarding the identity of the person making the request **there may be a suspension in time** until the controller has obtained the information needed from the data subject, **provided the controller has asked for additional information without undue delay**.* »
> — LD 01/2022, § 159

⚠️ **Cette suspension n'a aucune base textuelle dans l'art. 12.3.** Le texte ne connaît qu'un point de départ, la réception. Le CEPD écrit *there may be*, pas *there is* — c'est une tolérance, formulée au conditionnel, assortie d'une condition (avoir demandé sans tarder). La même tolérance s'étend au cas où le responsable demande à la personne de préciser sa demande, dans les conditions du considérant 63 (§ 159 *in fine*).

**Aucune source primaire ne fixe de durée maximale à cet échange, ni ne dit ce qui se passe si la personne ne répond jamais.** L'exemple 31 des lignes directrices décrit un aller-retour de plusieurs jours et conclut à la suspension, sans plafond.

## 1.5 Ce qui justifie la prolongation, et ce qui ne la justifie pas

Le CEPD encadre étroitement (§ 162-164) :

- C'est « *an exemption from the general rule and should not be overused* ». Y recourir souvent est « *an indication of a need to further develop their general procedures* ». **Le recours répété à la prolongation est en soi un signal de non-conformité organisationnelle.**
- Facteurs pertinents de complexité : volume de données, difficulté de récupération (données réparties entre unités), nécessité de **caviarder** au titre d'une exception, nécessité d'un travail de mise en intelligibilité (§ 163).
- Ce qui **ne** suffit **pas** : « *The mere fact that complying with the request would require a large effort does not make a request complex.* » Ni le fait qu'une grande entreprise reçoive beaucoup de demandes. Un afflux **temporaire et exceptionnel** (publicité extraordinaire autour de l'activité) peut en revanche être un motif légitime (§ 164).

## 1.6 Deux points hors art. 12.3

**L'accusé de réception n'est pas obligatoire.** Le CEPD le qualifie de bonne pratique et suggère d'y indiquer la période du jour X au jour Y (§ 57) ; le CEF 2024 confirme : « *a controller is not legally obliged to send confirmation/acknowledgement of an access request however it is considered good practice* » (§ 4.2.3). Le même rapport observe que **l'absence d'accusé est corrélée aux dépassements de délai et aux plaintes**.

**Le « 8 jours pour les données de santé » n'est pas du RGPD.** La CNIL l'annonce sur sa page professionnelle (« *8 jours maximum pour des données de santé* »), mais **l'art. 12.3 ne contient rien de tel**. Ce délai relève du droit national de la santé, dont la base n'a pas été tracée dans cette recherche. À vérifier avant tout engagement produit.

---

# Partie 2 — Gratuité, frais et refus (art. 12.5)

## 2.1 Le texte

> « **Aucun paiement n'est exigé** pour fournir les informations au titre des articles 13 et 14 et pour procéder à toute communication et prendre toute mesure au titre des articles 15 à 22 et de l'article 34. Lorsque les demandes d'une personne concernée sont **manifestement infondées ou excessives, notamment en raison de leur caractère répétitif**, le responsable du traitement peut : a) exiger le paiement de frais raisonnables qui tiennent compte des coûts administratifs […] ; ou b) refuser de donner suite à ces demandes.
> **Il incombe au responsable du traitement de démontrer le caractère manifestement infondé ou excessif de la demande.** »
> — art. 12.5

La charge de la preuve est donc explicitement **sur le responsable**, dans le texte lui-même. Ce n'est pas une construction doctrinale.

## 2.2 Comment le CEPD lit ces deux notions

**Interprétation restrictive imposée** : « *These concepts have to be interpreted narrowly, as the principles of transparency and cost free data subjects rights must not be undermined* » (§ 175). Et l'appréciation est **au cas par cas**, jamais catégorielle (§ 176).

**« Manifestement infondée » — quasi inutilisable.** Une demande l'est si les conditions de l'art. 15 « *are clearly and obviously not met when applying an objective approach* ». Or il y a très peu de conditions à une demande d'accès. Le CEPD en tire : « *there is only very limited scope for relying on the "manifestly unfounded" alternative* » (§ 177). Cas explicitement écartés : demande portant sur un traitement hors RGPD — ce n'est alors « *pas une demande art. 15 du tout* » (§ 178) ; demande adressée à un organisme qui ne traite pas ces données — mieux vaut répondre « non, nous n'en traitons pas » (§ 179, exemple 38) ; antécédents de demandes abusives, ou langage inconvenant (§ 180).

**« Excessive » — l'intervalle raisonnable, jamais un chiffre.** Le scénario principal est quantitatif : la répétition (§ 181). Le critère est l'« intervalle raisonnable » du considérant 63 (§ 183). Quatre facteurs (§ 185) : fréquence de modification des données, nature des données, finalités du traitement et préjudice potentiel, identité de périmètre entre les demandes.

Le CEPD refuse tout seuil : « *It is not possible to generally determine any specific interval* ». Il pose cependant une borne unilatérale : **un intervalle d'un an ne peut en aucun cas rendre une demande excessive** (« *a one-year interval […] will in any case be too large for the request to be considered excessive* », § 185 *in fine*). Illustrations contrastées : demandes tous les deux mois à un menuisier → potentiellement excessives (exemple 39) ; demandes trimestrielles à un réseau social → en principe pas excessives (exemple 40).

**Ce qui ne rend jamais une demande excessive** (§ 188-189) : l'effort considérable qu'elle impose (« *cannot on its own render a request excessive* », renvoyant au § 166 : le droit d'accès est « *without any general reservation to proportionality* ») ; l'absence de motifs donnés ; le langage impoli ; l'intention d'utiliser les données pour agir en justice contre le responsable.

**Ce qui peut la rendre excessive** (§ 190) : la demande retirée contre un avantage (chantage) ; l'intention avérée de harcèlement ou de nuisance, établie par une déclaration explicite ou par un envoi systématique de demandes fractionnées artificiellement, en campagne.

**Le CEPD nomme aussi une seconde branche, non répétitive** : « *cases of abusively relying on Art. 15 GDPR, which means cases in which data subjects make an excessive use of the right of access with the only intent of causing damage or harm to the controller* » (§ 188).

Le CEPD note aussi (§ 186) que si l'information peut être fournie facilement par voie électronique ou par accès distant à un système sécurisé, **il est peu vraisemblable qu'une demande ultérieure puisse être jugée excessive**. Autrement dit : plus l'outillage est bon, moins le refus est disponible. C'est un effet contre-intuitif dont un service d'automatisation doit avoir conscience.

## 2.3 Frais ou refus : le responsable n'a pas le choix libre

> « *controllers are – on the one hand – not generally obliged to charge a reasonable fee before refusing to act on a request. On the other hand, they aren't completely free to choose between the two alternatives either.* »
> — LD 01/2022, § 192

Orientation : facturer est « *hardly imaginable* » face à une demande manifestement infondée ; face à une demande excessive, facturer sera **souvent plus approprié** que refuser (§ 192). Et **avant** de facturer, le responsable doit prévenir et permettre le retrait de la demande (§ 194 ; même exigence pour les copies supplémentaires, § 31, avec indication du montant aussi précise que possible).

## 2.4 À ne pas confondre : les frais de l'art. 15.3

> « Le responsable du traitement peut exiger le paiement de **frais raisonnables basés sur les coûts administratifs pour toute copie supplémentaire** demandée par la personne concernée. »
> — art. 15.3, phrase 2

Ce régime est **distinct** de l'art. 12.5 : il n'exige aucun caractère abusif. Mais la frontière est fine, et le CEPD la trace (§ 28) : une demande ultérieure est une **copie supplémentaire** payante seulement si, *en périmètre et en période*, elle porte sur le même traitement que la précédente. Si elle vise un autre moment ou un autre ensemble, **le droit à une copie gratuite se rouvre**, même une semaine après (exemple 2, variantes 1 et 2). Et la relance d'une demande restée sans réponse ou refusée sans motif n'est pas une nouvelle demande : c'est un rappel (§ 29).

Les frais doivent porter sur les coûts **spécifiques** engendrés par la copie supplémentaire, jamais sur des frais généraux ou de structure (§ 30).

---

# Partie 3 — Vérification d'identité (art. 12.6)

## 3.1 Le texte : une faculté conditionnelle, pas une étape obligatoire

> « Sans préjudice de l'article 11, **lorsque le responsable du traitement a des doutes raisonnables** quant à l'identité de la personne physique présentant la demande visée aux articles 15 à 21, **il peut demander** que lui soient fournies des informations supplémentaires nécessaires pour confirmer l'identité de la personne concernée. »
> — art. 12.6

Deux verrous : un **déclencheur** (« doutes raisonnables ») et une **faculté** (« peut »). L'art. 12.6 n'institue pas une étape de vérification systématique. Le considérant 64 pose le versant positif — « *Le responsable du traitement devrait prendre toutes les mesures raisonnables pour vérifier l'identité d'une personne concernée qui demande l'accès à des données, en particulier dans le cadre des services et identifiants en ligne* » — mais assortit immédiatement : « *Un responsable du traitement ne devrait pas conserver des données à caractère personnel à la seule fin d'être en mesure de réagir à d'éventuelles demandes.* »

L'art. 11 pose le cas symétrique : si les finalités n'imposent pas d'identifier, le responsable « *n'est pas tenu de conserver, d'obtenir ou de traiter des informations supplémentaires pour identifier la personne concernée à la seule fin de respecter le présent règlement* » (art. 11.1) ; s'il démontre ne pas pouvoir identifier, « *les articles 15 à 20 ne sont pas applicables* » (art. 11.2), et l'art. 12.2 lui interdit de refuser **sauf** cette démonstration.

## 3.2 Identification ≠ authentification

Le CEPD sépare nettement (§ 58) : **identification** = retrouver quelles données se rapportent à la personne ; **authentification** = confirmer que le demandeur est bien cette personne. Deux problèmes distincts, deux remèdes distincts. Un service qui ne détient pas les données ne peut faire ni l'un ni l'autre.

## 3.3 Le principe de minimisation appliqué à la vérification elle-même

C'est le cœur du point 3 du ticket, et le CEPD y consacre une section entière (§ 70-79).

> « *as a rule, the controller cannot request more personal data than is necessary to enable this authentication, and […] the use of such information should be strictly limited to fulfilling the data subjects' request.* »
> — LD 01/2022, § 65

Le responsable doit conduire une **évaluation de proportionnalité** tenant compte du type de données (catégories particulières ou non), de la nature de la demande, du contexte, et du dommage qui résulterait d'une divulgation à la mauvaise personne (§ 70). Une méthode d'authentification lourde doit être **justifiée** au regard de la minimisation et de l'obligation de faciliter de l'art. 12.2 (§ 71).

## 3.4 La pièce d'identité : la position est franchement défavorable

Quatre affirmations en cascade, à ne pas diluer :

1. **Si la personne est déjà authentifiée, exiger une pièce d'identité est disproportionné.** « *it is disproportionate to require a copy of an identity document in the event where the data subject making a request is already authenticated by the controller* » (§ 73).
2. **En général, la copie de pièce d'identité est inappropriée.** « *using a copy of an identity document as a part of the authentication process creates a risk for the security of personal data and may lead to unauthorised or unlawful processing, and, as such, it should be considered inappropriate, unless it is necessary, suitable, and in line with national law* » (§ 74). Et : « *it should generally not be considered an appropriate way of authentication* » (§ 75).
3. **Ce qu'il faut faire à la place** : réutiliser l'authentification existante — connexion au compte, questions de sécurité non intrusives, authentification multifacteur configurée à l'inscription, lien ou code envoyé à l'adresse ou au numéro déjà connus (§ 72, 73, 75, exemples 12 et 13). Le considérant 57 et l'art. 12.1 confirment la piste des identifiants de connexion.
4. **Si une pièce est malgré tout justifiée** (catégories particulières, traitement à risque — § 78), alors : caviardage des mentions inutiles par la personne **et information de sa part qu'elle peut le faire**, caviardage par le responsable à réception si elle ne sait pas faire (§ 76-77) ; suffisent en général la date d'émission ou d'expiration, l'autorité émettrice et le nom complet correspondant au compte ; **et surtout** :

> « *this may include refraining from making a copy or deleting a copy of an ID immediately after the successful authentication […] further storage of a copy of an ID is likely to amount to an infringement of the principles of purpose limitation and storage limitation (Art. 5(1)(b) and (e) GDPR) […] The EDPB recommends, as good practice, that the controller, after checking the ID card, makes a note e.g. "ID card was checked" to avoid unnecessary copying or storage of copies of ID cards.* »
> — LD 01/2022, § 79

**Retenir cette formule : la trace attendue est « pièce vérifiée », pas la pièce.** C'est directement transposable à la conception d'une trace d'audit.

La certification notariée est écartée sauf nécessité et base légale nationale : elle expose à des coûts et constitue « *an excessive burden […] hampering the exercise of their right of access* » (exemple 14).

## 3.5 La CNIL dit la même chose, plus brièvement

À la question « dois-je fournir obligatoirement une copie de ma pièce d'identité ? », la CNIL répond « **En principe, non !** » ([CNIL, CNIL Direct](https://www.cnil.fr/fr/cnil-direct/question/exercice-de-mes-droits-informatique-et-libertes-dois-je-fournir-obligatoirement)). Il suffit de « justifier de son identité » : numéro de client ou d'abonné, ou exercice depuis un espace où l'on s'est authentifié. Sur sa page professionnelle, elle formule la condition : « *Si vous avez un "doute raisonnable" sur l'identité du demandeur, vous pouvez lui demander de joindre tout autre document permettant de prouver son identité* », et rappelle que « *dans un environnement numérique, le fait d'exercer ses droits depuis un espace où la personne s'est authentifiée peut être suffisant* » ([CNIL, *Comment répondre à une demande de droit d'accès ?*](https://www.cnil.fr/fr/repondre-une-demande-de-droit-dacces)).

⚠️ **Aucune des deux pages CNIL consultées ne traite du sort de la pièce une fois fournie.** Sur ce point, la source utilisable est le § 79 du CEPD.

## 3.6 Demandes par mandataire

Le CEPD (§ 80-81) : un tiers peut agir pour le compte de la personne ; la vérification de l'identité **du mandataire** et de son **habilitation** peut être exigée, si c'est adapté et proportionné. Le RGPD ne régit pas la représentation — le droit national s'applique. Mais au titre de la responsabilité, « *controllers shall be able to demonstrate the existence of the relevant authorisation* ». Et : communiquer des données à qui n'y a pas droit **peut constituer une violation de données**.

---

# Partie 4 — Motifs de refus et obligation de motiver (art. 12.4)

## 4.1 L'obligation de motiver, dans le texte

> « Si le responsable du traitement ne donne pas suite à la demande formulée par la personne concernée, il informe celle-ci **sans tarder et au plus tard dans un délai d'un mois** à compter de la réception de la demande **des motifs de son inaction** et de **la possibilité d'introduire une réclamation auprès d'une autorité de contrôle** et de **former un recours juridictionnel**. »
> — art. 12.4

Trois mentions obligatoires, une échéance. Le CEPD précise deux choses que le texte laisse implicites :

- **Elle s'applique au refus partiel.** « *if controllers refuse to act on an access request in whole or partly, they must inform the data subject without delay and at the latest within one month* » (§ 193). Un caviardage étendu est un refus partiel.
- **Elle est distincte de l'information sur la prolongation.** « *This obligation to inform […] about the extension and its reasons should not be confused with the information that has to be given […] when the controller does not take action on the request* » (§ 157). Deux courriers différents, deux régimes.

Le considérant 59 confirme la double obligation : répondre « *dans les meilleurs délais et au plus tard dans un délai d'un mois* » et « *motiver sa réponse lorsqu'il a l'intention de ne pas donner suite* ».

Les lignes directrices sur la portabilité, endossées par le CEPD, ajoutent la formule la plus opérationnelle qui soit : « *the data controller cannot remain silent* » ([WP242 rev.01](https://ec.europa.eu/newsroom/article29/items/611233), p. 15). **Le silence n'est jamais une option de sortie.**

Sanction : un refus injustifié est une violation des droits des art. 12 à 22, passible des amendes de l'art. 83.5.b), et ouvre la réclamation de l'art. 77 (§ 195).

## 4.2 Les motifs propres à chaque droit

Le tableau ci-dessous ne traite pas les conditions d'ouverture (qui relèvent du droit lui-même) mais **ce sur quoi un refus peut s'appuyer**, avec le porteur de la charge de la preuve. Tout est textuel sauf mention.

| Droit | Motifs de refus / limites | Charge de la preuve |
| --- | --- | --- |
| **Accès** (15) | 15.4 : la copie « *ne porte pas atteinte aux droits et libertés d'autrui* » (secret des affaires, propriété intellectuelle — cons. 63, qui ajoute que ces considérations « *ne devraient pas aboutir à refuser toute communication* ») ; 12.5 ; art. 23 (droit de l'Union ou national). **Hors de là, aucune autre dérogation** : LD 01/2022 § 166. | Responsable |
| **Rectification** (16) | **Aucun motif de refus dans le texte de l'art. 16.** Seuls 12.5 et 23 restent disponibles. | Responsable |
| **Effacement** (17) | 17.3 : liberté d'expression et d'information ; obligation légale ou mission d'intérêt public ; santé publique ; archivage / recherche / statistiques (art. 89.1) ; constatation, exercice ou défense de droits en justice. Plus 12.5 et 23. | Responsable |
| **Limitation** (18) | **Aucune exception dans l'art. 18.** Le débat porte sur les quatre cas d'ouverture du 18.1, pas sur un motif de refus. Plus 12.5 et 23. | Responsable |
| **Portabilité** (20) | 20.1 : conditions **cumulatives** — consentement (6.1.a ou 9.2.a) *ou* contrat (6.1.b), **et** procédés automatisés. 20.3 : exclu pour les missions d'intérêt public ou d'autorité publique. 20.4 : droits et libertés de tiers. 20.2 : transmission directe seulement « *lorsque cela est techniquement possible* ». | Responsable |
| **Opposition** (21.1) | Le responsable ne cesse pas s'il « *démontre qu'il existe des motifs légitimes et impérieux […] qui prévalent sur les intérêts et les droits et libertés de la personne concernée* », ou pour des droits en justice. Le mot « démontre » place explicitement la charge sur lui. | Responsable, **explicitement** |
| **Opposition à la prospection** (21.2-21.3) | **Aucun motif de refus.** « *Lorsque la personne concernée s'oppose au traitement à des fins de prospection, les données à caractère personnel ne sont plus traitées à ces fins.* » Inconditionnel. | Sans objet |

**Constat structurant : la charge de la preuve d'un refus pèse toujours sur le responsable, jamais sur le demandeur.** C'est vrai pour l'art. 12.5 (« il incombe au responsable »), pour l'art. 21.1 (« à moins qu'il ne démontre ») et pour l'art. 12.2 (« à moins que le responsable ne démontre qu'il n'est pas en mesure d'identifier »). Le règlement est construit sur cette asymétrie.

**La personne n'a pas à motiver sa demande.** « *data subjects are not obliged to give reasons or to justify their request. As long as the requirements of Art. 15 GDPR are met the purposes behind the request should be regarded as irrelevant* » (§ 167). Ni à citer le RGPD ni à invoquer un fondement juridique : le CEF 2024 le rappelle en constatant que c'est justement là que les responsables achoppent (§ 4.2.3).

## 4.3 Ce que les autorités constatent sur le terrain

Le CEF 2025 fait de la mauvaise application des exceptions sa **difficulté n° 4** : « *Misuse of and legal uncertainty on the exceptions to deny erasure requests* », avec des pratiques d'évaluation « *inconsistent* » — certains responsables évaluent formellement base légale, durée et finalité, d'autres se contentent de consultations informelles « *without documented steps* » (§ 4.2.1 et 4.2.4). La recommandation aux responsables est de faire intervenir les équipes juridiques ou conformité dans **toute décision de refus ou de report**.

⚠️ **Le CEPD n'a analysé « manifestement infondée ou excessive » que pour le droit d'accès.** Sa transposition aux cinq autres droits n'est validée par aucune source primaire consultée. Or l'argumentation du § 177 — « très peu de préalables, donc très peu de place pour "manifestement infondée" » — repose sur une caractéristique de l'art. 15 qui n'est pas partagée par l'art. 17 ou l'art. 21, plus conditionnels. Le raisonnement ne se transpose pas mécaniquement.

---

# Partie 5 — La notification aux destinataires (art. 19)

C'est l'obligation la plus lourde pour l'architecture, et celle sur laquelle la doctrine est la plus mince. Le ticket la qualifie de « souvent oubliée » ; les faits le confirment.

## 5.1 Le texte, en entier

> « Le responsable du traitement **notifie à chaque destinataire auquel les données à caractère personnel ont été communiquées** toute rectification ou tout effacement de données à caractère personnel ou toute limitation du traitement effectué conformément à l'article 16, à l'article 17, paragraphe 1, et à l'article 18, **à moins qu'une telle communication se révèle impossible ou exige des efforts disproportionnés**. Le responsable du traitement fournit à la personne concernée des informations sur ces destinataires si celle-ci en fait la demande. »
> — art. 19

Cinq éléments à extraire :

1. **« Chaque destinataire », pas « chaque tiers ».** L'art. 4, point 9, définit le destinataire comme « *la personne physique ou morale, l'autorité publique, le service ou tout autre organisme qui reçoit communication de données à caractère personnel, **qu'il s'agisse ou non d'un tiers*** ». **Les sous-traitants sont donc des destinataires.** L'art. 19 vise donc l'hébergeur, le prestataire d'emailing, le fournisseur d'analytics — pas seulement les partenaires commerciaux. C'est ce qui en fait un problème d'architecture, et non de gestion de contrats.
2. **Trois opérations couvertes, non l'accès ni la portabilité** : rectification (16), effacement (17.1) et limitation (18).
3. **Notification, pas simple mise à disposition.** Le verbe est actif et l'obligation est déclenchée par l'opération, sans demande de la personne.
4. **Deux échappatoires** : impossibilité, ou efforts disproportionnés. Elles ne sont **pas définies dans le règlement**.
5. **Une obligation seconde, à la demande** : informer la personne de l'identité de ces destinataires.

## 5.2 L'obligation voisine de l'art. 17.2, à ne pas confondre

> « Lorsqu'il a rendu publiques les données à caractère personnel et qu'il est tenu de les effacer […] le responsable du traitement, **compte tenu des technologies disponibles et des coûts de mise en œuvre**, prend des mesures raisonnables, y compris d'ordre technique, pour informer les responsables du traitement qui traitent ces données à caractère personnel que la personne concernée a demandé l'effacement […] de tout lien vers ces données […] ou de toute copie ou reproduction de celles-ci. »
> — art. 17.2, éclairé par le considérant 66

Régime distinct : il ne vise que les données **rendues publiques**, il s'adresse à d'autres **responsables** (pas à des destinataires connus), et son standard est celui des « mesures raisonnables », plus souple que celui de l'art. 19.

## 5.3 L'arrêt qui durcit le versant « information sur les destinataires »

CJUE, 12 janvier 2023, *RW c. Österreichische Post AG*, [C-154/21](https://eur-lex.europa.eu/legal-content/FR/TXT/?uri=CELEX:62021CJ0154), ECLI:EU:C:2023:3, dispositif :

> « le droit d'accès de la personne concernée aux données à caractère personnel la concernant […] implique, lorsque ces données ont été ou seront communiquées à des destinataires, **l'obligation pour le responsable du traitement de fournir à cette personne l'identité même de ces destinataires**, à moins qu'il ne soit impossible d'identifier ces destinataires ou que ledit responsable du traitement ne démontre que les demandes d'accès de la personne concernée sont manifestement infondées ou excessives, au sens de l'article 12, paragraphe 5, du règlement 2016/679, auxquels cas celui-ci peut indiquer à cette personne uniquement les catégories de destinataires en cause. »

**C'est un renversement du présupposé courant.** L'art. 15.1.c) écrit « les destinataires **ou** catégories de destinataires » ; la Cour dit que le choix appartient à la personne concernée, pas au responsable, et que la catégorie est un repli d'exception. La CNIL le reflète : on ne peut se limiter aux catégories que « *si leur identification vous est impossible* » ([CNIL](https://www.cnil.fr/fr/repondre-une-demande-de-droit-dacces)).

**Conséquence architecturale, à ne pas manquer** : tenir la liste nominative des destinataires devient nécessaire pour l'art. 15.1.c) *aussi*. La même donnée sert l'art. 19 et l'art. 15 — c'est un registre, pas un artefact de circonstance. Le CEF 2024 le formule directement : « *Controllers are responsible for accurately recording to which entities precisely they disclose personal data […] A comprehensive updated record of processing activities* » (§ 4.2, recommandations).

## 5.4 Ce que la CNIL ajoute, et ce que personne ne dit

La recommandation IA de la CNIL est la seule source primaire consultée qui donne un moyen concret : elle recommande « *le recours à des interfaces de programmation applicative (API)* », en particulier dans les cas les plus à risque, ou « *a minima des techniques de journalisation des téléchargements de données* », et l'établissement d'obligations contractuelles imposant aux réutilisateurs de répercuter les effets de l'exercice des droits ([CNIL, *IA : respecter et faciliter l'exercice des droits des personnes concernées*](https://www.cnil.fr/fr/ia-respecter-lexercice-des-droits-des-personnes)).

⚠️ **Ce qu'aucune source primaire consultée ne dit :**

- **Ce que signifie « efforts disproportionnés » à l'art. 19.** Aucune définition, aucun critère, aucun exemple dans les documents examinés. Le considérant 62 emploie la même expression à propos de l'obligation d'information des art. 13-14 et suggère de considérer « *le nombre de personnes concernées, l'ancienneté des données, ainsi que les garanties appropriées éventuelles adoptées* » — mais c'est un autre article et le considérant ne renvoie pas à l'art. 19. **La transposition serait une inférence, pas une source.**
- **Dans quel délai la notification aux destinataires doit intervenir.** L'art. 19 ne donne aucune échéance. L'art. 12.3 régit l'information **de la personne concernée**, pas celle des destinataires.
- **Sous quelle forme, et si elle doit être tracée.**
- **Ce qu'il faut faire quand un destinataire ne donne pas suite.**

Le fait que le CEF 2025 ait posé la question aux 32 autorités et n'ait rien publié en retour est, en soi, une information : **c'est une zone où la pratique n'est pas stabilisée**.

---

# Partie 6 — La forme de la réponse

## 6.1 Le socle : art. 12.1

> « Le responsable du traitement prend des mesures appropriées pour fournir toute information visée aux articles 13 et 14 ainsi que pour procéder à toute communication au titre des articles 15 à 22 et de l'article 34 […] d'une façon **concise, transparente, compréhensible et aisément accessible, en des termes clairs et simples**, en particulier pour toute information destinée spécifiquement à un enfant. Les informations sont fournies **par écrit ou par d'autres moyens y compris, lorsque c'est approprié, par voie électronique**. Lorsque la personne concernée en fait la demande, les informations peuvent être fournies **oralement**, à condition que l'identité de la personne concernée soit démontrée par d'autres moyens. »
> — art. 12.1

Le considérant 58 y ajoute l'illustration par éléments visuels « lorsqu'il y a lieu ». Le CEPD précise que si les données sont des codes ou des « *raw data* », **il faut les expliquer** pour qu'elles fassent sens (résumé exécutif, LD 01/2022) ; que l'exigence s'apprécie au regard de la capacité de compréhension de la personne (enfant, besoins particuliers) ; et que la compilation, quelle qu'en soit la forme, doit satisfaire l'art. 12 (§ 153).

## 6.2 Le canal électronique : deux règles superposées

- **Art. 12.3, dernière phrase** : « *Lorsque la personne concernée présente sa demande sous une forme électronique, les informations sont fournies par voie électronique lorsque cela est possible, à moins que la personne concernée ne demande qu'il en soit autrement.* »
- **Art. 15.3, phrase 3** : « *Lorsque la personne concernée présente sa demande par voie électronique, les informations sont fournies sous une forme électronique d'usage courant, à moins que la personne concernée ne demande qu'il en soit autrement.* »

Le CEPD lit le second comme un durcissement du premier dans le champ de l'accès, et l'étend à **toutes** les informations des art. 15.1 et 15.2, pas seulement à la copie (§ 32).

**Ce que « forme électronique d'usage courant » veut dire** (§ 148-151) :

- L'appréciation est **objective**, « *not on what format the controller uses in its daily operations* ». Le format interne du responsable n'est pas l'étalon.
- À défaut de format sectoriel établi, « *open formats set in an international standard, such as ISO, should, in general, be considered as commonly used* ».
- « *The data subject should not be obliged to buy software in order to get access to the information.* »
- « *the data subjects need to be able to **download** their data in a commonly used electronic form* » — **donner un accès ne suffit pas à valoir copie** (§ 151).
- Le format doit préserver l'intelligibilité et l'accessibilité, et la forme doit être **pérenne** — l'écrit, y compris électronique, est en principe préférable (§ 150).

## 6.3 La copie de l'art. 15.3 : copie des données, pas des documents

> « Le responsable du traitement **fournit une copie des données à caractère personnel faisant l'objet d'un traitement**. »
> — art. 15.3, phrase 1

Le CEPD (§ 152) : le responsable peut, sans y être tenu, fournir les documents originaux ; il peut aussi fournir une **compilation** de toutes les données couvertes, dès lors qu'elle permet à la personne de prendre connaissance du traitement et d'en vérifier la licéité. Mais « *it cannot be made in a way that somehow alters or changes the content of the information* ». Cas où le document lui-même s'impose : écriture manuscrite (l'écriture est une donnée), enregistrement audio (la voix est une donnée) — § 155.

C'est le responsable qui décide de la forme (§ 152), au cas par cas (§ 153).

## 6.4 Le format machine de l'art. 20.1

> « […] dans un **format structuré, couramment utilisé et lisible par machine** […] »
> — art. 20.1

Le CEPD tranche explicitement la comparaison avec l'accès :

> « *Whilst the right of data portability under Art. 20 GDPR requires that the information is provided in a machine readable format, the right to information under Art. 15 does not. Hence, formats that are considered not to be appropriate when complying with a data portability request, **for example pdf-files**, could still be suitable when complying with an access request.* »
> — LD 01/2022, § 156

**Le PDF disqualifie la portabilité et convient à l'accès.** C'est la règle la plus directement actionnable de toute cette partie.

Les lignes directrices WP242 rev.01, endossées par le CEPD le 25 mai 2018, précisent :

- « *structuré, couramment utilisé et lisible par machine* » est un **ensemble d'exigences minimales** dont l'**interopérabilité** est le résultat visé : « *specifications for the means, whereas interoperability is the desired outcome* ».
- Définition retenue de « lisible par machine », empruntée au considérant 21 de la directive 2013/37/UE : « *a file format structured so that software applications can easily identify, recognize and extract specific data […] Documents encoded in a file format that limits automatic processing […] should not be considered to be in a machine-readable format.* »
- Les formats sous licence coûteuse sont exclus (« *would not be considered an adequate approach* »).
- Mais le considérant 68 borne l'exigence : le droit à la portabilité « *ne devrait pas créer d'obligation pour les responsables du traitement d'adopter ou de maintenir des systèmes de traitement qui sont techniquement compatibles* ». WP242 : « *portability aims to produce interoperable systems, not compatible systems* ».

## 6.5 L'accès distant, encouragé mais facultatif

Le considérant 63 : « *Lorsque c'est possible, le responsable du traitement devrait pouvoir donner l'accès à distance à un système sécurisé permettant à la personne concernée d'accéder directement aux données à caractère personnel la concernant.* » Rédigé comme une faculté (« devrait pouvoir »), pas comme une obligation. Le CEF 2024 relève les systèmes en libre-service parmi les **bonnes pratiques** constatées. Rappel du § 6.2 ci-dessus : un tel accès ne dispense pas de rendre les données téléchargeables. Et rappel du § 186 : mieux le libre-service fonctionne, moins l'argument de l'excessivité est disponible.

---

# Partie 7 — Ce qui doit être conservé pour être redevable

C'est le point le plus fragile en source primaire, et il faut le dire franchement.

## 7.1 Ce que le texte impose

> « Le responsable du traitement est responsable du respect du paragraphe 1 et **est en mesure de démontrer que celui-ci est respecté** (responsabilité). »
> — art. 5.2

L'art. 24.1 en tire l'obligation de mesures techniques et organisationnelles appropriées, réexaminées et actualisées.

**Et c'est tout.** Il n'existe dans le RGPD :

- **aucun registre des demandes d'exercice de droits.** L'art. 30 énumère limitativement le contenu du registre des activités de traitement : finalités, catégories de personnes et de données, catégories de destinataires, transferts, délais d'effacement, description générale des mesures de sécurité. **Rien sur les demandes reçues ni sur leur traitement.**
- **aucune durée de conservation** d'une trace de traitement de demande ;
- **aucun format** ni contenu minimal imposé à une telle trace.

**L'art. 5.2 est une obligation de démontrer, sans prescription du moyen.** C'est exactement l'espace où le responsable garde le choix — et donc l'espace ouvert à un outil.

## 7.2 Ce que les autorités en disent, et le disent deux fois

Le CEF 2024 (§ 4.2.3), sur le droit d'accès :

> « *Although **it is not a legal requirement** for controllers to have a procedural document detailing how it responds to access requests, it is apparent that a lack of a formal documented procedure can **heighten the possibility of an infringement** of a data subjects rights.* »
> et : « *Having documented internal procedures can aid a controller in demonstrating its compliance with Art. 12 and 15 GDPR in line with the principle of accountability.* »

Le CEF 2025 (§ 4.2.1), sur l'effacement, reprend et confirme — c'est sa **difficulté n° 1**, relevée par 17 autorités sur 32 :

> « *While the GDPR does not explicitly require controllers to adopt a particular procedure or process for handling erasure requests, a clear and efficient process helps controllers to respond to requests within the legal deadline and adequately address them. More generally, a documented procedure is also useful for controllers to demonstrate compliance with their GDPR obligations, in line with the accountability principle (Art. 5(2) and 24 GDPR).* »

**La position est stable et explicite : pas d'obligation, mais une corrélation constatée entre absence de procédure documentée et infraction, et un adossement à l'art. 5.2.** Le CEF 2024 note en outre que le déficit frappe surtout les petites organisations et celles qui reçoivent peu de demandes — c'est-à-dire, très exactement, la population que sert un microservice mutualisé.

## 7.3 Ce que les autorités recommandent concrètement

Recommandations aux responsables, CEF 2025 § 4.2.1 :

- « *Establish and update internal procedures with clear deadlines and steps, and allocating responsibilities among the different actors for **handling and recording** erasure requests.* »
- « *Map personal data and storage locations (including, when possible, by relying on the ROPA)* ».

Bonnes pratiques **constatées** chez des responsables (même section) :

- « *Use of software or systems where **records are generated automatically as proof of deletion** when an erasure request is granted.* »
- « *Use Key Performance Indicators (KPI) to monitor the handling of erasure requests, such as the **percentage of requests answered within one month or three months** (in case of an extension) ; submit the KPI reports to the management […]* »

Le CEF 2024 ajoute, sur la décision elle-même : les responsables devraient « *document their reasoning in accordance with Art. 5 (2) GDPR* » et « *document their assessment* ». Le CEPD, sur l'art. 12.5 : « *Controllers must be able to demonstrate the manifestly unfounded or excessive character of a request […] it is recommended to ensure **proper documentation of the underlying facts*** » (§ 193).

## 7.4 La tension qu'il faut nommer

Conserver une trace du traitement d'une demande, c'est **traiter des données personnelles**. Deux sources primaires poussent en sens inverse de l'accumulation :

- Considérant 64 : « *Un responsable du traitement **ne devrait pas conserver des données à caractère personnel à la seule fin d'être en mesure de réagir à d'éventuelles demandes**.* »
- LD 01/2022 § 79 : détruire la copie de pièce d'identité immédiatement après vérification, sa conservation étant « *likely to amount to an infringement of the principles of purpose limitation and storage limitation* » ; conserver à la place une mention « *ID card was checked* ».

**Le modèle que les sources dessinent est donc : conserver les faits de la procédure, pas la matière de la demande.** Date de réception, date et contenu de la réponse, droit reconnu, décision et sa motivation, mention que l'identité a été vérifiée — et non le texte brut, ni les pièces justificatives, ni les données communiquées.

⚠️ **Aucune source primaire consultée ne fixe la durée de conservation de cette trace.** Le CEF 2024 met même en garde contre l'application automatique de durées légales prévues pour d'autres documents (comptables, fiscaux) aux échanges liés à une demande de droits : les responsables doivent « *document their assessment* » plutôt que reprendre un délai par analogie (§ 4.2, recommandations).

---

# Partie 8 — Le partage : ce qui se garantit, ce qui s'organise

Colonne « Résultat » : le service devra le garantir ou refuser explicitement de le promettre. Colonne « Organisation » : le responsable garde le choix du moyen ; un outil peut proposer, jamais imposer.

| Obligation | Nature | Qui peut la porter |
| --- | --- | --- |
| Répondre au plus tard à un mois de la **réception par le responsable** (12.3) | **Résultat**, daté | Le responsable seul : lui seul connaît la date de réception, et il ne maîtrise pas le canal d'arrivée (LD § 52-56). Un outil en aval **hérite** d'un compteur déjà lancé. |
| Notifier la prolongation **dans le premier mois** (12.3) | **Résultat**, daté | Automatisable intégralement, si la date de réception est fournie. |
| Motiver le refus, total ou partiel, avec réclamation et recours (12.4) | **Résultat** quant à la forme et au délai ; **organisation** quant au fond | Les trois mentions et l'échéance sont mécanisables. **La décision de refuser ne l'est pas** : charge de la preuve sur le responsable, appréciation au cas par cas imposée (LD § 176). |
| Gratuité (12.5, phrase 1) | **Résultat** | Par défaut. Y déroger est l'exception à justifier. |
| Démontrer le caractère manifestement infondé ou excessif (12.5, al. 3) | **Résultat** quant à la charge ; **organisation** quant à la méthode | Un outil peut **exiger** que la motivation soit saisie avant de laisser passer un refus. Il ne peut pas la produire. |
| Prévenir avant de facturer, permettre le retrait (LD § 31, 194) | Organisation, mais **fortement recommandée** | Automatisable. |
| Ne demander une pièce d'identité qu'en cas de doute raisonnable, et de façon proportionnée (12.6, LD § 70-79) | **Résultat** — c'est une **interdiction**, pas une faculté d'organisation | Un outil qui exigerait une pièce par défaut ferait **basculer** son utilisateur dans la non-conformité. Point de conception non négociable. |
| Authentifier en réutilisant l'authentification existante (LD § 72-73, 75) | Organisation | **Hors de portée d'un service tiers** : il n'a pas accès au référentiel d'authentification. Il peut signaler qu'une authentification est requise ; il ne peut pas la conduire. |
| Ne pas conserver la copie de pièce, garder une mention (LD § 79) | Organisation, mais adossée aux art. 5.1.b) et 5.1.e) | Directement transposable en modèle de trace. |
| Notifier chaque destinataire, sous-traitants compris (19) | **Résultat**, sous réserve d'impossibilité ou d'efforts disproportionnés | Le responsable seul. Suppose de connaître la cartographie des flux sortants. **Aucun outil de qualification ne peut y contribuer.** |
| Tenir la liste **nominative** des destinataires (15.1.c) + C-154/21) | **Résultat** | Le responsable seul, via son registre (CEF 2024, § 4.2). |
| Répondre par voie électronique si la demande l'était (12.3) ; en forme électronique d'usage courant pour l'accès (15.3) ; en format lisible par machine pour la portabilité (20.1) | **Résultat** | Le producteur de la charge utile. Un service qui ne produit pas les données ne peut pas le garantir. |
| Répondre de façon concise, transparente, intelligible (12.1) | **Résultat** quant à l'exigence ; **organisation** quant à la mise en œuvre | Modèles de réponse, mise en intelligibilité — un outil y contribue réellement. |
| Rendre les données **téléchargeables**, l'accès ne valant pas copie (LD § 151) | **Résultat** | Le producteur de la charge utile. |
| Disposer d'une procédure interne documentée | **Organisation** — explicitement pas une obligation légale (CEF 2024 § 4.2.3, CEF 2025 § 4.2.1) | Un outil **est** une procédure documentée, exécutée. C'est le terrain le plus favorable. |
| Documenter la décision et sa motivation (art. 5.2, CEF 2024, LD § 193) | **Résultat** quant au fait de pouvoir démontrer ; **organisation** quant au support | Idem. |
| Accuser réception | **Ni l'un ni l'autre** — bonne pratique (LD § 57, CEF 2024) | Automatisable, et corrélé à moins de plaintes. |

---

# Partie 9 — Ce qui reste sans réponse en source primaire

Recensé explicitement, parce que c'est ce dont la décision suivante aura besoin.

**Sur le délai**

- La **suspension du compteur** pendant la vérification d'identité n'a pas de base textuelle. Le CEPD écrit « *there may be a suspension* » (§ 159) — permissif, au conditionnel. **Aucune durée maximale n'est fixée** à cet échange, et aucune source ne dit ce qu'il advient si la personne ne répond jamais.
- Aucune source ne dit dans quel délai un canal non dédié doit réacheminer une demande en interne. Le CEPD dit seulement « *all reasonable efforts* » et interdit d'en tirer un allongement du délai (exemple 8).
- Le « 8 jours pour les données de santé » de la CNIL **n'a pas de fondement dans l'art. 12.3** ; sa base nationale n'a pas été tracée ici.

**Sur l'article 19**

- **Aucune définition, aucun critère, aucun exemple** d'« efforts disproportionnés ». Le considérant 62 offre des critères pour la même expression **dans un autre article** ; les transposer serait une inférence.
- **Aucun délai** pour la notification aux destinataires.
- **Aucune forme** prescrite, et rien sur l'obligation de tracer cette notification.
- **Rien** sur la conduite à tenir si un destinataire ne répercute pas.
- Le CEF 2025 a posé la question aux 32 autorités et **n'a rien publié en retour**.

**Sur le refus**

- Le CEPD n'a analysé « manifestement infondée ou excessive » que **pour l'accès**. La transposition aux cinq autres droits n'est validée par aucune source consultée, et le raisonnement du § 177 ne s'y transpose pas mécaniquement.
- Aucune source ne dit si une demande peut être **partiellement** jugée excessive.

**Sur la conservation**

- **Aucune durée de conservation** de la trace du traitement d'une demande. Le CEF 2024 met explicitement en garde contre l'emprunt à d'autres régimes (comptable, fiscal, archives).
- **Aucun contenu minimal** prescrit à cette trace.
- **Aucun registre des demandes** n'est exigé : l'art. 30 n'en dit rien.

**Sur l'intermédiation**

- **Aucune source primaire consultée n'aborde le cas d'un outil tiers qui reçoit, qualifie ou instruit des demandes pour le compte d'un responsable.** Les lignes directrices traitent des portails tiers **du côté du demandeur** (LD § 80-82, mandataires), pas d'un composant sous-traitant côté responsable. Le statut d'un tel composant (sous-traitant au sens de l'art. 28, très vraisemblablement) et la répartition des rôles ne sont documentés par **aucune** des sources examinées.
- L'obligation de l'art. 12.3 de répondre « par voie électronique » quand la demande l'était **n'est traitée nulle part pour une saisine machine à machine** (formulaire, API).

---

## Sources

**Texte**

- [Règlement (UE) 2016/679, version française du JO L 119/1 du 4.5.2016](http://publications.europa.eu/resource/celex/32016R0679) — ELI [`reg/2016/679/oj/fra`](https://eur-lex.europa.eu/eli/reg/2016/679/oj/fra). Articles 4.9, 5, 11, 12, 15 à 22, 24, 30 ; considérants 57 à 68.
- Reproduction CNIL, [chapitre I](https://www.cnil.fr/fr/reglement-europeen-protection-donnees/chapitre1), [chapitre II](https://www.cnil.fr/fr/reglement-europeen-protection-donnees/chapitre2), [chapitre III](https://www.cnil.fr/fr/reglement-europeen-protection-donnees/chapitre3).
- [Règlement (CEE, Euratom) n° 1182/71](https://eur-lex.europa.eu/eli/reg/1971/1182/oj) — calcul des délais.

**Jurisprudence**

- CJUE, 12 janvier 2023, *RW c. Österreichische Post AG*, [C-154/21](https://eur-lex.europa.eu/legal-content/FR/TXT/?uri=CELEX:62021CJ0154), ECLI:EU:C:2023:3.

**CEPD**

- [Lignes directrices 01/2022 sur les droits des personnes concernées — Droit d'accès](https://www.edpb.europa.eu/system/files/2023-04/edpb_guidelines_202201_data_subject_rights_access_v2_en.pdf), version 2.1, adoptées le 28 mars 2023 ([page de référence](https://www.edpb.europa.eu/our-work-tools/documents/public-consultations/2022/guidelines-012022-data-subject-rights-right_en)).
- [Rapport de l'action coordonnée 2024 — Mise en œuvre du droit d'accès](https://www.edpb.europa.eu/system/files/2025-01/edpb_cef-report-2024_20250116_rightofaccess_en.pdf), adopté le 16 janvier 2025.
- [Rapport de l'action coordonnée 2025 — Mise en œuvre du droit à l'effacement](https://www.edpb.europa.eu/system/files/documents/2026-02/edpb_cef-report_2025_right-to-erasure_en.pdf), adopté le 18 février 2026.
- [Lignes directrices WP242 rev.01 sur le droit à la portabilité des données](https://ec.europa.eu/newsroom/article29/items/611233), G29, révisées le 5 avril 2017, [endossées par le CEPD](https://www.edpb.europa.eu/our-work-tools/general-guidance/endorsed-wp29-guidelines_en) le 25 mai 2018.

**CNIL**

- [Professionnels : comment répondre à une demande de droit d'accès ?](https://www.cnil.fr/fr/repondre-une-demande-de-droit-dacces)
- [Exercice de mes droits : dois-je fournir obligatoirement une copie de ma pièce d'identité ?](https://www.cnil.fr/fr/cnil-direct/question/exercice-de-mes-droits-informatique-et-libertes-dois-je-fournir-obligatoirement)
- [IA : respecter et faciliter l'exercice des droits des personnes concernées](https://www.cnil.fr/fr/ia-respecter-lexercice-des-droits-des-personnes)

**Note de méthode** — les serveurs web d'EUR-Lex renvoient un corps vide aux clients non navigateurs ; le texte français officiel a donc été obtenu du service *cellar* de l'Office des publications, qui sert le même document ELI. Les liens EUR-Lex ci-dessus restent la référence canonique pour un lecteur humain.
