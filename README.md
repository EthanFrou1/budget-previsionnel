# budget-previsionnel

Outil personnel/familial de gestion de factures, budgets et prévisionnel
financier — import manuel de relevés bancaires (CSV/OFX), catégorisation,
suivi d'épargne et de crédits, projection mensuelle/annuelle.

Contexte et décisions de conception : [`brief-projet-budget-previsionnel.md`](brief-projet-budget-previsionnel.md).
Plan de développement détaillé, lot par lot : [`docs/roadmap.md`](docs/roadmap.md).

## Architecture

Solution .NET en couches (Clean Architecture allégée, pas de CQRS/MediatR —
non justifié pour ce périmètre) :

```
BudgetPrevisionnel.sln
src/
  BudgetPrevisionnel.Domain          entités métier, aucune dépendance
  BudgetPrevisionnel.Application     contrats (IBankStatementParser...), DTOs
  BudgetPrevisionnel.Infrastructure  EF Core, PostgreSQL, parseurs bancaires
  BudgetPrevisionnel.Api             contrôleurs, DI, Program.cs, Swagger
tests/
  BudgetPrevisionnel.Domain.Tests
  BudgetPrevisionnel.Application.Tests
  BudgetPrevisionnel.Infrastructure.Tests
  BudgetPrevisionnel.Api.Tests         WebApplicationFactory + Testcontainers
frontend/                              SPA React/TypeScript (Lot 10+, voir plus bas)
```

Règle de dépendance : `Domain` ← `Application` ← `Infrastructure` ← `Api`.
Chaque couche ne connaît que celles à sa gauche. Les parseurs bancaires
(un par banque, ex. `BoursoBankCsvParser`) implémentent l'interface commune
`IBankStatementParser` définie dans `Application` — pas de moteur de parsing
générique multi-banques, le format de chaque banque est trop hétérogène pour
que ça vaille le coup (cf. brief).

## Erreurs, validation, logs

Toutes les erreurs métier passent par `AppExceptionHandler`, plus de
`try/catch` dans les contrôleurs : chaque exception hérite de
`NotFoundException` (404), `ValidationException` (400), `ConflictException`
(409), `ForbiddenException` (403) ou `UnauthorizedException` (401) — définies
dans `Application/Common/AppException.cs` — et la réponse `ProblemDetails`
correspondante est générée automatiquement. Une référence invalide *dans* une
requête (`parentCategoryId`, `linkedAccountId`, `categoryId`...) est un 400
(`InvalidReferenceException`), distincte d'une ressource primaire introuvable
par son id dans l'URL (404).

Validation d'entrée via FluentValidation (`Api/Contracts/**/*Validators.cs`),
plus les règles métier qui ont besoin d'accès base restent dans les Services
(les deux se recoupent parfois délibérément — voir `docs/roadmap.md#lot-9--durcissement-fait`).

Logs structurés (Serilog) : console + `logs/log-.txt` (non commité, 14 jours
conservés), une ligne par requête (`UseSerilogRequestLogging`, enregistré
*avant* `UseExceptionHandler` dans le pipeline — l'ordre inverse fait loguer
une exception bien gérée comme un faux 500, cf. roadmap).

## Import bancaire

Import en deux temps, aucune transaction n'est créée avant que l'utilisateur ait
validé — voir `docs/roadmap.md#revue-avant-import-fait` pour le détail :

- `POST /api/bank-accounts/{id}/import/preview` (multipart, champ `file`) parse
  l'export CSV, déduplique contre les transactions déjà en base et propose une
  catégorie par ligne, sans rien persister.
- `POST /api/bank-accounts/{id}/import/commit` (`{ rows: [...], fileContentBase64?
  }`) reçoit exactement les lignes que l'utilisateur a gardées (certaines
  potentiellement exclues, d'autres avec une catégorie modifiée) et les
  enregistre — la déduplication est revérifiée à ce stade au cas où quelque
  chose aurait changé entre les deux appels. `fileContentBase64` (optionnel) est
  stocké tel quel sur l'`ImportBatch` correspondant.
- `GET /api/bank-accounts/{id}/import/history/{batchId}/file` retélécharge le
  CSV exact d'un import passé — 404 si ce batch n'a pas de fichier stocké (import
  antérieur à cette fonctionnalité, ou `fileContentBase64` non envoyé).

Le pipeline (voir `docs/roadmap.md#lot-3--import-bancaire-fait`) détecte aussi les
virements entre comptes du même utilisateur, une fois les transactions
persistées par `commit`. Aucun autre format bancaire n'est géré pour l'instant —
le `BankName` du compte doit correspondre exactement au `BankName` d'un parseur
enregistré (`"BoursoBank"`).

Le parseur a été validé contre un vrai export ; par précaution ce fichier n'a
jamais été commité (il contient un numéro de compte et des noms de personnes).
Les tests versionnés (`BoursoBankCsvParserTests`) utilisent une fixture
synthétique qui reproduit les mêmes particularités de format.

## Catégories

`GET/POST/PUT/DELETE /api/categories` et `GET/POST/DELETE /api/category-rules`.
Les 10 catégories système (seedées au Lot 1) sont en lecture seule — création,
modification et suppression ne sont possibles que sur ses propres catégories
personnalisées. À l'import (voir `docs/roadmap.md#lot-4--catégories-fait`), la
correspondance exacte entre catégorie bancaire suggérée et catégorie système
reste prioritaire ; les règles `CategoryRule` de l'utilisateur ne servent que
de filet de sécurité quand cette correspondance échoue.

## Comptes bancaires & transactions

`GET/POST/PUT/DELETE /api/bank-accounts` — supprimer un compte supprime en
cascade toutes ses transactions.

`GET /api/transactions` sert à la fois la vue consolidée (tous comptes,
omettre `bankAccountId`) et la vue par compte (`bankAccountId` fourni) :

| Paramètre | Effet |
|---|---|
| `bankAccountId` | Limite à un compte (sinon : tous les comptes de l'utilisateur) |
| `categoryId` | Filtre par catégorie |
| `fromDate` / `toDate` | Filtre par période (format `yyyy-MM-dd`) |
| `search` | Recherche insensible à la casse sur le libellé brut ou nettoyé |
| `excludeInternalTransfers` | Exclut les transactions flaggées comme virement interne |
| `page` / `pageSize` | Pagination (`pageSize` max 200) |

`excludeInternalTransfers` répond à la mise en garde du brief sur le double
comptage des virements internes dans une vue consolidée — mais ce lot ne
calcule aucun total ; les agrégats (solde, répartition par catégorie) sont le
rôle du Lot 8.

`PUT /api/transactions/{id}/category` (`{ "categoryId": number | null }`, ajouté au
Lot 12 pour l'écran transactions) permet de recatégoriser manuellement une transaction
après import ; `categoryId: null` la désincatégorise. La catégorie doit être visible par
l'utilisateur (système ou sienne), sinon 400 ; une transaction inexistante ou d'un autre
utilisateur renvoie 404.

## Épargne, crédits, dépenses récurrentes

`GET/POST/PUT/DELETE` sur `/api/savings-goals`, `/api/loans` et
`/api/recurring-expenses`. Règle de validation notable : `Loan.RemainingAmount`
ne peut pas dépasser `Loan.PrincipalAmount` (rejeté en 400). Les montants sont
stockés en magnitude positive (pas de convention de signe négatif comme sur
`Transaction.Amount`) — le nom de chaque entité encode déjà la direction.

`RecurrenceFrequency` (sur `RecurringExpense`) est le premier enum exposé par
l'API ; il sérialise en chaîne (`"Monthly"`, pas `1`) — convention à garder
pour tout futur enum exposé.

## Prévisionnel

`GET/POST/PUT/DELETE /api/budgets?month=yyyy-MM-dd` — planifié par catégorie
et par mois, avec le réel calculé à la volée depuis les transactions (jamais
stocké). Une seule entrée par (catégorie, mois) ; en double, 409.

`GET /api/forecast/monthly?month=` et `GET /api/forecast/annual?year=`
combinent `Budget` + `RecurringExpense` + `Loan` en un prévisionnel. Règle de
précédence : une catégorie avec un `Budget` pour le mois l'utilise ; une
dépense récurrente ne comble que les catégories sans budget explicite (sinon
un loyer récurrent et son budget Logement se cumuleraient). Les crédits n'ont
pas de catégorie dans le modèle — `LoanPayments` est une ligne à part, pas une
ligne de catégorie.

Les dépenses récurrentes hebdomadaires ne sont pas approximées à "montant ×
4,33" : le nombre exact d'occurrences du jour de semaine dans le mois ciblé
est calculé (voir `RecurringExpenseProjector`), donc le prévisionnel varie
correctement d'un mois à l'autre plutôt que d'utiliser une moyenne lissée.

`GET /api/calendar/monthly?month=` et `GET /api/calendar/annual?year=`
exposent les mêmes occurrences datées (`RecurringExpenseProjector.GetOccurrenceDates`
+ `LoanProjector`) pour une vue calendrier plutôt qu'un total : chaque entrée
porte sa date exacte et un drapeau `isLastOccurrence` (dernier paiement avant
`EndDate`). Les lignes de `Budget` n'y apparaissent jamais — elles ne sont pas
rattachées à un jour précis.

## Dashboard

Trois endpoints sous `/api/dashboard`, même convention `bankAccountId` que
`/api/transactions` (absent = vue consolidée, présent = vue par compte) :

| Endpoint | Contenu |
|---|---|
| `GET .../balance-evolution?fromDate=&toDate=&startingBalance=` | Flux net cumulé — **pas le vrai solde bancaire**, l'app ne le persiste pas (voir `docs/roadmap.md#lot-8--agrégation-dashboard-fait`). `startingBalance` optionnel pour ancrer sur un solde connu. |
| `GET .../category-breakdown?fromDate=&toDate=` | Dépenses par catégorie (positif), période donnée. Les transactions sans catégorie apparaissent sous `categoryId: null`. |
| `GET .../monthly-comparison?year=` | Revenus/dépenses/net par mois, toujours 12 entrées (complétées à zéro), année courante par défaut. |

Les virements internes sont exclus de `category-breakdown` et
`monthly-comparison` dans tous les cas ; pour `balance-evolution`, ils sont
inclus en vue par compte (un virement change vraiment le solde de ce compte)
mais exclus en vue consolidée.

## Stack technique

- Backend : .NET 8, ASP.NET Core Web API (contrôleurs), EF Core, PostgreSQL.
- Frontend : Vite + React 19 + TypeScript, TanStack Query, Tailwind CSS v4,
  PWA (manifest + service worker minimal, pas de mode offline).
- Gestion centralisée des versions de packages NuGet via `Directory.Packages.props`.

## Démarrer en local

### Prérequis
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Docker (pour PostgreSQL local)

### Base de données

```bash
docker compose up -d
```

Démarre PostgreSQL sur `localhost:5432` (base `budget_previsionnel`, identifiants
dans `docker-compose.yml` — usage local uniquement, à ne jamais réutiliser en
production).

### API

```bash
dotnet restore
dotnet build
dotnet run --project src/BudgetPrevisionnel.Api
```

Swagger disponible sur `https://localhost:<port>/swagger` en environnement
Development (bouton "Authorize" pour tester les endpoints protégés avec un
token). Endpoint de santé : `GET /health`.

La chaîne de connexion de développement est dans
`src/BudgetPrevisionnel.Api/appsettings.Development.json`. Ne jamais y mettre
de vraies informations sensibles ; pour un déploiement réel, utiliser une
variable d'environnement ou un secret manager.

### Authentification

L'Api exige une clé de signature JWT (`Jwt:Key`) qui n'est **jamais** commitée,
même en dev. À faire une fois après avoir cloné le repo :

```bash
dotnet user-secrets set "Jwt:Key" "<une longue chaîne aléatoire>" --project src/BudgetPrevisionnel.Api
```

Sans ça, `Program.cs` refuse de démarrer (message explicite). En production,
passer par une variable d'environnement (`Jwt__Key`) ou un secret manager —
jamais dans un fichier suivi par git.

Endpoints disponibles :
- `POST /api/auth/register` — `{ "email": "...", "password": "..." }` (min. 8
  caractères), retourne un token.
- `POST /api/auth/login` — mêmes champs, retourne un token.
- `GET /api/auth/me` — protégé (`Authorization: Bearer <token>`), retourne
  l'utilisateur courant.

Le hachage des mots de passe utilise `PasswordHasher<T>` d'ASP.NET Core
Identity (PBKDF2) sans le reste de la stack Identity (`UserManager`,
`SignInManager`, rôles) — disproportionné pour un usage familial et
incompatible avec un `Domain.User` sans dépendance externe. Détails dans
[`docs/roadmap.md`](docs/roadmap.md#lot-2--authentification-fait).

### Tests

```bash
dotnet test
```

Docker doit tourner (Testcontainers y démarre des instances Postgres
éphémères pour `Infrastructure.Tests` et `Api.Tests` — ce dernier boote
l'Api réelle via `WebApplicationFactory`). Ces tests n'ont besoin d'aucune
base déjà démarrée ni d'aucun `dotnet user-secrets` configuré au préalable :
`CustomWebApplicationFactory` fournit ses propres connexion et clé JWT via
variables d'environnement, isolées de la config locale de développement.

### Migrations EF Core

La migration `InitialCreate` (schéma complet + seed des 10 catégories système)
existe déjà. Pour l'appliquer sur votre Postgres local (après `docker compose up -d`) :

```bash
dotnet ef database update \
  --project src/BudgetPrevisionnel.Infrastructure \
  --startup-project src/BudgetPrevisionnel.Api
```

Pour générer une nouvelle migration après avoir modifié une entité :

```bash
dotnet ef migrations add <NomDeLaMigration> \
  --project src/BudgetPrevisionnel.Infrastructure \
  --startup-project src/BudgetPrevisionnel.Api \
  --output-dir Persistence/Migrations
```

(Nécessite l'outil `dotnet-ef` : `dotnet tool install --global dotnet-ef`.)
La génération de migration passe par `BudgetDbContextFactory` et ne dépend pas
de la configuration de l'Api ; elle utilise par défaut les identifiants de
`docker-compose.yml`, surchargeables via la variable d'environnement
`BUDGET_PREVISIONNEL_CONNECTION_STRING`.

Les tests d'intégration Postgres (`BudgetDbContextPostgresTests`) démarrent un
conteneur éphémère via Testcontainers — Docker doit tourner pour `dotnet test`,
mais aucune base persistante n'est requise.

## Frontend

SPA dans `frontend/` (Vite + React 19 + TypeScript). Détails de conception :
[`docs/roadmap.md`](docs/roadmap.md#lot-10--setup-reactts-fait).

### Prérequis
- Node.js 20+ (le template Vite 8 / TS 6 utilisé ici vise les runtimes récents).
- L'Api backend doit tourner (voir plus haut) pour que l'auth fonctionne —
  aucun mock n'est utilisé en dev.

### Installation et lancement

```bash
cd frontend
npm install
npm run dev
```

Sert l'app sur `http://localhost:5173`. L'origine `:5173` est déjà autorisée
côté Api en environnement Development (`Cors:AllowedOrigins` dans
`appsettings.Development.json`) — aucune configuration CORS supplémentaire
n'est nécessaire en local.

### Variables d'environnement

`VITE_API_BASE_URL` pointe vers l'Api (`.env.development` : déjà réglé sur
`http://localhost:5080`). `.env.production` contient un placeholder à
remplacer avant tout build de production réel (voir Lot 16, déploiement) —
ne jamais y mettre une URL de développement par erreur.

### Authentification côté frontend

Le token JWT retourné par `POST /api/auth/login|register` est stocké dans
`localStorage` (`budget-previsionnel.auth`) — pas de cookie httpOnly, arbitrage
assumé pour une SPA sans backend de sessions (voir roadmap). `useAuth()`
(`src/auth/authContext.ts`) expose `login`/`register`/`logout`/`token`/`user`.
`useApiClient()` (`src/auth/useApiClient.ts`) est le point d'entrée à utiliser
pour tout appel API authentifié à partir du Lot 11 (TanStack Query) : il
attache le token courant et déconnecte automatiquement l'utilisateur sur une
réponse 401 (token expiré ou invalide) plutôt que de laisser un écran bloqué.

`ProtectedRoute`/`PublicOnlyRoute` (`src/auth/ProtectedRoute.tsx`) gardent les
routes selon l'état de connexion.

### Comptes bancaires & import CSV

Écran `/accounts` (protégé) : liste, création, édition et suppression des comptes
bancaires, plus import d'un relevé CSV par compte en deux temps : "Aperçu" appelle
`POST .../import/preview` (multipart) et ouvre une modale listant les lignes
détectées (catégorie déjà proposée, éditable), chacune décochable ; "Confirmer
l'import" envoie uniquement les lignes gardées à `POST .../import/commit`. Rien
n'est enregistré tant que cette confirmation n'a pas eu lieu. Le sélecteur de
banque à la création est limité à `BoursoBank` — seul parseur
(`IBankStatementParser`) enregistré côté backend pour l'instant ; un import sur
une autre valeur échouerait en 400. Voir
[`docs/roadmap.md`](docs/roadmap.md#lot-11--écran-comptes--import-csv-fait) et
[`docs/roadmap.md`](docs/roadmap.md#revue-avant-import-fait).

### Transactions & règles de catégorisation

Écran `/transactions` (protégé) : liste paginée avec filtres (compte, catégorie,
période — dont deux raccourcis "Ce mois-ci"/"3 derniers mois", recherche débouncée,
exclusion des virements internes — mêmes paramètres que `GET /api/transactions`),
recatégorisation manuelle par ligne, une note personnelle libre par transaction, un
raccourci "+ Récurrente" par ligne de dépense (crée une `RecurringExpense` pré-remplie
depuis la transaction — libellé, montant, catégorie, date — sans lien conservé ensuite,
voir [`docs/roadmap.md`](docs/roadmap.md#raccourci-marquer-comme-récurrente-sur-les-transactions-fait)),
et une section de gestion des `CategoryRule` (motif → catégorie → priorité) juste en
dessous. Voir [`docs/roadmap.md`](docs/roadmap.md#lot-12--écran-transactions-fait).

### Dashboard

Écran `/` (protégé, ex-`HomePage`) : trois graphiques (évolution du solde cumulé,
répartition des dépenses par catégorie, comparatif mensuel revenus/dépenses) construits
en SVG/HTML fait main dans `src/components/charts/` — aucune librairie de charting
ajoutée. Filtres communs (compte, période, solde de départ optionnel) pilotant les trois
graphiques à la fois. Voir [`docs/roadmap.md`](docs/roadmap.md#lot-13--dashboard-fait)
pour le détail des choix de forme/couleur et la formule de dérivation des tuiles KPI
(`Revenus = Variation du solde + Dépenses`, affichée seulement en vue consolidée).

### Épargne, crédits & prévisionnel

Trois écrans consommant des ressources qui avaient déjà tout leur CRUD côté API depuis
les lots 6-7 mais aucun écran jusqu'ici :
- `/savings` : objectifs d'épargne (`SavingsGoal`) — CRUD, barre de progression vers le
  montant visé, compte lié optionnel.
- `/loans` : crédits (`Loan`) — CRUD, barre de progression du remboursement, taux et
  mensualité.
- `/forecast` : prévisionnel combiné (`GET /api/forecast/monthly|annual`), vue mensuelle
  (détail par catégorie avec un badge Budget/Récurrent, remboursements de crédits, total),
  annuelle (total par mois), ou **Calendrier** (`GET /api/calendar/monthly|annual`) — une
  grille mensuelle (semaine du lundi, navigation ‹/›) ou une liste annuelle groupée par
  mois des échéances datées d'abonnements et de crédits, avec un repère "(dernière)" sur
  le dernier paiement avant une `EndDate`. Sous ces vues, deux sections de gestion sans
  route propre : **Budget du mois** (`Budget`, seulement en vue mensuelle — le formulaire
  d'ajout exclut les catégories déjà budgétées pour éviter un 409 prévisible) et
  **Dépenses récurrentes** (`RecurringExpense`, toujours visible).

Voir [`docs/roadmap.md`](docs/roadmap.md#lot-14--épargne--crédits--prévisionnel-fait)
pour le détail (règle de préséance Budget > RecurringExpense côté prévisionnel, etc.).

### PWA

`vite-plugin-pwa` (stratégie `generateSW`) génère un manifest et un service
worker qui ne précache que les icônes et le manifest (`globPatterns: []`
volontaire) — installabilité seulement, aucun mode hors ligne fonctionnel
(décision explicite du brief). `favicon.png` (onglet navigateur),
`apple-touch-icon.png` (iOS "Ajouter à l'écran d'accueil"), `pwa-192x192.png`/
`pwa-512x512.png` (icônes standard) et `maskable-icon-512x512.png` (plein
cadre, logo dans la "safe zone" à 80 % — voir la spec W3C sur les icônes
maskable) sont tous dérivés du même symbole € sur fond `#C6A15B` (couleur
d'accent de la DA, voir plus bas — régénérées après le Lot 15 qui les avait
d'abord faites en bleu). Voir
[`docs/roadmap.md`](docs/roadmap.md#lot-15--finalisation-pwa-fait) pour le
détail de la construction des icônes. Pas d'outil Lighthouse ici : audit
d'installabilité réel et test "Ajouter à l'écran d'accueil" à faire
manuellement.

### Direction artistique

Sombre par défaut, avec une variante claire (bouton "Clair"/"Sombre" dans la
sidebar, ou préférence système par défaut) : fond quasi-noir chaud en sombre /
presque blanc chaud en clair, cartes/sidebar légèrement contrastées avec le
fond, texte en trois niveaux (`text-heading`/`text-body`/`text-muted`), accent
laiton (`#C6A15B` en sombre, une nuance plus soutenue en clair pour rester
lisible en texte), titres en **Space Grotesk**, corps de texte en **IBM Plex
Sans** (Google Fonts). Conventions de couleur métier : `positive`/`negative`
(vert/rouge) pour les montants, `info` (bleu) réservé au badge "Récurrent" du
prévisionnel pour ne pas se confondre avec l'accent laiton. Tous les tokens
sont définis une seule fois dans `src/index.css` (`@theme` pour les valeurs
sombres par défaut, puis une redéfinition des mêmes variables sous
`@media (prefers-color-scheme: light)` et `[data-theme='light']`) — jamais de
couleur Tailwind brute (`gray-900`, `sky-600`, ...) ni de variante `dark:`
dans les composants, toujours `bg-surface`/`text-accent`/etc. Voir
[`docs/roadmap.md`](docs/roadmap.md#refonte-de-la-direction-artistique) et
[`docs/roadmap.md`](docs/roadmap.md#mode-clairsombre-fait) pour le détail des
choix.

### Tests et qualité

```bash
npm run test     # Vitest + React Testing Library
npm run lint      # oxlint
npx tsc -b        # vérification des types
npm run format    # Prettier
```

### Build de production

```bash
npm run build
```

Génère `dist/` (`tsc -b && vite build`). Nécessite `VITE_API_BASE_URL` réglée
sur la vraie URL de l'Api en production (`.env.production`) — voir Lot 16
pour le déploiement effectif.
