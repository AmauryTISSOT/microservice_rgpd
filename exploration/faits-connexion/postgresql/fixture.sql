-- Base d'épreuve PostgreSQL : de quoi exercer chacun des faits à vérifier.

CREATE SCHEMA audit;

CREATE TABLE public.adherents (
  id            bigserial PRIMARY KEY,
  a_supprimer   text,                 -- droppée plus bas : creuse un trou dans attnum
  adr_l1        varchar(255),
  courriel      text NOT NULL,
  photo         bytea,
  signature     bytea,
  solde         numeric(12,2),
  cree_le       timestamptz,
  actif         boolean,
  jeton         uuid,
  ip            inet,
  etiquettes    text[],
  meta          jsonb,
  duree         interval,
  point_geo     point
);
ALTER TABLE public.adherents DROP COLUMN a_supprimer;

COMMENT ON TABLE  public.adherents           IS 'les adhérents';
COMMENT ON COLUMN public.adherents.courriel  IS 'adresse de contact';

CREATE TABLE public.cotisations (
  id           bigserial PRIMARY KEY,
  adherent_id  bigint REFERENCES public.adherents(id),
  montant      numeric(10,2)
);

-- Même nom de table dans un second schéma : le multi-schéma doit les distinguer.
CREATE TABLE audit.adherents (
  id       bigserial PRIMARY KEY,
  qui      text,
  quand    timestamptz
);

CREATE VIEW public.v_adherents AS SELECT id, courriel FROM public.adherents;

INSERT INTO public.adherents (adr_l1, courriel, photo, solde, actif, meta)
VALUES ('12 rue des Lilas', 'ada@example.org', '\x0102039fff'::bytea, 42.50, true, '{"a":1}'),
       (NULL,               'bob@example.org', NULL,                  NULL,   false, NULL);

-- Le rôle de l'épreuve : peut se connecter, ne possède AUCUN privilège objet.
CREATE ROLE sans_droits LOGIN PASSWORD 'sans_droits';
REVOKE ALL ON ALL TABLES IN SCHEMA public FROM sans_droits;
REVOKE ALL ON ALL TABLES IN SCHEMA audit  FROM sans_droits;
REVOKE ALL ON SCHEMA public FROM sans_droits;
REVOKE ALL ON SCHEMA audit  FROM sans_droits;
