// LE TABLEAU DES DEMANDES RGPD : le bouton « Créer une demande » ouvre la modale que le serveur a
// rendue, formulaire remis à zéro, et quatre modes de fermeture la referment — « Annuler », la croix,
// Échap, un clic sur le fond.
//
// ⚠️ POUR L'INSTANT, CHACUN DES QUATRE FERME DIRECTEMENT, saisie comprise. Ils passent tous par
// `requestClose`, et c'est là que la confirmation d'abandon se posera, avec son propre ticket :
// aucun mode de fermeture ne doit pouvoir la contourner.

const dialog = document.getElementById("create-request");
const opener = document.getElementById("create-request-open");
const form = document.getElementById("create-request-form");
const receivedOn = form.elements.namedItem("receivedOn");

// « AUJOURD'HUI » S'ENTEND À PARIS, comme côté serveur (ParisCalendar) : ni en UTC, ni au fuseau du
// poste de l'Operator. Le format `en-CA` n'est qu'un moyen d'obtenir les trois parties en chiffres ;
// elles sont relues une à une, sans dépendre de l'ordre dans lequel une locale les écrit.
const parisCalendar = new Intl.DateTimeFormat("en-CA", {
  timeZone: "Europe/Paris",
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
});

function todayInParis() {
  const parts = Object.fromEntries(
    parisCalendar.formatToParts(new Date()).map(({ type, value }) => [type, value]),
  );

  return `${parts.year}-${parts.month}-${parts.day}`;
}

// LE FORMULAIRE REPART DE ZÉRO À CHAQUE OUVERTURE : l'Operator n'hérite jamais d'une saisie
// précédente. Les valeurs par défaut sont celles que le serveur a rendues, sauf « aujourd'hui », qui
// est recalculé ici — une page restée ouverte au-delà de minuit ne doit ni proposer la veille, ni
// interdire le jour même. Il devient la valeur par défaut du champ, et non seulement sa valeur.
function resetToDefaults() {
  form.reset();

  const today = todayInParis();
  receivedOn.max = today;
  receivedOn.defaultValue = today;
  receivedOn.value = today;
}

function open() {
  resetToDefaults();
  dialog.showModal();

  // `showModal` donnerait le focus à la croix, premier élément focalisable de la modale : il va au
  // premier champ, pour que la saisie commence aussitôt.
  form.elements[0].focus();
}

function requestClose() {
  dialog.close();
}

opener.addEventListener("click", open);

for (const dismiss of dialog.querySelectorAll("[data-dismiss]")) {
  dismiss.addEventListener("click", requestClose);
}

// Échap : le navigateur fermerait seul, sans passer par `requestClose`. Il est donc retenu, et la
// fermeture repasse par le même chemin que les trois autres modes.
dialog.addEventListener("cancel", (event) => {
  event.preventDefault();
  requestClose();
});

// LE FOND N'EST PAS UN ÉLÉMENT : un clic dessus arrive sur le `dialog` lui-même, hors de son cadre.
// ⚠️ L'appui ET le relâchement doivent tomber sur le fond. Une sélection de texte commencée dans la
// modale et relâchée dehors produit elle aussi un clic sur le `dialog` : elle ne doit rien fermer.
let pressedOnTheBackdrop = false;

function isOnTheBackdrop(event) {
  if (event.target !== dialog) {
    return false;
  }

  const frame = dialog.getBoundingClientRect();

  return (
    event.clientX < frame.left ||
    event.clientX > frame.right ||
    event.clientY < frame.top ||
    event.clientY > frame.bottom
  );
}

dialog.addEventListener("pointerdown", (event) => {
  pressedOnTheBackdrop = isOnTheBackdrop(event);
});

dialog.addEventListener("click", (event) => {
  if (pressedOnTheBackdrop && isOnTheBackdrop(event)) {
    requestClose();
  }

  pressedOnTheBackdrop = false;
});
