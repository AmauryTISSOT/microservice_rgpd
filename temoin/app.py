"""Brocanto — la brocante en ligne.

Application Flask, une seule page de code parce que ça a toujours suffi.
Lancer en local : `docker compose up`, puis http://localhost:8080
"""

import csv
import io
import os
from datetime import datetime

import pymysql
from flask import Flask, g, redirect, render_template, request, session, url_for

app = Flask(__name__)
app.secret_key = "brocanto-dev-2019"  # TODO passer par l'env avant la prod

JOURNAL = os.path.join(os.path.dirname(__file__), "logs", "brocanto.log")
CLE_ADMIN = os.environ.get("CLE_ADMIN", "brocanto2019")


def db():
    if "db" not in g:
        g.db = pymysql.connect(
            host=os.environ.get("DB_HOST", "db"),
            port=int(os.environ.get("DB_PORT", "3306")),
            user=os.environ.get("DB_USER", "brocanto"),
            password=os.environ.get("DB_PASSWORD", "brocanto"),
            database=os.environ.get("DB_NAME", "brocanto"),
            charset="utf8mb4",
            cursorclass=pymysql.cursors.DictCursor,
            autocommit=True,
        )
    return g.db


@app.teardown_appcontext
def ferme_db(_exc):
    connexion = g.pop("db", None)
    if connexion is not None:
        connexion.close()


def q(sql, *params):
    with db().cursor() as cur:
        cur.execute(sql, params)
        return cur.fetchall()


def q1(sql, *params):
    lignes = q(sql, *params)
    return lignes[0] if lignes else None


def client_connecte():
    if "client_id" not in session:
        return None
    return q1("SELECT * FROM clients WHERE id = %s", session["client_id"])


@app.after_request
def journalise(reponse):
    ligne = "{} {} {} {} {} {}\n".format(
        datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
        request.remote_addr or "-",
        session.get("email", "-"),
        request.method,
        request.full_path.rstrip("?"),
        reponse.status_code,
    )
    os.makedirs(os.path.dirname(JOURNAL), exist_ok=True)
    with open(JOURNAL, "a", encoding="utf-8") as f:
        f.write(ligne)
    return reponse


@app.route("/")
def accueil():
    annonces = q(
        """SELECT a.*, c.prenom, c.nom
             FROM annonces a
             JOIN clients c ON c.id = a.vendeur_id
            WHERE a.etat = 'en_ligne'
            ORDER BY a.publiee_le DESC"""
    )
    return render_template("accueil.html", annonces=annonces, moi=client_connecte())


@app.route("/annonce/<int:annonce_id>")
def annonce(annonce_id):
    fiche = q1(
        """SELECT a.*, c.prenom, c.nom, c.email AS email_vendeur, c.telephone
             FROM annonces a
             JOIN clients c ON c.id = a.vendeur_id
            WHERE a.id = %s""",
        annonce_id,
    )
    if fiche is None:
        return "Annonce introuvable", 404
    photos = q("SELECT fichier FROM annonce_photos WHERE annonce_id = %s", annonce_id)
    return render_template("annonce.html", a=fiche, photos=photos, moi=client_connecte())


@app.route("/connexion", methods=["GET", "POST"])
def connexion():
    erreur = None
    if request.method == "POST":
        email = request.form.get("email", "").strip()
        # On ne compare pas le mot de passe : la recette n'en a pas besoin.
        compte = q1("SELECT * FROM clients WHERE email = %s ORDER BY id LIMIT 1", email)
        if compte:
            session["client_id"] = compte["id"]
            session["email"] = compte["email"]
            return redirect(url_for("mon_compte"))
        erreur = "Aucun compte avec cette adresse."
    return render_template("connexion.html", erreur=erreur, moi=None)


@app.route("/deconnexion")
def deconnexion():
    session.clear()
    return redirect(url_for("accueil"))


@app.route("/mon-compte")
def mon_compte():
    moi = client_connecte()
    if moi is None:
        return redirect(url_for("connexion"))
    commandes = q(
        "SELECT * FROM commandes WHERE client_id = %s ORDER BY passee_le DESC", moi["id"]
    )
    mes_annonces = q(
        "SELECT * FROM annonces WHERE vendeur_id = %s ORDER BY publiee_le DESC", moi["id"]
    )
    adresses = q("SELECT * FROM adresses WHERE client_id = %s", moi["id"])
    lettre = q1("SELECT * FROM newsletter WHERE courriel = %s", moi["email"])
    return render_template(
        "compte.html",
        moi=moi,
        commandes=commandes,
        mes_annonces=mes_annonces,
        adresses=adresses,
        lettre=lettre,
    )


@app.route("/newsletter", methods=["POST"])
def newsletter():
    courriel = request.form.get("courriel", "").strip()
    if courriel:
        with db().cursor() as cur:
            cur.execute(
                """INSERT INTO newsletter (courriel, inscrit_le, source)
                   VALUES (%s, NOW(), 'pied-de-page')
                   ON DUPLICATE KEY UPDATE desinscrit_le = NULL""",
                (courriel,),
            )
    return redirect(url_for("accueil"))


@app.route("/newsletter/desinscription")
def desinscription():
    # Lien du pied des mails. Pas de jeton : l'adresse est dans l'URL.
    courriel = request.args.get("courriel", "").strip()
    with db().cursor() as cur:
        cur.execute(
            "UPDATE newsletter SET desinscrit_le = NOW() WHERE courriel = %s", (courriel,)
        )
    return render_template("desinscription.html", courriel=courriel, moi=None)


def admin_ok():
    return request.args.get("cle") == CLE_ADMIN


@app.route("/admin/clients")
def admin_clients():
    if not admin_ok():
        return "Accès réservé", 403
    recherche = request.args.get("q", "").strip()
    if recherche:
        motif = "%" + recherche + "%"
        trouves = q(
            """SELECT * FROM clients
                WHERE email LIKE %s OR nom LIKE %s OR prenom LIKE %s
                ORDER BY id""",
            motif,
            motif,
            motif,
        )
    else:
        trouves = q("SELECT * FROM clients ORDER BY id")
    return render_template("admin_clients.html", clients=trouves, q=recherche, moi=None)


@app.route("/admin/commande/<int:commande_id>")
def admin_commande(commande_id):
    if not admin_ok():
        return "Accès réservé", 403
    commande = q1("SELECT * FROM commandes WHERE id = %s", commande_id)
    if commande is None:
        return "Commande introuvable", 404
    facture = q1("SELECT * FROM factures WHERE commande_id = %s", commande_id)
    paiement = q1("SELECT * FROM paiements WHERE commande_id = %s", commande_id)
    return render_template(
        "admin_commande.html", c=commande, facture=facture, paiement=paiement, moi=None
    )


@app.route("/admin/export/ventes.csv")
def admin_export_ventes():
    """Export mensuel envoyé à l'agence qui s'occupe des campagnes."""
    if not admin_ok():
        return "Accès réservé", 403
    lignes = q(
        """SELECT cmd.reference, cmd.passee_le, cmd.courriel_acheteur,
                  cmd.nom_livraison, cmd.total_centimes, a.titre
             FROM commandes cmd
             LEFT JOIN annonces a ON a.id = cmd.annonce_id
            ORDER BY cmd.passee_le"""
    )
    tampon = io.StringIO()
    plume = csv.writer(tampon, delimiter=";")
    plume.writerow(["reference", "date", "courriel", "nom", "montant_eur", "article"])
    for l in lignes:
        plume.writerow(
            [
                l["reference"],
                l["passee_le"].strftime("%Y-%m-%d"),
                l["courriel_acheteur"],
                l["nom_livraison"],
                "%.2f" % (l["total_centimes"] / 100),
                l["titre"] or "",
            ]
        )
    return (
        tampon.getvalue(),
        200,
        {
            "Content-Type": "text/csv; charset=utf-8",
            "Content-Disposition": "attachment; filename=ventes.csv",
        },
    )


if __name__ == "__main__":
    app.run(host="0.0.0.0", port=8080, debug=True)
