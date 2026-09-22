// L'ARBITRAGE D'UNE TABLE, SANS RECHARGER LA PAGE : un clic sur « Retenir », « Écarter » ou un
// geste de lot poste le MÊME formulaire que sans script, puis remplace les seules régions que la
// réponse a changées — l'avancement, la liste des tables, la table ouverte et les comptes.
//
// ⚠️ SANS CE MODULE, l'écran reste entier : chaque bouton poste son formulaire, et la redirection
// ramène sur la colonne tranchée. Le module ne décide de rien — il n'y a ici ni règle, ni compte,
// ni état : ce qui s'affiche est ce que le serveur a rendu, région pour région.
//
// ⚠️ UN RENVOI AILLEURS EST RENDU TEL QUEL. Un arbitrage refusé mène au rapport, avec la phrase
// qui dit que rien n'a été enregistré ; cette phrase se lit une seule fois côté serveur, et elle
// est déjà dans la réponse que le module a reçue. La redemander l'aurait perdue.

const regions = "[data-arbitration-live]";
const detail = document.querySelector('[data-arbitration-live="detail"]');

if (detail && window.fetch && window.DOMParser) {
  let busy = false;

  // L'annonce aux lecteurs d'écran : un remplacement silencieux ne leur dirait pas que le geste a
  // porté.
  const status = document.createElement("p");
  status.className = "visually-hidden";
  status.setAttribute("role", "status");
  document.body.append(status);

  document.addEventListener("submit", async (event) => {
    const form = event.target;

    if (
      !(form instanceof HTMLFormElement) ||
      form.method.toLowerCase() !== "post" ||
      !form.closest('[data-arbitration-live="detail"]')
    ) {
      return;
    }

    event.preventDefault();

    if (busy) {
      return;
    }

    busy = true;

    const submitter = event.submitter;
    const body = new FormData(form);

    // Le bouton cliqué porte l'issue : il n'est dans les données du formulaire que si on l'y met.
    if (submitter?.name) {
      body.append(submitter.name, submitter.value);
    }

    // Où l'on était : le bloc de la colonne tranchée, et sa hauteur dans la fenêtre.
    const block = form.closest('[id^="colonne-"]');
    const anchor = block?.id;
    const offset = block?.getBoundingClientRect().top;

    form.setAttribute("aria-busy", "true");

    let response;

    try {
      response = await fetch(form.action, {
        method: "POST",
        body: new URLSearchParams(body),
        credentials: "same-origin",
      });
    } catch {
      // Le réseau a lâché : on repasse par le formulaire nu, qui dira lui-même ce qu'il advient.
      busy = false;
      form.removeAttribute("aria-busy");
      HTMLFormElement.prototype.submit.call(withRuling(form, submitter));
      return;
    }

    const html = await response.text();
    const next = new DOMParser().parseFromString(html, "text/html");
    const landed = new URL(response.url);

    if (landed.pathname !== location.pathname || !next.querySelector(regions)) {
      replaceTheWholePage(next, landed);
      return;
    }

    for (const region of document.querySelectorAll(regions)) {
      const fresh = next.querySelector(
        `[data-arbitration-live="${region.dataset.arbitrationLive}"]`,
      );

      if (fresh) {
        region.replaceWith(document.importNode(fresh, true));
      }
    }

    // On reste là où l'on était : la colonne tranchée, à la même hauteur — jamais la suivante.
    const again = anchor ? document.getElementById(anchor) : null;

    if (again && offset !== undefined) {
      window.scrollBy(0, again.getBoundingClientRect().top - offset);
      (again.querySelector("summary") ?? again.querySelector("button"))?.focus({
        preventScroll: true,
      });
    }

    status.textContent = announcementIn(next);
    busy = false;
  });

  // Le compte lu après le geste : c'est lui, et non un « enregistré », qui dit que le geste a porté.
  function announcementIn(page) {
    const notice = page.querySelector(".notice, .refusals");
    const count = page.querySelector(".arbitration-progress-count");
    return [notice?.textContent, count?.textContent]
      .filter(Boolean)
      .map((text) => text.replace(/\s+/g, " ").trim())
      .join(" ");
  }

  function replaceTheWholePage(page, landed) {
    history.pushState(null, "", landed.href);
    document.title = page.title;
    document.body.replaceWith(document.importNode(page.body, true));
    window.scrollTo(0, 0);
  }

  // Le formulaire nu ne porte pas le bouton cliqué quand on le soumet par programme.
  function withRuling(form, submitter) {
    if (submitter?.name) {
      const field = document.createElement("input");
      field.type = "hidden";
      field.name = submitter.name;
      field.value = submitter.value;
      form.append(field);
    }
    return form;
  }
}
