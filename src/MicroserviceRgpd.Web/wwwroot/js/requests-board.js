// LE TABLEAU DES DEMANDES RGPD : le bouton « Créer une demande » ouvre la modale que le serveur a
// rendue, et quatre modes de fermeture la referment — « Annuler », la croix, Échap, un clic sur le fond.
//
// ⚠️ TANT QU'AUCUNE SAISIE N'EST POSSIBLE, CHACUN DES QUATRE FERME DIRECTEMENT. Ils passent tous par
// `requestClose`, et c'est là que la confirmation d'abandon se posera, avec la saisie qui la rend
// nécessaire : aucun mode de fermeture ne doit pouvoir la contourner.

const dialog = document.getElementById("create-request");
const opener = document.getElementById("create-request-open");

function requestClose() {
  dialog.close();
}

opener.addEventListener("click", () => dialog.showModal());

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
