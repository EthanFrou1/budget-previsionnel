# Roadmap de développement

Découpage en lots, backend d'abord puis frontend. Chaque lot est pensé pour être
livrable et testable indépendamment — on ne démarre pas un lot avant que le
précédent compile, teste et tourne.

Ce document est mis à jour au fil de l'avancement (statut, ajustements de
périmètre). Les décisions de conception amont sont dans
[`brief-projet-budget-previsionnel.md`](../brief-projet-budget-previsionnel.md) à la
racine du repo et ne sont pas remises en cause ici sauf blocage technique réel.

## Statut

| Lot | Contenu | Statut |
|---|---|---|
| 0 | Squelette solution .NET, EF Core wiring, conventions | ✅ Fait |
| 1 | Domaine & persistance : migrations, seed catégories système | ✅ Fait |
| 2 | Auth (hachage + JWT maison) | ✅ Fait |
| 3 | Import bancaire (`BoursoBankCsvParser`, pipeline, déduplication) | ✅ Fait |
| 4 | Catégories : CRUD, `CategoryRule`, mapping non exact | ✅ Fait |
| 5 | Comptes bancaires & transactions (CRUD complet, vues consolidée/par compte) | ✅ Fait |
| 6 | Épargne, crédits, dépenses récurrentes | ✅ Fait |
| 7 | Prévisionnel : calcul planifié vs réel, moteur de projection | ✅ Fait |
| 8 | Endpoints d'agrégation dashboard | ✅ Fait |
| 9 | Durcissement : validation, erreurs, logs, tests d'intégration | ✅ Fait |
| 10 | Setup React/TS (Vite) + PWA scaffold, auth flow | ✅ Fait |
| 11 | Écran comptes & import CSV | ✅ Fait |
| 12 | Écran transactions | ✅ Fait |
| 13 | Dashboard (graphiques) | ✅ Fait |
| 14 | Écrans épargne / crédits / prévisionnel | ✅ Fait |
| 15 | Finalisation PWA (manifest + service worker installable) | ✅ Fait |
| 16 | Déploiement (VPS/Railway, CI/CD) | À faire |

## Détail des lots backend

### Lot 0 — Squelette (fait)
- Solution 4 projets (`Domain`, `Application`, `Infrastructure`, `Api`) + 3 projets
  de tests xUnit, Central Package Management, `Directory.Build.props`.
- Entités du domaine (première version, cf. brief) et `BudgetDbContext` avec
  configurations Fluent API par entité.
- Contrat `IBankStatementParser` + modèle pivot `ParsedBankTransaction`.
- `docker-compose.yml` pour PostgreSQL en local, Swagger, endpoint `/health`.

### Lot 1 — Domaine & persistance (fait)
- `BudgetDbContextFactory` (`IDesignTimeDbContextFactory`) pour générer les
  migrations indépendamment de la configuration de l'Api.
- Migration `InitialCreate` (`dotnet ef migrations add`), validée par
  application réelle sur un Postgres local (`dotnet ef database update`).
- Seed des 10 catégories système (Alimentation, Logement, Transport,
  Abonnements & téléphonie, Loisirs, Santé, Revenus, Épargne, Impôts & taxes,
  Autres) via `HasData`, avec Id fixes — toute catégorie système future doit
  être ajoutée en fin de liste, jamais renumérotée.
- Tests d'intégration sur une vraie instance Postgres éphémère
  (`Testcontainers.PostgreSql`) : la migration s'applique proprement, le seed
  est présent, et les contraintes d'unicité (email, `Budget` par
  utilisateur/mois/catégorie) sont réellement appliquées par la base — pas
  seulement déclarées dans le modèle EF.

### Lot 2 — Authentification (fait)
- **Écart assumé par rapport à l'intitulé initial** : pas la stack complète
  ASP.NET Core Identity (`UserManager`/`SignInManager`/rôles/`IdentityDbContext`)
  — disproportionnée pour quelques comptes familiaux et incompatible avec un
  `Domain.User` sans dépendance externe. À la place : `PasswordHasher<T>`
  (`Microsoft.Extensions.Identity.Core`, PBKDF2) pour le hash, JWT signés à la
  main (`System.IdentityModel.Tokens.Jwt`). C'est un vrai sous-ensemble
  d'ASP.NET Core Identity (même composant de hachage), pas une réinvention.
- `AuthService` (Application) orchestre `IUserRepository` / `IPasswordHasher` /
  `IJwtTokenGenerator` — mêmes abstractions que le pattern `IBankStatementParser`
  du Lot 0 (contrat en Application, implémentation en Infrastructure).
- Endpoints `POST /api/auth/register`, `POST /api/auth/login`,
  `GET /api/auth/me` (protégé, prouve le pipeline bout en bout).
- `ICurrentUser` (Application) / `CurrentUser` (Api, lit les claims JWT) : le
  mécanisme que les contrôleurs de ressources (Lot 4+) utiliseront pour scoper
  chaque requête par `UserId`.
- Clé de signature JWT en `dotnet user-secrets` (jamais commitée, jamais dans
  `appsettings.*.json`) ; `Program.cs` refuse de démarrer si elle est absente.
- Testé en conditions réelles : inscription, doublon d'email (409), mot de
  passe trop court (400), connexion, mauvais mot de passe (401), accès à
  `/me` avec et sans token.

### Lot 3 — Import bancaire (fait)
- **Format validé contre un vrai relevé BoursoBank** (fourni par l'utilisateur,
  gardé hors repo — voir plus bas). Deux découvertes qui n'étaient pas
  devinables depuis le brief seul :
  - L'en-tête contient littéralement deux colonnes nommées `Solde` (le montant
    de la transaction *et* le vrai solde courant) → `BoursoBankCsvParser` lit
    par **position de colonne fixe**, jamais par nom.
  - Les dates sont au format `yyyy-MM-dd` (pas `dd/MM/yyyy`), et c'est
    `Catégorie Parente` (pas `Catégorie`) qui recoupe nos catégories système.
    `Non catégorisé` est normalisé en `null`, pas traité comme un vrai nom de
    catégorie.
- **Aucune donnée personnelle commitée** : le fichier réel (numéro de compte,
  noms de personnes ayant fait des virements) a servi à valider le parseur en
  local puis a été jeté ; les tests versionnés utilisent une fixture
  synthétique reproduisant les mêmes particularités de format.
- `TransactionDeduplicator` : les exports CSV n'ont pas d'id stable, donc la
  déduplication se fait sur une empreinte (Date, Libellé brut, Montant) — mais
  compare des **compteurs d'occurrences** plutôt que de bannir toute empreinte
  déjà vue, pour ne pas fusionner à tort deux achats identiques le même jour
  (cas réel observé : deux prélèvements Riot Games à des montants différents
  le même jour, mais le même libellé pourrait aussi se répéter à montant égal).
- `InternalTransferMatcher` : heuristique montant opposé ± 3 jours entre
  comptes du même utilisateur ; les cas ambigus (plusieurs candidats) restent
  volontairement non flagués plutôt que de deviner (un flag erroné exclurait
  à tort une vraie dépense/recette du dashboard consolidé).
- **Résolution d'une incohérence de périmètre entre Lot 3 et Lot 4** (les deux
  mentionnaient le mapping de catégorie) : le Lot 3 résout uniquement les
  correspondances **exactes** entre `Catégorie Parente` et le nom d'une
  catégorie système (ex. "Logement", "Abonnements & téléphonie" collent
  directement). Le Lot 4 gère les catégories personnalisées, le moteur
  `CategoryRule`, et le mapping approximatif pour les catégories BoursoBank
  qui ne collent pas exactement (ex. "Auto & Moto", "Vie quotidienne").
- Endpoints minimaux `POST /api/bank-accounts` et `GET /api/bank-accounts`
  (juste de quoi créer un compte et importer dedans) — anticipés depuis le
  Lot 5, qui complètera avec update/delete/vues détaillées.
- `POST /api/bank-accounts/{id}/import` (upload multipart), protégé, scope
  par utilisateur.
- Testé en conditions réelles : import → déduplication sur réimport du même
  fichier → import d'un second compte avec virement correspondant → vérifié
  en base (catégorisation correcte, `IsInternalTransfer` sur les deux
  transactions).

### Lot 4 — Catégories (fait)
- `CategoryService` (Application) porte les règles métier, pas les contrôleurs :
  - Catégories système en lecture seule (403 sur update/delete, même pour un
    admin — aucune notion d'admin d'ailleurs, juste "système" vs "perso").
  - `ParentCategoryId` accepté seulement si visible par l'utilisateur (système
    ou sa propre catégorie perso) — jamais la catégorie perso d'un autre
    utilisateur, dont l'existence n'est d'ailleurs pas révélée (même exception
    `CategoryNotFoundException` que "n'existe pas").
  - Prévention de cycle dans la hiérarchie (remonte la chaîne `ParentCategoryId`
    avant d'accepter un nouveau parent).
  - Suppression bloquée (409) si la catégorie a des sous-catégories ; les
    `Budget` associés cascadent (décision Lot 0 : un montant planifié n'a plus
    de sens sans sa catégorie), les `Transaction`/`RecurringExpense` perdent
    juste la référence (`SetNull`).
- `CategoryRuleMatcher` (Application, logique pure) : teste chaque règle de
  l'utilisateur (triées par `Priority` croissante) contre le libellé brut et
  le libellé nettoyé, s'arrête à la première correspondance.
- **Intégré dans le pipeline d'import du Lot 3** (`BankStatementImportService`)
  comme filet de sécurité : le mapping exact catégorie bancaire → catégorie
  système reste prioritaire (Lot 3), les règles de l'utilisateur ne
  s'appliquent que si ce mapping exact échoue.
- Endpoints `GET/POST/PUT/DELETE /api/categories`,
  `GET/POST/DELETE /api/category-rules`.
- Testé en conditions réelles : catégorie système protégée (403 update/delete),
  sous-catégorie personnalisée créée sous une catégorie système, cycle refusé
  (400), suppression bloquée tant qu'il reste une sous-catégorie (409), et
  surtout — une transaction avec une catégorie bancaire sans correspondance
  exacte ("Auto & Moto") correctement catégorisée via une règle utilisateur
  pendant un import réel, vérifié en base.

### Lot 5 — Comptes & transactions (fait)
- CRUD `BankAccount` complété (`PUT`/`DELETE`) via `BankAccountService`, extrait
  du contrôleur pour rester cohérent avec le reste du code (Auth/Category/
  BankImport ont chacun leur service Application).
- **Refactor mineur** : `BankAccountNotFoundException` vivait dans
  `BankImport` depuis le Lot 3 alors qu'elle est utilisée par trois features
  maintenant (import, comptes, transactions) — déplacée vers `BankAccounts`,
  son namespace naturel.
- `GET /api/transactions` : un seul endpoint sert les deux vues du brief —
  vue consolidée (pas de `bankAccountId`, toutes les transactions de
  l'utilisateur tous comptes confondus) ou vue par compte (`bankAccountId`
  fourni). Filtres additionnels : `categoryId`, `fromDate`/`toDate`,
  `search` (`ILike` Postgres, insensible à la casse, teste libellé brut et
  nettoyé), `excludeInternalTransfers`. Pagination (`page`/`pageSize`,
  bornée à 200) — anticipée dès maintenant plutôt que d'y revenir plus tard,
  un historique de transactions personnel grossit vite sur plusieurs années.
- `excludeInternalTransfers` est le mécanisme qui répond à la préoccupation
  du brief sur le double comptage des virements internes dans la vue
  consolidée — mais le calcul d'agrégats (sommes, totaux) proprement dit
  reste le rôle du Lot 8 ; ce lot expose juste le flag `IsInternalTransfer`
  et le filtre, il ne calcule aucun total.
- Testé en conditions réelles contre Postgres (pas seulement les fakes en
  mémoire, pour valider `EF.Functions.ILike` qui est spécifique à Npgsql) :
  recherche insensible à la casse, filtre par période, pagination stable sur
  plusieurs pages, filtre par catégorie, suppression de compte en cascade
  sur ses transactions.

### Point ouvert identifié pendant ce lot
Le vrai export BoursoBank contient une colonne solde courant (`Solde`, index
10) que le Lot 3 lit sans la persister. Un `BankAccount.CurrentBalance` mis à
jour à chaque import donnerait un vrai solde affichable au lieu de devoir le
recalculer (peu fiable si l'historique importé est partiel). Pas fait ici —
sortait du périmètre annoncé de ce lot — mais à réévaluer avant le Lot 8
(dashboard) si l'affichage du solde s'avère nécessaire.

### Lot 6 — Épargne, crédits, dépenses récurrentes (fait)
- Trois paires repository/service symétriques (`SavingsGoalService`,
  `LoanService`, `RecurringExpenseService`), volontairement dupliquées plutôt
  que généralisées derrière un `IRepository<T>` — même raisonnement que la
  décision du Lot 0 contre un repository générique : trois interfaces
  explicites de 5 méthodes chacune restent plus faciles à suivre pour un
  futur contributeur qu'une abstraction générique.
- Règle explicitement demandée par la roadmap validée en conditions réelles :
  `Loan.RemainingAmount` ne peut pas dépasser `Loan.PrincipalAmount` (créer ou
  modifier un prêt qui violerait ça renvoie 400 avec un message clair).
  Autres règles : montants strictement positifs sauf `InterestRate` (0%
  valide) et `RecurringExpense.EndDate` (autorisé égal à `StartDate`).
- **Convention de signe** : `RecurringExpense.Amount` est stocké en magnitude
  positive (comme `Loan.MonthlyPayment`), pas en négatif comme
  `Transaction.Amount` — le nom de l'entité encode déjà la direction ("Expense"),
  ce qui évite une source de confusion à la saisie (un utilisateur qui
  renseigne son loyer tape "800", pas "-800"). Le moteur de projection du
  Lot 7 devra en tenir compte lors de l'agrégation avec les `Transaction`.
- `LinkedAccountId` (SavingsGoal) et `CategoryId` (RecurringExpense) vérifiés
  comme appartenant à l'utilisateur (ou catégorie système) avant acceptation
  — même schéma que la vérification `ParentCategoryId` du Lot 4.
- Première utilisation d'un enum exposé par l'API (`RecurrenceFrequency`) :
  configuré pour sérialiser en chaîne (`"Monthly"`) plutôt qu'en entier — plus
  lisible et insensible à un futur réordonnancement de l'enum. Convention à
  appliquer à tout futur enum exposé.
- Testé en conditions réelles : création/modification/suppression des trois
  ressources, rejet 400 sur `RemainingAmount > PrincipalAmount` et sur
  `EndDate < StartDate`, catégorie liée correctement chargée dans la réponse
  (bug détecté et corrigé pendant ce lot : le repository ne faisait pas
  `Include(Category)`, donc `CategoryName` restait `null` même quand
  `CategoryId` était renseigné), compte lié à un objectif d'épargne.

### Lot 6 — Épargne, crédits, dépenses récurrentes
- CRUD `SavingsGoal`, `Loan`, `RecurringExpense`.
- Règles de validation métier (ex : `RemainingAmount <= PrincipalAmount`).

### Lot 7 — Prévisionnel (fait)
- `BudgetService` : CRUD sur `Budget` + calcul de `ActualAmount` à la volée
  (`GetNetAmountAsync` sur `ITransactionRepository`, jamais stocké — cf.
  décision `Budget.cs` du Lot 0). Convention de signe : `PlannedAmount` est en
  magnitude positive (comme `Loan`/`RecurringExpense` depuis le Lot 6),
  `ActualAmount` est l'opposé de la somme signée des transactions de la
  catégorie/mois (une dépense négative devient un montant "dépensé" positif,
  directement comparable au planifié). Les virements internes sont exclus du
  calcul, même raison que `TransactionQuery.ExcludeInternalTransfers`.
- Contrainte d'unicité (`UserId`, `Month`, `CategoryId`) du Lot 0 pré-vérifiée
  côté service (`DuplicateBudgetException`, 409) plutôt que de laisser
  remonter l'erreur SQL brute.
- **Moteur de projection** (`ForecastService`), règle de précédence
  explicite : une catégorie avec un `Budget` pour le mois utilise ce montant
  (le plan délibéré de l'utilisateur prime) ; `RecurringExpense` ne comble que
  les catégories sans `Budget` explicite pour ce mois-là — sinon un loyer
  récurrent et son budget Logement se cumuleraient à tort. Les crédits n'ont
  pas de `CategoryId` dans le modèle de domaine, donc `Loan.MonthlyPayment`
  reste une ligne à part (`LoanPayments`), pas une ligne de catégorie.
- **Comptage précis des occurrences hebdomadaires** (`RecurringExpenseProjector`) :
  une dépense récurrente `Weekly` n'est pas approximée à "montant × 4,33
  semaines/mois" — le nombre exact d'occurrences du jour de semaine de
  `StartDate` dans le mois ciblé est calculé (4 ou 5 selon les mois, borné par
  `StartDate`/`EndDate`). Une approximation aurait sous/sur-estimé le
  prévisionnel de façon visible sur certains mois ; vérifié aussi bien par
  tests unitaires (11 cas, dates calculées à la main) qu'en conditions
  réelles sur une année complète (le montant alterne correctement entre 200€
  et 250€ selon les mois pour une dépense hebdomadaire de test).
- `GET /api/forecast/monthly?month=` et `GET /api/forecast/annual?year=`
  (douze appels du calcul mensuel, pas de logique dupliquée).
- Testé en conditions réelles : création/doublon(409)/modification de
  budget, `ActualAmount` calculé depuis un import réel, précédence
  Budget > RecurringExpense vérifiée sur l'API qui tourne, prévisionnel
  annuel complet sur 2026 avec crédit actif et dépense hebdomadaire.

### Lot 8 — Agrégation dashboard (fait)
- **Décision sur "évolution du solde"** (point laissé ouvert au Lot 5) : pas de
  retour en arrière sur le pipeline d'import pour persister le vrai solde
  bancaire. `GET /api/dashboard/balance-evolution` calcule un **flux net
  cumulé** (somme courante des montants de transaction) avec un solde de
  départ optionnel (`startingBalance`, défaut 0) fourni par l'appelant —
  honnête sur ce que c'est (une évolution relative, pas le vrai solde
  bancaire), sans changement de schéma. Documenté explicitement dans le
  contrat `BalancePoint`/`BalancePointResponse` pour qu'un futur contributeur
  ne le confonde pas avec un vrai solde.
- Trois endpoints, même convention `bankAccountId` que `/api/transactions`
  (absent = vue consolidée tous comptes, présent = vue par compte) :
  `GET /api/dashboard/balance-evolution`, `/category-breakdown`,
  `/monthly-comparison`.
- **Traitement différencié des virements internes selon la vue** :
  - `balance-evolution` : inclus en vue par compte (un virement change
    vraiment le solde de CE compte), exclus en vue consolidée (sinon
    fausserait la vision multi-comptes — même logique que
    `TransactionQuery.ExcludeInternalTransfers`).
  - `category-breakdown` et `monthly-comparison` : toujours exclus, quelle
    que soit la vue — un virement vers son propre livret n'est ni une vraie
    dépense ni un vrai revenu, peu importe l'angle.
- `category-breakdown` ne compte que les transactions négatives (dépenses),
  en magnitude positive ; les transactions sans catégorie forment un groupe
  `categoryId: null` plutôt que d'être silencieusement ignorées.
- `monthly-comparison` retourne toujours 12 mois (complétés à zéro si aucune
  transaction), même principe que `ForecastService.GetAnnualForecastAsync`
  au Lot 7.
- Regroupements effectués côté application (après matérialisation), pas via
  `GroupBy` traduit en SQL — même choix que `GetFingerprintCountsAsync`
  (Lot 3), pour éviter tout risque de traduction EF sur des clés composées.
- Testé en conditions réelles sur des données couvrant deux mois : flux
  cumulé correct, répartition par catégorie exacte (y compris le regroupement
  "Non catégorisé" via la jointure nullable sur `Category`, jamais exercée par
  les tests unitaires en mémoire), comparatif mensuel avec mois à zéro,
  400 si `fromDate`/`toDate` manquants, 404 sur un compte d'un autre
  utilisateur.

### Lot 9 — Durcissement (fait)
Le lot le plus transversal jusqu'ici : touche les 11 contrôleurs et une
vingtaine de types d'exceptions. Détails dans les commentaires du code
(`Application/Common/AppException.cs`, `Api/ErrorHandling/AppExceptionHandler.cs`,
`Api/Validation/ValidationActionFilter.cs`), résumé ici.

**Hiérarchie d'exceptions commune** — chaque exception métier hérite maintenant
de `NotFoundException` (404), `ValidationException` (400), `ConflictException`
(409), `ForbiddenException` (403) ou `UnauthorizedException` (401), définies
dans `Application/Common/AppException.cs`. Un unique `AppExceptionHandler`
(`IExceptionHandler`, .NET 8) traduit ça en `ProblemDetails`, ce qui a permis
de **supprimer tous les `try/catch` qui existaient dans chaque contrôleur**
depuis les Lots 2 à 8.
- Distinction volontaire entre `XNotFoundException` (ressource primaire de
  l'URL, ex. `PUT /api/categories/{id}`) et la nouvelle
  `InvalidReferenceException` (référence invalide *dans* la requête, ex.
  `parentCategoryId`, `linkedAccountId`, `categoryId` sur une règle/dépense
  récurrente/budget) — la première est un 404, la seconde un 400. Même
  exception réutilisée par plusieurs features plutôt qu'une par cas d'usage.
- `NoParserAvailableException` consolidée de 422 vers 400 (un statut de moins
  à retenir, pas de distinction utile pour cette API).

**FluentValidation** remplace les `[Required]` de DataAnnotations sur tous les
DTOs d'entrée (cohérence : un seul mécanisme de validation dans tout le code).
- `ValidationActionFilter` (fait main, ~40 lignes) plutôt qu'un package
  d'auto-validation tiers — l'intégration ASP.NET Core officielle de
  FluentValidation n'est plus maintenue par son équipe, qui recommande
  explicitement d'écrire ce filtre soi-même désormais.
- Les règles dupliquent parfois une vérification déjà faite en Service
  (ex. `RemainingAmount <= PrincipalAmount`) — assumé : un 400 rapide et bien
  formé côté DTO, le Service reste la source de vérité pour tout appelant qui
  ne passerait pas par cette API.
- `ValidatorOptions.Global.LanguageManager.Enabled = false` — **bug trouvé en
  testant réellement l'API** : FluentValidation localise ses messages selon la
  culture de l'OS serveur par défaut, ce qui donnait des messages en français
  sur cette machine et aurait donné un résultat différent (et imprévisible) en
  production selon la locale du serveur, incohérent avec le reste des messages
  d'erreur du code qui sont tous des chaînes anglaises fixes.

**Serilog** (logs structurés) : console + fichier journalier
(`logs/log-.txt`, 14 jours conservés), `UseSerilogRequestLogging()` pour une
ligne par requête.
- **Deux bugs de configuration trouvés en testant réellement l'API** (aucun
  des deux n'était détectable par les tests unitaires avec fakes) :
  1. `Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware` logue
     lui-même chaque exception en `[ERR]` avec pile complète *avant* de
     déléguer à `AppExceptionHandler` — doublon bruyant qui faisait ressembler
     un simple 404 à un bug applicatif. Réduit au silence via
     `Serilog:MinimumLevel:Override` dans `appsettings.json`.
  2. Ordre des middlewares : `UseSerilogRequestLogging()` doit être enregistré
     **avant** `UseExceptionHandler()` (pas après). Dans le sens inverse,
     une exception qui remonte à travers le middleware de logging avant
     d'atteindre le gestionnaire d'erreurs se voit loguée comme "responded
     500", alors que la vraie réponse envoyée au client est un 404/400/409
     correct — deux lignes de log contradictoires pour une seule requête.

**Tests d'intégration** (`tests/BudgetPrevisionnel.Api.Tests`,
`WebApplicationFactory<Program>` + Testcontainers Postgres, même approche que
Infrastructure.Tests) — **deux bugs sérieux trouvés en écrivant ces tests**,
qu'aucun test unitaire avec fakes n'aurait pu révéler :
1. `WebApplicationFactory.ConfigureWebHost`/`ConfigureAppConfiguration` ne
   fusionne pas ses valeurs de configuration avant que `Program.cs` (modèle
   d'hébergement minimal) lise `builder.Configuration` dans son propre code
   top-level. Résultat concret : les premiers tests d'intégration écrivaient
   silencieusement dans **la vraie base Postgres locale de développement**
   (celle de `docker-compose.yml`) avec **la vraie clé JWT** des user-secrets,
   au lieu du conteneur Postgres éphémère prévu — sans aucune erreur visible.
   17 utilisateurs de test ont fui dans la base locale avant d'être repérés
   (nettoyés). Corrigé en passant par des variables d'environnement
   (`ConnectionStrings__BudgetDatabase`, `Jwt__Key`), lues par
   `WebApplication.CreateBuilder` avec une priorité supérieure aux
   user-secrets — voir le commentaire dans `CustomWebApplicationFactory.cs`.
2. `UseHttpsRedirection()` provoque une redirection http→https sur le premier
   appel ; `HttpClient` supprime l'en-tête `Authorization` en suivant une
   redirection qui change de schéma, transformant silencieusement tout appel
   authentifié en appel anonyme (401). Corrigé en pointant les clients de test
   directement sur `https://localhost` (`CreateHttpsClient()`), qui n'a besoin
   d'aucun certificat réel puisque `TestServer` ne sort jamais du process.
- Périmètre couvert : flux auth complet (register/login/me, 409 doublon, 400
  validation), forme des erreurs (404/400 avec `ProblemDetails`), non-fuite
  d'existence d'une ressource d'un autre utilisateur, et un scénario croisé
  compte → import → recherche de transactions bout en bout.

## Détail des lots frontend

### Lot 10 — Setup React/TS (fait)
- **Stack** : Vite 8 + React 19 + TypeScript 6 (template officiel `react-ts`,
  très récent — voir point `erasableSyntaxOnly` plus bas), `oxlint` (linter en
  Rust fourni par le template, remplace ESLint), TanStack Query v5 pour l'état
  serveur, Context React pour l'état auth uniquement (pas de store global type
  Redux/Zustand — le périmètre ne le justifie pas), Tailwind CSS v4
  (`@tailwindcss/vite`, pas de config séparée), React Router v7, Vitest +
  React Testing Library. Mêmes raisons de fond que le choix .NET du Lot 0 :
  stack standard, pas de dépendance exotique à maintenir seul dans le temps.
- **CORS côté backend** (`Program.cs`, `appsettings*.json`) : origines
  autorisées lues depuis `Cors:AllowedOrigins` (config, pas codé en dur) —
  vide par défaut (refuse tout), `http://localhost:5173` uniquement en
  `appsettings.Development.json`. Pas de fallback permissif : tant que
  l'origine de prod n'est pas connue (Lot 16), l'API refuse toute origine hors
  dev plutôt que d'ouvrir largement "en attendant".
- **Client API** (`src/api/client.ts`) : une seule fonction `apiFetch<T>()`,
  pas de wrapper Axios — `fetch` suffit pour le volume d'appels de cette
  application. Parse les réponses d'erreur au format `ProblemDetails`/
  `ValidationProblemDetails` du Lot 9 pour reconstituer une `ApiError`
  typée (`status`, `title`, `errors` par champ) exploitable directement par
  les formulaires (ex. `RegisterPage` affiche le premier message de
  validation FluentValidation renvoyé par l'API).
- **Auth** (`src/auth/`) : JWT stocké en `localStorage`
  (`budget-previsionnel.auth`) plutôt qu'en cookie httpOnly — arbitrage
  assumé pour une SPA sans backend de sessions ; lectures/écritures protégées
  par try/catch (navigation privée, storage bloqué). `useApiClient()` est le
  point d'entrée prévu pour tous les appels authentifiés à partir du Lot 11
  (`queryFn`/`mutationFn` TanStack Query) : il déclenche une déconnexion
  automatique sur toute réponse 401, pour transformer un token expiré en
  redirection propre vers `/login` plutôt qu'un écran bloqué en erreur.
  `ProtectedRoute`/`PublicOnlyRoute` gardent les routes via `<Outlet />`.
- **Scission `authContext.ts` / `AuthProvider.tsx`** : `oxlint` (règle
  `react/only-export-components`) refuse qu'un même fichier exporte à la fois
  un composant et d'autres valeurs (casse le Fast Refresh de Vite). Le
  contexte, son type et le hook `useAuth()` vivent dans `authContext.ts`
  (aucun JSX) ; `AuthProvider.tsx` ne contient que le composant.
- **PWA** (`vite-plugin-pwa`, stratégie `generateSW`) : `globPatterns: []`
  volontaire — le service worker généré ne précache que les icônes et le
  manifest (5 entrées, vérifié dans `dist/sw.js` après build), jamais
  `index.html` ni le bundle JS/CSS. Ça suffit au critère d'installabilité PWA
  (manifest valide + service worker enregistré) sans donner de mode hors
  ligne fonctionnel, cohérent avec la décision explicite du brief de ne pas
  faire de mode offline pour cette v1.
- **`erasableSyntaxOnly` (TypeScript 6, nouveau défaut du template)** :
  interdit le sucre syntaxique des *parameter properties* dans les
  constructeurs (`constructor(public readonly x: number)`), qui génère du
  code JS supplémentaire au lieu de s'effacer à la compilation. Repéré sur
  `ApiError` (`src/api/client.ts`) — champs déclarés explicitement et assignés
  dans le corps du constructeur à la place. À garder en tête pour tout futur
  code TS écrit dans ce projet.
- Testé en conditions réelles (API + Postgres du `docker-compose.yml`,
  serveur de dev Vite lancés ensemble, pas seulement les tests unitaires) :
  préflight CORS `OPTIONS` correct depuis l'origine `:5173`, inscription
  (200, token renvoyé, en-tête `Access-Control-Allow-Origin` présent),
  connexion avec bon/mauvais mot de passe (200/401, en-tête CORS présent même
  sur l'erreur — condition nécessaire pour que `useApiClient` puisse lire le
  401 et déclencher la déconnexion automatique). Utilisateurs de test créés
  pendant cette vérification supprimés de la base locale immédiatement après
  (même discipline que la fuite détectée au Lot 9).
- 13 tests (Vitest) sur `apiFetch`/`ApiError`, `AuthProvider` (connexion,
  déconnexion, restauration de session depuis `localStorage`) et les gardes
  de route ; `tsc -b`, `oxlint` et `npm run build` passent sans avertissement.
- **Non fait dans ce lot, volontairement** : aucun écran métier (comptes,
  transactions, dashboard) — seulement `HomePage` comme preuve que le flux
  protégé fonctionne. Pas de validation via un vrai navigateur piloté
  (aucun outil d'automatisation navigateur disponible dans cet environnement) ;
  la validation s'est donc faite au niveau HTTP direct contre l'API réelle
  plutôt qu'en cliquant dans l'UI — à refaire manuellement par l'utilisateur
  avant de considérer le flux auth définitivement validé visuellement.

### Lot 11 — Écran comptes & import CSV (fait)
- Écran `/accounts` (`AccountsPage.tsx`) : liste des comptes (`GET /api/bank-accounts`
  via TanStack Query), création, édition inline, suppression, et import CSV par
  compte — quatre opérations sur trois entités (compte, formulaire, import), gardées
  dans un seul fichier avec des sous-composants (`CreateAccountForm`, `AccountCard`,
  `ImportCsvForm`) plutôt que trois fichiers séparés : chacun est trop petit pour
  justifier son propre module, et ils ne sont utilisés que par cet écran.
- **Écart volontaire par rapport à l'énoncé initial du lot** ("aperçu avant import,
  résolution des catégories ambiguës") : le backend (Lot 3/4) n'expose pas d'endpoint
  de prévisualisation — l'import est une opération atomique côté API (parse +
  déduplication + catégorisation + détection virement en une seule transaction), et
  la résolution des catégories ambiguës via `CategoryRule` est déjà un écran à part
  entière (Lot 12, gestion des catégories/règles). Ajouter un aperçu aurait exigé un
  nouvel endpoint backend hors périmètre annoncé pour ce lot ; le résumé post-import
  (`ImportSummaryResponse` : lignes lues/importées/doublons/virements détectés,
  déjà renvoyé depuis le Lot 3) donne un retour suffisant sans dupliquer la logique
  d'import côté frontend.
- **Sélecteur de banque limité à `["BoursoBank"]`** (constante `SUPPORTED_BANKS`) au
  lieu d'un champ texte libre : `BankStatementImportService` résout le parseur par
  correspondance exacte sur `BankAccount.BankName` (Lot 3) et ne connaît qu'un seul
  parseur enregistré aujourd'hui — un texte libre rendrait trivialement atteignable
  l'erreur 400 "No statement parser is registered for bank '...'" pour toute faute de
  frappe. À étendre en vrai `<select>` multi-options le jour où un second parseur
  (`IBankStatementParser`) sera ajouté.
- **Extension de `apiFetch`** (`src/api/client.ts`, jusqu'ici JSON uniquement depuis
  le Lot 10) pour accepter un `body: FormData` — nécessaire pour l'upload multipart
  du CSV. Ne pas fixer `Content-Type` soi-même sur du `FormData` : le navigateur doit
  générer l'en-tête avec le paramètre `boundary` du multipart, qu'on ne peut pas
  reproduire à la main.
- Testé en conditions réelles contre l'Api + Postgres du `docker-compose.yml` (pas
  seulement Vitest/RTL avec `apiFetch` mocké) : création de compte, ré-import du même
  CSV synthétique (0 nouvelle transaction, 2 doublons signalés — même comportement
  que la déduplication validée au Lot 3), modification, import échouant proprement en
  400 sur un compte dont la banque n'a pas de parseur, suppression avec vérification
  que le compte et son solde disparaissent bien de la liste. Utilisateur et comptes de
  test supprimés de la base locale immédiatement après (même discipline qu'aux lots
  précédents). Comme au Lot 10, aucune validation par clic réel dans un navigateur
  (aucun outil d'automatisation disponible dans cet environnement) — à refaire
  manuellement par l'utilisateur.
- 20 tests Vitest/RTL (`AccountsPage.test.tsx`) : liste, état vide, création,
  suppression (confirmée et annulée), import avec résumé affiché, affichage d'erreur
  API. `tsc -b`, `oxlint` et `npm run build` passent sans avertissement.

### Lot 12 — Écran transactions (fait)
- **Écart backend découvert pendant ce lot** : aucun endpoint n'existait pour changer
  manuellement la catégorie d'une transaction — `TransactionsController` (Lot 5) n'avait
  qu'un `GET`, et `CategoryId` n'est fixé qu'à l'import (`BankStatementImportService`,
  Lot 3/4). La "catégorisation manuelle" annoncée dans l'intitulé initial de ce lot était
  donc irréalisable côté frontend sans revenir sur le backend. Comme au Lot 3 (endpoints
  minimaux de comptes anticipés pour le Lot 5) et au Lot 5→8 (point ouvert sur le solde),
  jugé plus honnête de fermer ce trou immédiatement plutôt que de livrer un écran qui fait
  semblant : `PUT /api/transactions/{id}/category` (`{ "categoryId": number | null }`),
  ajouté à `TransactionsController`/`TransactionService`/`ITransactionRepository` en
  suivant exactement les conventions déjà en place (`TransactionNotFoundException`, même
  vérification "système ou à moi" que `CategoryRuleService`/`CategoryService` via
  `InvalidReferenceException`). `categoryId: null` désinscrit la transaction. 6 nouveaux
  tests `TransactionServiceTests` (catégorie valide, désincatégorisation, transaction
  introuvable, transaction d'un autre utilisateur, catégorie personnalisée d'un autre
  utilisateur) — 136 tests Application au total, tous verts.
- **Fixation explicite de la navigation EF, pas seulement du FK** : `UpdateCategoryAsync`
  assigne `transaction.Category = category` en plus de `transaction.CategoryId` avant
  `SaveChangesAsync`, plutôt que de compter sur le fixup automatique du change tracker EF
  Core. Ça garantit que `TransactionResponse.FromEntity(transaction).CategoryName` est
  correct dans la réponse retournée immédiatement après l'update, sans dépendre d'un détail
  d'implémentation d'EF potentiellement fragile. Validé en conditions réelles : la réponse
  HTTP du `PUT` contient bien le nouveau `categoryName` sans requête supplémentaire.
- Écran `/transactions` (`TransactionsPage.tsx`) : filtres (compte, catégorie, période,
  recherche, exclusion des virements internes — mêmes paramètres que
  `GET /api/transactions` du Lot 5), tableau paginé (montant coloré rouge/vert, badge
  virement interne, `<select>` de catégorie par ligne qui appelle le nouvel endpoint), et
  pagination Précédent/Suivant pilotée par `totalCount`/`pageSize` de la réponse — pas de
  logique de pagination dupliquée côté client.
- **Recherche débouncée (400 ms), pas les autres filtres** : taper dans le champ recherche
  ne déclenche une requête qu'après une pause de frappe (`setTimeout` réinitialisé à chaque
  frappe) ; les filtres select/date changent assez rarement pour resynchroniser la page à 1
  directement depuis leur `onChange`, sans effet dédié. Choix motivé par un avertissement
  `oxlint` (`react/set-state-in-effect`) sur un premier essai avec un `useEffect` générique
  qui remettait `page` à 1 à chaque changement de filtre — corrigé en déplaçant cette
  remise à zéro dans le code appelant (l'événement) plutôt que dans un effet qui ne fait
  que dériver un state depuis un autre.
- **Extraction de `AppHeader`** (`src/components/AppHeader.tsx`) : `HomePage` et
  `AccountsPage` dupliquaient le même en-tête (nav + email + déconnexion) depuis les Lots
  10/11 ; avec un troisième écran qui en a besoin, extraction plutôt que continuer à
  dupliquer (règle de trois).
- `CategoryRulesSection.tsx` (utilisé dans `TransactionsPage`, pas une route à part —
  une règle n'a de sens que dans le contexte des transactions qu'elle catégorisera) :
  liste des `CategoryRule` de l'utilisateur, création (motif, catégorie, priorité),
  suppression — CRUD déjà exposé par `CategoryRulesController` depuis le Lot 4, jamais
  consommé par un écran jusqu'ici.
- Testé en conditions réelles contre l'Api + Postgres du `docker-compose.yml` (en plus des
  158 tests `dotnet test`, Testcontainers inclus, tous verts) : import d'un CSV synthétique
  avec deux transactions non catégorisées, recherche par libellé, catégorisation manuelle
  (réponse immédiate avec le bon `categoryName`), désincatégorisation, 400 sur une catégorie
  inexistante, 404 sur une transaction inexistante et sur la transaction d'un autre
  utilisateur (testé avec un second compte), création/liste/suppression d'une
  `CategoryRule`. Comme aux lots précédents, aucun clic réel dans un navigateur (aucun
  outil d'automatisation disponible) — validation HTTP directe. Utilisateurs et comptes de
  test supprimés de la base ensuite.
- 12 tests Vitest/RTL supplémentaires (`TransactionsPage.test.tsx` : 7,
  `CategoryRulesSection.test.tsx` : 5) — 32 tests frontend au total. `tsc -b`, `oxlint` et
  `npm run build` passent sans avertissement.

### Lot 13 — Dashboard (fait)
- **Aucun changement backend** : les trois endpoints du Lot 8
  (`/api/dashboard/balance-evolution|category-breakdown|monthly-comparison`)
  couvraient déjà exactement ce dont l'écran avait besoin — contrairement au Lot 12, pas
  de trou à combler ici.
- **`HomePage` devient `DashboardPage`** (toujours la route `/`) : la page d'accueil
  n'était qu'un placeholder explicite depuis le Lot 10 ("le dashboard arrive au
  prochain lot") ; le lien de nav `AppHeader` passe de "Accueil" à "Dashboard".
- **Trois graphiques en SVG/HTML fait main, aucune librairie de charting ajoutée** —
  cohérent avec les arbitrages du projet contre les dépendances non justifiées (`fetch`
  au lieu d'Axios au Lot 10, pas de Redux/Zustand, etc.) : le volume de graphiques (3,
  formes simples) ne justifie pas une dépendance supplémentaire à maintenir. Construits
  en suivant la méthode du skill dataviz (choix de la forme avant la couleur, palette
  validée CVD-safe, légende dès 2 séries, tooltips qui n'empêchent jamais l'accès à la
  donnée) :
  - `BalanceEvolutionChart` : ligne + aire, **une seule teinte séquentielle** (bleu
    `#2a78d6`/`#3987e5` clair/sombre) — c'est une tendance dans le temps à une série,
    pas une comparaison d'identités. Axe x en vraie échelle temporelle (écart en jours
    réels entre points, pas un simple index ordinal) puisque les jours sans transaction
    n'apparaissent pas dans les `BalancePoint` renvoyés par le Lot 8. Curseur croisé au
    survol, étiquette directe sur le dernier point, bascule vers une vue tableau.
  - `CategoryBreakdownChart` : barres horizontales, **une seule teinte séquentielle**
    elle aussi (le nom de catégorie porte déjà l'identité, pas besoin de 8 couleurs
    catégorielles pour un classement de magnitude) — top 7 + repli "Autres (n)"
    au-delà (plafond de tokens documenté dans `choosing-a-form.md` du skill). Rendu en
    vrai `<table>` (pas une `<ul>` stylée) : les valeurs sont déjà en étiquette directe
    sur chaque ligne, donc pas besoin d'une bascule tableau séparée comme pour les deux
    autres graphiques.
  - `MonthlyComparisonChart` : barres divergentes autour d'une ligne zéro — revenus vers
    le haut, dépenses vers le bas (magnitude déjà positive côté `MonthlyComparisonEntry`,
    inversée seulement pour l'affichage) — **paire divergente bleu/rouge** validée par le
    skill (deux teintes qui se lisent comme opposées, jamais deux teintes froides). `Net`
    volontairement seulement dans l'infobulle au survol, pas une troisième série visuelle,
    pour ne pas surcharger le graphique.
- **Filtres communs à tout l'écran** (compte, préréglages de période "Ce mois-ci"/"3
  derniers mois"/"Cette année" + dates personnalisées, solde de départ optionnel) — un
  seul jeu de filtres qui pilote les trois graphiques à la fois, jamais un filtre par
  graphique (règle explicite du skill : "one row, above the charts").
- **KPI dérivés plutôt qu'un nouvel endpoint** : `Dépenses` (somme de
  `category-breakdown`) et `Variation du solde` (somme des `netChange` de
  `balance-evolution`) sont exacts dans les deux vues. `Revenus` est dérivé par identité
  comptable (`Revenus = Variation + Dépenses`, puisque Variation = Revenus - Dépenses)
  et n'est affiché **qu'en vue consolidée** : en vue par compte, `balance-evolution`
  inclut les virements internes (Lot 8, un virement change vraiment le solde du compte
  visualisé) alors que `category-breakdown` les exclut toujours — la même formule y
  compterait à tort un virement entrant comme un revenu. Plutôt que d'afficher un chiffre
  subtilement faux, la tuile "Revenus" est simplement masquée en vue par compte. Validé
  en conditions réelles : sur des données réelles importées (2 salaires de 2000€, 400€ de
  dépenses réparties sur 2 catégories), `Revenus = Variation (3600€) + Dépenses (400€) =
  4000€`, exactement les deux salaires.
- **Extraction de `src/utils/format.ts`** (`formatDateFr`, `formatCurrency`) : ces deux
  fonctions existaient déjà en local dans `TransactionsPage.tsx` (Lot 12) ; avec les
  graphiques qui en ont besoin aussi, extraction plutôt que troisième copie (même
  raisonnement que l'extraction d'`AppHeader` au Lot 12).
- Testé en conditions réelles contre l'Api + Postgres (import CSV synthétique sur deux
  mois, vue consolidée et par compte, 400 si `fromDate`/`toDate` manquants, 404 sur un
  compte d'un autre utilisateur) en plus des 37 tests Vitest/RTL (dont 5 nouveaux sur
  `DashboardPage`). `tsc -b`, `oxlint` et `npm run build` passent sans avertissement.
  Aucun changement côté `dotnet test` (backend non touché).
- **Non fait dans ce lot** : comme pour tous les écrans précédents, aucune vérification
  visuelle réelle dans un navigateur (aucun outil d'automatisation disponible) — le rendu
  SVG (collisions d'étiquettes, mise en page réelle des graphiques) n'a été vérifié qu'au
  niveau des données/texte via Vitest, pas visuellement. Le skill dataviz recommande
  explicitement d'ouvrir et regarder le résultat rendu avant de le considérer terminé :
  **à faire par l'utilisateur** en lançant `npm run dev` et en ouvrant `/`.

### Lot 14 — Épargne / crédits / prévisionnel (fait)
- **Aucun changement backend** : `SavingsGoal`, `Loan`, `RecurringExpense` et `Budget` avaient
  déjà tout leur CRUD côté API (Lots 6-7) et un `ForecastController` (mensuel/annuel) qui les
  combine — mais aucun écran frontend ne les consommait encore, exactement comme les
  `CategoryRule` avant le Lot 12.
- **Trois écrans** :
  - `/savings` (`SavingsPage`) — objectifs d'épargne : CRUD, barre de progression
    (`currentAmount`/`targetAmount`), échéance et compte lié optionnels.
  - `/loans` (`LoansPage`) — crédits : CRUD, barre de progression du remboursement
    (`principalAmount - remainingAmount`), taux et mensualité.
  - `/forecast` (`ForecastPage`) — bascule Mensuel/Annuel. En mensuel : tableau des lignes
    du prévisionnel avec un badge de source (Budget vs Récurrent), la ligne des
    remboursements de crédits, un total ; en annuel : total par mois avec un bouton "Détail"
    qui repasse en vue mensuelle sur le mois choisi. Sous les deux vues, deux sections de
    gestion qui n'avaient jamais eu d'écran non plus : **Budget du mois** (`BudgetSection`,
    seulement en vue mensuelle) et **Dépenses récurrentes** (`RecurringExpensesSection`,
    toujours visible, non liées à un mois). Gérées ici plutôt que sur leur propre route —
    elles ne prennent sens que dans le contexte du prévisionnel qu'elles alimentent, même
    raisonnement que `CategoryRulesSection` au Lot 12.
- **Le formulaire de création d'une ligne de Budget exclut les catégories déjà budgétées**
  pour le mois affiché : `BudgetService.CreateAsync` renvoie 409 (`DuplicateBudgetException`)
  sur un doublon (mois, catégorie) ; plutôt que de laisser cet échec atteignable, le
  `<select>` de catégorie est filtré côté client à partir des lignes déjà chargées pour ce
  mois (même logique que `SUPPORTED_BANKS` au Lot 11 pour la banque d'import).
- **Bug trouvé par les tests, pas par relecture** : `GoalFields`/`LoanFields` (formulaires
  partagés entre la création et chaque édition en ligne) utilisaient un `id`/`htmlFor`
  fixe ("goal-label", "loan-label", ...). Comme le formulaire de création reste toujours
  monté à côté de la carte en cours d'édition, ça produisait un vrai doublon d'`id` dans le
  DOM (invalide en HTML, et `<label>` peut alors s'associer au mauvais champ au clic) — un
  test qui éditait un objectif tapait en fait dans le champ du formulaire de création
  resté vide. Corrigé en donnant à `GoalFields`/`LoanFields` un `idPrefix` par instance
  (`"goal-create"` / `"goal-{id}"`, `"loan-create"` / `"loan-{id}"`). `RecurringExpenseForm`
  et les champs de `BudgetSection` n'ont pas ce problème : ils suivent déjà la convention
  `AccountCard` du Lot 11 (`aria-label` sans `id` sur les champs d'édition en ligne).
- **`formatPercent` et `formatMonthFr` ajoutés à `src/utils/format.ts`** : le taux d'intérêt
  d'un `Loan` et les libellés de mois du prévisionnel doivent suivre la même convention
  locale `fr-FR` que `formatCurrency`/`formatDateFr` (`2,5%` et non `2.5%`) ; `formatMonthFr`
  reprend la même précaution que `formatDateFr` (parser les parties plutôt que passer la
  chaîne ISO à `new Date`, pour éviter un décalage de mois selon le fuseau du navigateur).
- Testé en conditions réelles contre l'Api + Postgres réels (conteneur Postgres temporaire
  sur un port alternatif le temps de la session : le `5432` par défaut de
  `docker-compose.yml` était occupé par un autre projet local d'Ethan déjà démarré — noté
  ici pour la prochaine fois plutôt que de le considérer réglé) : création d'un objectif
  d'épargne et mise à jour de son montant courant, création d'un crédit, 400 sur un crédit
  dont `RemainingAmount` dépasse `PrincipalAmount`, création de deux dépenses récurrentes
  (une mensuelle catégorisée, une hebdomadaire), vérification du prévisionnel d'un mois
  sans Budget (les deux dépenses récurrentes apparaissent, projetées correctement dont le
  comptage précis des occurrences hebdomadaires du Lot 7), création d'une ligne de Budget
  sur la catégorie de la dépense hebdomadaire pour ce même mois puis re-vérification du
  prévisionnel (la ligne bascule de source Récurrent à Budget, exactement la règle de
  préséance de `ForecastService`, et seulement pour ce mois-là dans la vue annuelle — tous
  les autres mois restent en Récurrent), 409 sur une deuxième ligne de Budget pour la même
  catégorie/mois, 404 sur un crédit et un objectif d'épargne inexistants. Utilisateur et
  toutes les données de test supprimés avec la destruction du conteneur Postgres temporaire
  en fin de session. 27 tests Vitest/RTL supplémentaires (`SavingsPage` : 6, `LoansPage` :
  5, `RecurringExpensesSection` : 5, `BudgetSection` : 6, `ForecastPage` : 5) — 64 tests
  frontend au total. `tsc -b`, `oxlint` et `npm run build` passent sans avertissement.
  Aucun changement côté `dotnet test` (158 tests toujours verts, backend non touché).
- **Non fait dans ce lot** : comme pour tous les écrans précédents, aucune vérification
  visuelle réelle dans un navigateur (aucun outil d'automatisation disponible) — **à faire
  par l'utilisateur** en lançant `npm run dev` et en ouvrant `/savings`, `/loans` et
  `/forecast`.

### Lot 15 — Finalisation PWA (fait)
- **`favicon.svg` et `icons.svg` supprimés** : `favicon.svg` était le logo abstrait violet par
  défaut du template (jamais remplacé depuis le Lot 10) — sans rapport avec le symbole € bleu
  utilisé partout ailleurs (icônes PWA, thème). L'onglet du navigateur et l'icône d'installation
  affichaient donc deux identités visuelles différentes. `icons.svg` était un sprite
  bluesky/discord/github/x totalement inutilisé (`grep` ne trouve aucune référence) — cruft de
  template, supprimé sans remplacement.
- **Nouveau `favicon.png`** : le `pwa-512x512.png` existant (déjà correct) redimensionné en
  48×48, plutôt qu'un nouveau design — cohérence garantie avec l'icône d'installation puisque
  c'est littéralement la même image.
- **`maskable-icon-512x512.png` corrigé** : c'était un doublon octet-pour-octet de
  `pwa-512x512.png` (coins arrondis + fond transparent déjà appliqués), donc pas une vraie icône
  "maskable" au sens de la spec W3C — un masque circulaire ou squircle du système peut rogner ou
  laisser apparaître de la transparence aux coins. Régénérée en plein cadre (fond bleu
  `#0EA5E9` sur tout le canevas 512×512, aucune transparence) avec le glyphe € centré et
  dimensionné pour tenir dans la "safe zone" centrale à 80 % exigée par la spec, quel que soit le
  masque appliqué par l'OS.
- **`apple-touch-icon.png` ajouté** (180×180, plein cadre, même logique que le maskable) : iOS
  ignore complètement les icônes du manifest web pour "Ajouter à l'écran d'accueil", il lui faut
  son propre `<link rel="apple-touch-icon">` — absent jusqu'ici.
- Icônes générées avec Pillow (police Arial Bold pour le glyphe €, couleur de fond échantillonnée
  directement depuis `pwa-512x512.png` pour garantir un `#0EA5E9` exact plutôt que retapé à la
  main) — script jetable, pas conservé dans le repo.
- **Manifest complété** : `id: "/"` (recommandé pour que le navigateur associe correctement les
  mises à jour à l'app installée) et `lang: "fr"`. `scope` est ajouté automatiquement par
  `vite-plugin-pwa`.
- **Bug de responsive trouvé en relisant le Lot 14** : le tableau de `BudgetSection` était le
  seul des quatre tableaux de l'app sans conteneur `overflow-x-auto` (`TransactionsPage` et les
  deux vues de `ForecastPage` l'ont) — sur un écran étroit, ça aurait fait défiler toute la page
  horizontalement plutôt que juste le tableau. Corrigé. Les autres écrans (grilles de formulaire
  `sm:`/`lg:`, graphiques SVG en `viewBox` + `width="100%"`, `CategoryBreakdownChart` en cellules
  flexibles) ne présentent pas ce risque à la lecture du code.
- Validé structurellement (aucun outil Lighthouse/navigateur ici, donc pas d'audit
  d'installabilité réel) : `npm run build` copie bien les 5 fichiers d'icônes dans `dist/`,
  `manifest.webmanifest` généré contient tous les champs et référence des icônes qui existent
  réellement, `index.html` construit charge le service worker et le manifest. 64 tests
  Vitest/RTL toujours verts (aucun nouveau test — changements de config/assets, pas de
  composant), `tsc -b` et `oxlint` propres.
- **Non fait dans ce lot** : comme pour tous les lots précédents, aucune vérification visuelle
  réelle (aucun outil d'automatisation disponible) — **à faire par l'utilisateur** : audit
  Lighthouse PWA dans Chrome DevTools, test réel "Ajouter à l'écran d'accueil" sur iOS/Android,
  et un coup d'œil à `npm run dev` en largeur mobile (~375-400px) sur les six écrans.

### Refonte de la direction artistique (fait)
- **Demande d'Ethan** : sortir de la palette Tailwind par défaut (gray/sky) pour une DA "à nous",
  sombre et classe, sans idée précise de départ — "je te laisse tester pour voir".
- **Exploration sur un canvas de maquettes** (skill `design`) : trois pistes comparées côte à
  côte sur un mini-dashboard fidèle à la vraie mise en page (sidebar, KPI, graphique, carte à
  barre de progression) — "Bureau feutré" (noir chaud, serif Spectral, accent laiton),
  "Financier confiant" (noir-bleu froid, sans-serif géométrique Space Grotesk, accent indigo),
  "Nocturne chaleureux" (noir prune, serif Petrona, accent terracotta). Ethan a choisi le fond/
  accent de la première et la police de la deuxième — combinaison non prévue à l'avance, exactement
  le genre de mix que l'exploration à plusieurs artboards permet.
- **Décision structurante validée avec Ethan avant de coder** : l'app passe en **sombre
  uniquement**, sans mode clair à maintenir en parallèle (au lieu de garder `dark:` partout comme
  avant). Ça change l'ampleur du travail : remplacer une paire (classe claire + `dark:classe`) par
  un seul token, plutôt qu'ajouter une troisième variante.
- **Tokens sémantiques dans `src/index.css`** (`@theme`, Tailwind v4 CSS-first) plutôt que des
  couleurs Tailwind brutes dans les composants : `bg`/`surface`/`field`/`border` (fonds et
  bordures, trois niveaux de clarté), `heading`/`body`/`muted` (texte), `accent`/`accent-hover`
  (laiton `#C6A15B`, cohérent avec la charte "Bureau feutré"), `positive`/`negative` (vert/rouge,
  convention métier des montants déjà en place, juste reconduite), `info` (bleu, réservé au badge
  "Récurrent" du prévisionnel — sans lui, il aurait fini de la même couleur que l'accent laiton et
  serait devenu indiscernable du badge "Budget"). `font-sans` (IBM Plex Sans, corps de texte) et
  `font-display` (Space Grotesk, titres/KPI/montants) chargées en Google Fonts.
- **Bascule mécanique en deux temps plutôt qu'à la main sur ~200 occurrences** : un script
  Python jetable a fait l'inventaire exact de chaque classe couleur utilisée (`grep` sur toutes
  les classNames), construit un dictionnaire classe-Tailwind-existante → nouveau-token (ex.
  `text-gray-900` → `text-heading`, `dark:text-white` supprimé), puis appliqué le remplacement
  token par token (découpage sur les espaces, jamais de regex approximative) sur tous les
  fichiers `.tsx` hors tests. Deux classes ambiguës (`dark:bg-gray-700` et `dark:text-white`,
  qui désignaient selon le contexte soit un champ de formulaire soit un titre) ont été résolues
  par un remplacement littéral de la paire exacte *avant* la passe générique. Les couleurs dans
  des ternaires JS (`className={cond ? '...' : '...'}`, template literals) n'étaient pas capturées
  par ce script scanning `className="..."` — 7 cas trouvés et corrigés à la main (boutons actifs/
  inactifs, montants positifs/négatifs, badge de source du prévisionnel). Trois graphiques
  (`BalanceEvolutionChart`, `CategoryBreakdownChart`, `MonthlyComparisonChart`) avaient leurs
  couleurs en hex arbitraire (`bg-[#2a78d6]`) plutôt qu'en classes Tailwind — remplacées par les
  nouveaux tokens sémantiques (`bg-accent`, `fill-positive`, etc.), utilisables directement sur
  n'importe quel utilitaire de couleur (`fill-*`, `stroke-*`, `border-*`...) dès qu'ils sont
  déclarés dans `@theme`.
- **`MonthlyComparisonChart` passe du bleu/rouge au vert/rouge pour la paire divergente
  revenus/dépenses** : cohérent avec le reste de l'app (KPI, montants de transactions) qui utilise
  déjà cette convention ; `BalanceEvolutionChart` et `CategoryBreakdownChart` restent en teinte
  unique sur l'accent laiton (une tendance/un classement n'est pas intrinsèquement positif ou
  négatif, contrairement à une comparaison revenus vs dépenses).
- **Icônes PWA régénérées** (même script Pillow qu'au Lot 15, juste la couleur de fond/glyphe qui
  change) : elles étaient encore bleues, donc plus cohérentes avec le nouvel accent laiton — sinon
  l'icône d'installation aurait juré avec le reste de l'app.
- Validé : `tsc -b`, `oxlint`, `npm run build` propres ; build inspecté pour confirmer que
  Tailwind génère bien les nouvelles classes (`bg-accent`, `text-heading`, `bg-info/15` avec son
  fallback `color-mix`, etc.) plutôt que de les ignorer silencieusement comme des classes inconnues.
  64 tests Vitest/RTL toujours verts sans modification (ils testent du texte/des rôles, jamais des
  classes CSS). Recherche exhaustive de résidus (`grep` sur tous les anciens tokens gray/sky/red/
  green/amber et sur `dark:`) : zéro occurrence restante hors tests.
- **Non fait** : comme toujours, aucune vérification visuelle réelle dans un navigateur (aucun
  outil d'automatisation disponible) — **à faire par l'utilisateur** en rafraîchissant
  `npm run dev`.

### Retouches UX après relecture visuelle par Ethan (fait)
Trois retours après le premier coup d'œil réel dans le navigateur (la seule vérification
visuelle possible ici, faite par Ethan lui-même) :
- **Champ d'import CSV pas assez visible** : `<input type="file">` sans style se fondait dans le
  fond sombre. Stylé via le pseudo-élément `file:` de Tailwind (fond accent, texte blanc) plutôt
  qu'en reconstruisant un composant de sélection de fichier custom.
- **Formulaires de création cachés derrière un bouton "+ Ajouter..."** plutôt qu'affichés en
  permanence au-dessus de la liste : `/accounts`, `/savings`, `/loans`, et les trois sections
  secondaires (règles de catégorisation, budget du mois, dépenses récurrentes). Chaque écran
  gagne un état `isCreating` ; le formulaire se referme aussi automatiquement après une création
  réussie. `RecurringExpensesSection` avait déjà un `onCancel` optionnel sur son formulaire
  (réutilisé pour l'édition en ligne) — il suffisait de le rendre obligatoire côté création plutôt
  que d'écrire un second formulaire.
- **Largeur de page uniformisée** : le composant `AppLayout` n'accepte plus de `maxWidth` par
  page (chaque écran avait sa propre valeur - `max-w-3xl`/`4xl`/`5xl` - sans que ce soit
  intentionnel) ; une seule largeur (`max-w-5xl`) pour tous les écrans protégés.
- **Bug de test découvert en cachant les formulaires** : `CategoryRulesSection.test.tsx`
  vérifiait `getByText('Alimentation')` en pensant tester le nom de catégorie affiché dans la
  liste des règles — en réalité, cette assertion ne passait que par accident, en matchant
  l'`<option>Alimentation</option>` du formulaire de création (toujours monté avant ce lot). Une
  fois le formulaire caché par défaut, l'assertion échouait pour de bon. `getByText` avec une
  chaîne exacte ne peut pas matcher le texte réellement affiché dans la liste ("→ Alimentation"
  scindé sur plusieurs nœuds texte) — corrigé en scopant la recherche à la ligne (`<li>`) et en
  utilisant une regex, comme c'était déjà fait ailleurs pour `RecurringExpensesSection`. Un rappel
  qu'un test qui passe ne prouve pas qu'il teste la bonne chose.
- 6 tests Vitest/RTL supplémentaires (un par écran/section pour le nouveau bouton "+"), 70 au
  total. `tsc -b`, `oxlint` et `npm run build` propres.
- **Bug réel trouvé par Ethan en testant un vrai import CSV** : un double-clic sur "Importer"
  déclenchait deux requêtes d'import quasi simultanées ; la première réussissait (message vert),
  la seconde échouait ensuite (message rouge) — les deux s'affichaient en même temps puisque
  `error`/`summary` sont deux états indépendants, chacun mis à jour par sa propre requête. Le
  bouton était bien `disabled={isImporting}`, mais cet attribut ne se pose qu'au prochain rendu
  React : deux clics assez rapprochés peuvent tous les deux déclencher `handleSubmit` avant que le
  premier rendu désactivant le bouton n'ait eu lieu. Corrigé avec un verrou synchrone
  (`useRef<boolean>`, testé et remis à `false` avant tout `await`) en plus de l'état `isImporting`
  existant — l'état React pilote l'affichage (bouton grisé, "Import…"), la ref empêche réellement
  la double soumission indépendamment du moment où React re-rend. Testé avec deux `user.click()`
  non attendus l'un après l'autre (`Promise.all`) pour reproduire la course sans dépendre du
  timing réel. 71 tests au total.

### Note personnelle sur les transactions + filtres rapides (fait)
- **Nouveau champ `Transaction.Notes`** (`string?`, 500 caractères max) : texte libre pour l'usage
  personnel d'Ethan (ex. "remboursé par Paul"), jamais lu ni écrit par l'import ou la
  catégorisation. Migration `AddTransactionNotes` (colonne nullable, aucune donnée existante
  affectée). Nouvel endpoint `PUT /api/transactions/{id}/notes`, symétrique à l'endpoint de
  catégorisation déjà en place (`TransactionService.UpdateNotesAsync`, même convention :
  `NotFoundException` sur une transaction inexistante ou d'un autre utilisateur, validation de
  longueur dupliquée côté FluentValidation pour un 400 rapide en plus du contrôle service). Une
  chaîne blanche efface la note (stockée `null`, jamais une chaîne vide).
- **Frontend** : colonne "Note" dans le tableau de `/transactions`, champ texte qui enregistre au
  blur (pas à chaque frappe) et seulement si la valeur a réellement changé — évite un PUT inutile
  à chaque clic dans le champ. État local par ligne (`NotesCell`) resynchronisé depuis la prop via
  un `useEffect` quand la transaction sous-jacente change (après une mutation ou un refetch).
- **Boutons rapides "Ce mois-ci" / "3 derniers mois"** ajoutés aux filtres de `/transactions`,
  même calcul de plage que `DashboardPage` (dupliqué plutôt qu'extrait - toujours seulement deux
  usages, sous le seuil de trois qui a justifié l'extraction de `format.ts`).
- Validé en conditions réelles contre la vraie base locale d'Ethan (utilisateur et compte de test
  séparés, un import CSV synthétique) : note enregistrée avec trim, note effacée par une chaîne
  blanche, 400 sur une note de 501 caractères, 404 sur une transaction inexistante — utilisateur
  et compte de test supprimés ensuite (pas d'endpoint de suppression de compte utilisateur, donc
  ligne supprimée directement en base). 163 tests `dotnet test` (5 nouveaux pour
  `UpdateNotesAsync`), 73 tests Vitest/RTL (2 nouveaux). `tsc -b`, `oxlint` et `npm run build`
  propres des deux côtés.
- **Non fait** : comme toujours, aucune vérification visuelle réelle (aucun outil
  d'automatisation disponible) — **à faire par l'utilisateur**.

### Vue calendrier des échéances (abonnements et crédits) (fait)
Demande d'Ethan : un affichage de calendrier mois/année pour voir les dates de ses
abonnements (mensuels/annuels) et de ses crédits, avec leur date de fin quand il y en a une.

- **Refactor plutôt que doublon** : `RecurringExpenseProjector` calculait déjà un total mensuel
  (`GetMonthlyContribution`) sans jamais matérialiser les dates individuelles. Plutôt que d'écrire
  un second calcul de dates pour le calendrier (avec le risque que les deux se désaccordent un
  jour), la logique a été retournée dans l'autre sens : une nouvelle méthode
  `GetOccurrenceDates(expense, month)` renvoie chaque occurrence concrète
  (`RecurringExpenseOccurrence(Date, IsLastOccurrence)`), et `GetMonthlyContribution` est
  maintenant définie comme `Amount * GetOccurrenceDates(...).Count`. Les 21 tests existants
  (`RecurringExpenseProjectorTests` + `ForecastServiceTests`) passent sans modification après le
  refactor, ce qui prouve qu'il ne change aucun comportement observable. Le cas hebdomadaire est
  le plus délicat : une approximation "4,33 semaines/mois" se trompe d'un paiement dans les mois
  à 5 occurrences du jour de la semaine — `GetOccurrenceDates` compte les vraies occurrences du
  jour de semaine de `StartDate`, bornées à `[StartDate, EndDate]`.
- **`LoanProjector`** (nouveau) : `Loan` n'a pas de `StartDate` dans le modèle de domaine, contrairement
  à `RecurringExpense` — seul `EndDate.Day` peut ancrer le jour du mois, ce qui correspond au
  fonctionnement réel de la plupart des crédits (un jour de prélèvement fixe chaque mois, et
  `EndDate` qui est littéralement la date du dernier paiement). Pas de borne basse non plus, comme
  le fait déjà `ForecastService` (`EndDate >= month`) : un crédit est considéré actif jusqu'à
  `EndDate`, faute de `StartDate` pour le borner autrement.
- **`CalendarService`** (nouveau, `Application/Calendar/`) : combine `RecurringExpenseProjector` et
  `LoanProjector` en une liste d'occurrences datées et triées (`GetMonthlyCalendarAsync`,
  `GetAnnualCalendarAsync` = 12 appels mensuels). Exclut délibérément les lignes de `Budget` : un
  budget est un montant prévu par catégorie/mois, jamais rattaché à un jour précis, donc il n'a
  rien à apporter à un calendrier d'événements datés (documenté dans le commentaire XML de la
  classe).
- **Nouveaux endpoints** `GET /api/calendar/monthly?month=...` et `GET /api/calendar/annual?year=...`
  (`CalendarController`), protégés par `[Authorize]` comme le reste de l'API, filtrés par
  utilisateur courant via `ICurrentUser`.
- **Frontend** : troisième mode "Calendrier" dans `/forecast` (à côté de Mensuel/Annuel), avec un
  sous-bascule Mois/Année. La vue mensuelle est une vraie grille de calendrier (7 colonnes,
  semaine commençant le lundi, navigation ‹/›) ; la vue annuelle est une liste groupée par mois
  (grille de 12 cartes) plutôt qu'une grille de mini-calendriers, pour rester lisible. Chaque
  échéance affiche son libellé, son montant au survol, et un badge "(dernière)" quand
  `isLastOccurrence` est vrai ; abonnements et crédits sont distingués par couleur (mêmes tokens
  `bg-info`/`bg-accent` que le badge de source du prévisionnel).
- Validé : 8 nouveaux tests `RecurringExpenseProjectorTests` (18 au total dans ce fichier), 9
  nouveaux `CalendarServiceTests`, 5 nouveaux tests `ForecastPage.test.tsx` (9 au total) — 179
  tests `dotnet test` et 77 tests Vitest/RTL au global, tous verts. `tsc -b`, `oxlint`,
  `npm run build` propres des deux côtés. Testé en conditions réelles contre la vraie base locale
  d'Ethan (utilisateur de test séparé, un abonnement mensuel avec date de fin et un crédit) :
  dates d'occurrence et drapeau `isLastOccurrence` vérifiés sur plusieurs mois (dont les deux mois
  de fin respectifs) et sur la vue annuelle — utilisateur et données de test supprimés ensuite.
- **Non fait** : comme toujours, aucune vérification visuelle réelle de la grille (aucun outil
  d'automatisation disponible) — **à faire par l'utilisateur** en rafraîchissant `npm run dev`.

### Mode clair/sombre (fait)
Retour en arrière assumé sur la décision "sombre uniquement" du Lot DA : demande explicite
d'Ethan d'ajouter un mode clair.

- **Palette claire dérivée de la palette sombre** plutôt qu'indépendante : mêmes teintes
  (hue oklch 60/70/75/155/25/250) que la DA existante, luminosité inversée. L'accent laiton
  (`#C6A15B`) est en revanche recalculé pour le mode clair (`oklch(0.5 0.1 75)`, plus soutenu) :
  utilisé tel quel comme texte (`text-accent` sur les badges), le ton clair d'origine tombe à
  ~2,4:1 de contraste sur fond blanc (calcul manuel de luminance relative WCAG), bien en dessous
  du seuil de lisibilité — inutile pour un simple bouton plein (texte blanc dessus, peu importe
  le fond de page) mais rédhibitoire pour du texte directement sur fond clair. Même traitement
  pour `positive`/`negative`/`info`, plus soutenus en clair pour rester lisibles sur un fond
  presque blanc.
- **Un seul jeu de tokens, deux valeurs** : les variables `--color-*` du Lot DA (déjà toutes
  dans `@theme`) sont redéfinies une seconde fois dans `src/index.css`, sous
  `@media (prefers-color-scheme: light) { :root:not([data-theme='dark']) { ... } }` (préférence
  système, sauf override explicite) et sous `:root[data-theme='light'] { ... }` (choix explicite,
  toujours prioritaire). Aucune classe `dark:`/`light:` à ajouter dans les composants : les
  utilitaires Tailwind existants (`bg-surface`, `text-heading`, etc.) référencent déjà
  `var(--color-*)`, donc ils suivent automatiquement la palette active.
- **Nouveau token `--color-overlay`** : les nombreux `hover:bg-white/5` (survol des boutons/liens
  bordés) étaient un blanc translucide, invisible en mode clair (blanc sur presque-blanc). Un
  token dédié (blanc 5% en sombre, noir 5% en clair) remplace toutes les occurrences
  (`hover:bg-overlay`) - même mécanisme de redéfinition par thème que les autres tokens.
- **Bascule** : `src/theme/useTheme.ts` (résout la préférence stockée en `localStorage`, sinon
  `prefers-color-scheme`, applique `<html data-theme>`) + bouton dans `AppSidebar` à côté de
  "Déconnexion". Un petit script inline synchrone dans `index.html` (avant le chargement du
  bundle) applique le même choix immédiatement, pour éviter un flash de la mauvaise palette au
  chargement — dupliqué à la main par rapport à `useTheme.ts` puisqu'il doit tourner seul, avant
  tout script de module.
- Validé : 3 nouveaux tests (`AppSidebar.test.tsx` : thème par défaut sombre en l'absence de
  préférence stockée, bascule + persistance `localStorage`, redémarrage depuis une préférence
  déjà stockée) — 80 tests Vitest/RTL au total. `tsc -b`, `oxlint`, `npm run build` propres ;
  build inspecté pour confirmer que `hover:bg-overlay`, `bg-accent/15` (via `color-mix`) et les
  deux blocs de redéfinition de palette sont bien émis. Confirmé visuellement par Ethan
  (captures d'écran des vues Calendrier et Transactions en mode clair).
- **Non fait** : pas de lien entre le `theme-color` de la PWA (`index.html`/manifest, resté sur
  l'accent laiton) et le thème actif — détail mineur, revisiter si besoin.

### Raccourci "Marquer comme récurrente" sur les transactions (fait)
Demande d'Ethan : pouvoir repérer/marquer une dépense récurrente (abonnement, etc.)
directement depuis la page Transactions plutôt que de ressaisir sa fréquence sur
`/forecast`. Deux options possibles - un simple raccourci de création, ou un vrai lien
persistant `Transaction → RecurringExpense` en base avec badge affiché sur les
transactions déjà liées - **raccourci de création choisi** par Ethan : plus simple,
zéro changement de modèle de données, pas de moteur de correspondance à construire pour
détecter les transactions futures similaires (aurait dupliqué la logique des règles de
catégorisation pour un gain incertain).

- **Aucun changement backend** : réutilise tel quel `POST /api/recurring-expenses`
  (existant depuis le Lot 6). Le bouton "+ Récurrente" sur une ligne de `/transactions`
  ouvre un sélecteur de fréquence (mêmes libellés que `RecurringExpensesSection`) et crée
  une `RecurringExpense` pré-remplie avec le libellé, le montant (`Math.abs`, la
  convention de magnitude positive du domaine), la catégorie et la date de la transaction
  (`RecurringMarkCell`/`RecurringMarkDialog` dans `TransactionsPage.tsx`).
- **N'apparaît que sur une vraie dépense** (montant négatif, hors virement interne) :
  `RecurringExpense` n'a pas d'équivalent "revenu récurrent" dans le modèle de domaine.
- **Pas de lien conservé après coup** (choix assumé, voir plus haut) : après création, la
  transaction affiche juste "Ajoutée ✓" pour éviter un doublon accidentel dans la même
  session — rien ne l'indique après un rechargement de page, et la dépense créée s'édite
  ensuite sur `/forecast` comme n'importe quelle autre.
- **Bug réel trouvé par Ethan sur la première version** : le sélecteur de fréquence
  s'ouvrait en ligne dans la cellule du tableau, ce qui écrasait la largeur des colonnes
  voisines (Catégorie, Note) à chaque clic. Remplacé par une modale (`RecurringMarkDialog`,
  premier composant modal de l'app) — `fixed inset-0` la sort du flux normal, donc le
  tableau ne bouge plus jamais autour d'elle. Fermeture sur Échap, sur clic du fond
  (comparaison `e.target === e.currentTarget` pour ignorer les clics dans le panneau), ou
  sur "Annuler".
- **Deuxième retour d'Ethan sur la même modale** : `bg-surface` n'est que légèrement plus
  clair que `bg-bg` par design (cartes discrètes sur le fond, voir la DA) - un fond assombri
  à 40% suffisait à peine à distinguer la modale de la page derrière en mode sombre.
  Assombrissement du fond porté à 70% et bordure (`border-border`) + ombre plus marquée
  (`shadow-xl`) ajoutées au panneau, pour que la limite soit visible par le contour plutôt
  que de compter uniquement sur l'écart de luminosité entre les deux fonds.
- Validé : 6 nouveaux tests `TransactionsPage.test.tsx` (bouton absent sur revenu/virement
  interne, corps de la requête exact à la création, fermeture par "Annuler"/Échap/clic sur
  le fond sans appel API, erreur affichée en cas d'échec) — 86 tests Vitest/RTL au total.
  `tsc -b`, `oxlint`,
  `npm run build` propres. Pas de validation backend en conditions réelles nécessaire :
  aucun changement côté API, l'endpoint réutilisé est déjà couvert par les tests
  d'intégration existants et par la validation réelle du Lot 6.
- **Retouches supplémentaires demandées par Ethan** : le bouton n'affiche plus que "+" (le
  libellé "Récurrente" faisait doublon avec l'en-tête de colonne déjà nommé "RÉCURRENTE" -
  un `aria-label` explicite garde le bouton compréhensible pour un lecteur d'écran). Filtres
  de `/transactions` réorganisés : "Du"/"Au" étaient toujours visibles même quand un
  préréglage ("Ce mois-ci"/"3 derniers mois") suffisait, ce qui encombrait la section pour
  rien - ils ne s'affichent plus que derrière un choix "Personnalisé". Deux itérations sur
  la forme de ce contrôle : d'abord trois boutons ("Ce mois-ci"/"3 derniers mois"/
  "Personnalisé", surlignés en `bg-accent` quand actifs), puis converti en `<select>`
  ("Toutes les périodes"/"Ce mois-ci"/"3 derniers mois"/"Personnalisé") à la demande
  d'Ethan pour rester cohérent avec les filtres Compte/Catégorie voisins, qui sont déjà des
  `<select>` - "Toutes les périodes" redonne aussi un moyen explicite d'effacer la période
  (elle n'en avait pas avant ce lot, au-delà de recharger la page). 3 tests supplémentaires
  (champs cachés par défaut, apparition + filtrage au choix de "Personnalisé", remise à
  zéro via "Toutes les périodes") — 91 tests Vitest/RTL au total.

### Revue avant import (fait)
Demande d'Ethan : pouvoir voir la liste des lignes détectées par un import CSV, en
exclure certaines, et ajuster/détecter leur catégorie - le tout avant que quoi que ce
soit ne soit enregistré. Deux architectures possibles : revue après import (l'import se
fait tout de suite, la revue permet de supprimer/recatégoriser ensuite) ou aperçu avant
import (rien n'est persisté avant confirmation) — **aperçu avant import choisi** par
Ethan, plus proche de "revenir en arrière avant que ce soit fait" et évitant d'avoir à
construire un endpoint de suppression de transaction (qui n'existait pas) juste pour
défaire un import annulé.

- **`BankStatementImportService` scindé en deux** : `PreviewAsync` (parse + déduplique +
  auto-catégorise, comme l'ancien `ImportAsync` un-shot, mais s'arrête là - rien n'est
  persisté) et `CommitAsync` (reçoit exactement les lignes que le client veut garder,
  éventuellement avec une catégorie modifiée, et les enregistre). La détection de
  catégorie ne change pas : correspondance exacte sur la catégorie suggérée par la banque
  d'abord, puis repli sur les `CategoryRule` de l'utilisateur (Lot 4) - elle tourne
  simplement à l'aperçu plutôt qu'à l'enregistrement.
- **`ImportRow`** (nouveau type partagé) : une ligne pas encore persistée, dans les deux
  sens de l'aller-retour aperçu/commit. `CategoryName` n'est renseigné qu'à l'aperçu (pour
  l'affichage) et ignoré par `CommitAsync`, qui ne persiste que `CategoryId` - le client
  renvoie exactement les lignes qu'il veut garder, une ligne exclue n'est simplement
  jamais dans la liste envoyée au commit (pas de champ "exclu" séparé).
- **Déduplication revérifiée aux deux étapes** : `TransactionDeduplicator` généralisé
  (`RemoveAlreadyImported<T>` avec un sélecteur d'empreinte) pour tourner à la fois sur
  les `ParsedBankTransaction` fraîchement parsées (aperçu) et sur les `ImportRow` renvoyées
  par le client (commit) - au cas où quelque chose aurait changé entre les deux appels
  (un autre import, une saisie manuelle).
- **Nouveaux endpoints** `POST /api/bank-accounts/{id}/import/preview` (remplace l'ancien
  `POST .../import`, supprimé) et `POST /api/bank-accounts/{id}/import/commit`.
- **Frontend** : le bouton "Importer" devient "Aperçu" - il ouvre une modale
  (`ImportPreviewDialog`, même technique `fixed inset-0` que `RecurringMarkDialog`) listant
  chaque ligne détectée avec une case à cocher (incluse par défaut), son montant, et un
  `<select>` de catégorie pré-rempli avec la suggestion mais éditable. "Confirmer l'import"
  n'envoie que les lignes encore cochées.
- Validé : 12 tests `BankStatementImportServiceTests` (réécrits pour Preview/Commit,
  contre 5 avant), tests `TransactionDeduplicatorTests` adaptés au sélecteur générique,
  2 tests d'intégration `BankAccountImportFlowTests` (aperçu ne persiste rien, ligne
  exclue jamais importée) — 186 tests `dotnet test` au total. 4 tests `AccountsPage.test.tsx`
  réécrits/ajoutés (aperçu, double-clic ignoré, commit avec ligne exclue + catégorie
  modifiée, annulation) — 90 tests Vitest/RTL au total. `tsc -b`, `oxlint`, `npm run build`
  propres des deux côtés. Testé en conditions réelles contre la vraie base locale d'Ethan
  (compte de test, CSV synthétique à 2 lignes) : aperçu sans persistance vérifié,
  exclusion d'une ligne au commit vérifiée (jamais en base), re-aperçu du même fichier
  confirmant que la ligne déjà importée disparaît alors que la ligne exclue reste
  proposée, commit avec catégorie modifiée manuellement vérifié — compte et utilisateur
  de test supprimés ensuite.

### Labels visibles sur les formulaires compacts (fait)
Repéré par Ethan sur le champ "Priorité" de la section Règles de catégorisation : un
`<input type="number">` sans `placeholder` ni label visible (seulement un `aria-label`,
invisible à l'écran), qui affichait juste "100" sans aucune indication de ce que ce
nombre représentait. Généralisé : "comme sur d'autres formulaires" - un `aria-label` seul
ne suffit pas dès qu'un champ a une valeur (un placeholder disparaît une fois rempli, un
aria-label n'est jamais affiché du tout), donc plusieurs formulaires compacts en grille
avaient le même problème.

- **`CategoryRulesSection` (`CreateRuleForm`)** : les trois champs (Motif, Catégorie,
  Priorité) ont maintenant un `<label>` visible au-dessus, comme les formulaires plus
  spacieux (`LoginPage`, `CreateAccountForm`...). Ajout d'une phrase d'aide sous le champ
  Priorité ("Le plus petit numéro est testé en premier") - la sémantique
  petit-nombre-gagne (voir `CategoryRuleMatcher`, `OrderBy(r => r.Priority)`) n'était
  expliquée nulle part, probablement la vraie source de confusion au-delà du simple
  manque de label.
- **`BudgetSection` (`CreateBudgetLineForm`)** : Catégorie et Montant planifié idem.
  L'édition inline du montant par ligne (`BudgetLineRow`) reste en `aria-label` seul - son
  contexte est déjà visible (catégorie dans la cellule voisine, en-tête de colonne
  "Planifié"), pas le même problème qu'un formulaire autonome.
- **`RecurringExpensesSection` (`RecurringExpenseForm`)** : les six champs (Libellé,
  Montant, Catégorie, Fréquence, dates de début/fin) labellisés. Ce formulaire sert à la
  fois de création et d'édition en ligne par dépense - plusieurs lignes peuvent être en
  édition simultanément, donc les `id` sont scopés par dépense
  (`recurring-expense-{id|new}-{champ}`) pour que deux formulaires ouverts en même temps
  n'aient jamais le même `id` (deux `<label htmlFor>` identiques casseraient le clic sur
  le libellé pour l'un des deux).
- **`AccountsPage` (formulaire d'édition d'un compte, `AccountCard`)** : Banque, Libellé,
  IBAN labellisés - même souci de scoping par compte (`account-{id}-{champ}`), et ce
  formulaire n'avait jusqu'ici aucun test (angle mort découvert en le modifiant) : un
  nouveau test couvre l'édition complète.
- Validé : tests existants adaptés au texte des nouveaux labels (raccourcis - "Catégorie
  de la règle" → "Catégorie", etc. - le texte affiché doit rester court dans une grille
  serrée), 1 nouveau test pour l'édition de compte — 92 tests Vitest/RTL au total. `tsc
  -b`, `oxlint`, `npm run build` propres.

### Vraies modales de confirmation de suppression (fait)
Demande d'Ethan (capture d'écran de la boîte `confirm()` native du navigateur, hors DA,
non stylable, non testable avec Testing Library) : remplacer les 6 confirmations de
suppression de l'app (comptes, budgets, règles de catégorisation, crédits, dépenses
récurrentes, objectifs d'épargne) par de vraies modales.

- **`useConfirm()`** (`components/confirmContext.ts` + `components/ConfirmDialog.tsx`,
  même séparation fichier-contexte/fichier-composant que `auth/authContext.ts` -
  `oxlint` signale qu'un fichier exportant à la fois un composant et un hook perd le Fast
  Refresh) : remplace `window.confirm(message)` par `await confirm(message, options)`,
  une Promise résolue par le clic sur le bouton de la modale plutôt que par un retour
  synchrone. Rendu par un seul `ConfirmProvider`, monté une fois dans `AppLayout` (pas à
  la racine de `App.tsx` : `LoginPage`/`RegisterPage` n'ont pas d'action destructive et
  n'ont donc pas besoin du provider) - chaque écran protégé en hérite sans rien à faire.
- **Options** : `confirmLabel`/`cancelLabel` (le bouton dit "Supprimer", pas un
  "Confirmer" générique) et `danger` (fond rouge `bg-negative` plutôt que l'accent laiton -
  première utilisation de `bg-negative` en remplissage plein avec texte blanc dans l'app,
  jusqu'ici réservé aux textes/bordures/fonds translucides). Fermeture sur Échap ou clic
  sur le fond, même mécanique `fixed inset-0` que les modales précédentes
  (`RecurringMarkDialog`, `ImportPreviewDialog`).
- **6 appels convertis** : `AccountsPage` (compte), `BudgetSection` (ligne de budget),
  `CategoryRulesSection` (règle), `LoansPage` (crédit), `RecurringExpensesSection`
  (dépense récurrente), `SavingsPage` (objectif) - simple ajout d'un `await` et d'options,
  la structure `if (!(await confirm(...))) return` reste quasiment identique à l'ancien
  `if (!confirm(...)) return`.
- Validé : 5 nouveaux tests dédiés (`ConfirmDialog.test.tsx` : résolution true/false par
  clic, Échap, clic sur le fond, erreur si utilisé hors `ConfirmProvider`), et les 8 tests
  de suppression existants adaptés (`vi.stubGlobal('confirm', ...)` remplacé par un clic
  réel sur le bouton de la modale ouverte) — 97 tests Vitest/RTL au total. Trois sections
  (`BudgetSection`, `CategoryRulesSection`, `RecurringExpensesSection`) sont testées de
  façon isolée, hors `AppLayout` - `ConfirmProvider` ajouté directement dans leurs
  `renderSection` de test. `tsc -b`, `oxlint`, `npm run build` propres.

### Tri des colonnes du tableau des transactions (fait)
Demande d'Ethan ("dans le tableau des transactions on puisse trier les colonnes, en
appuyant sur une colonne ou un bouton à côté du titre") : discutée lors d'une session
précédente mais jamais réellement codée (aucune trace dans `TransactionsPage.tsx` ni dans
le repository/contrôleur) - reprise et implémentée entièrement dans cette session.

- **Tri côté backend, pas seulement côté client** : `/transactions` pagine déjà à 50
  lignes par page côté serveur, donc trier uniquement la page affichée aurait été trompeur
  (l'utilisateur voit un tri qui ne porte que sur 50 lignes sur potentiellement des
  centaines). `TransactionQuery` gagne `SortBy` (`TransactionSortColumn` : `Date`,
  `BankAccount`, `Label`, `Amount`, `Category`) et `SortDescending`, tous deux avec des
  valeurs par défaut (`Date`/`true`) qui reproduisent l'ordre fixe précédent -
  rétrocompatible avec tout appelant qui ne passe pas ces paramètres.
- **`TransactionRepository.SearchAsync`** : le tri fixe `OrderByDescending(Date)` devient
  un `switch` sur `SortBy` construisant l'`IOrderedQueryable` adapté, puis un
  `ThenBy(Id)`/`ThenByDescending(Id)` (même sens que le tri primaire) pour garder une
  pagination stable quand plusieurs lignes partagent la même valeur triée. Tri par
  libellé sur `CleanedLabel ?? RawLabel` (ce que l'écran affiche réellement), par compte
  sur `BankAccount.Label`, par catégorie sur `Category.Name` (navigation nullable - les
  transactions non catégorisées se retrouvent en tête ou en fin selon la direction, LEFT
  JOIN généré par EF).
- **`TransactionService.SearchAsync`** : deux nouveaux paramètres optionnels `sortBy`/
  `sortDirection` (chaînes, comme `search`) parsés en `TransactionSortColumn` - une valeur
  inconnue ou absente retombe silencieusement sur `Date` plutôt que de rejeter la requête
  (même philosophie que `page`/`pageSize` déjà clampés dans ce service).
  `TransactionsController.Search` expose `sortBy`/`sortDirection` en query string.
- **Frontend** : état `Sort { column, direction }` dans `TransactionsPage`, valeur par
  défaut `{ date, desc }` qui reflète le défaut backend (l'en-tête "Date" affiche donc la
  flèche active dès le chargement initial, pas un tableau qui a l'air non trié).
  `SortableHeader` (bouton dans le `<th>`, flèche ▲/▼ ou ⇕ neutre, `aria-sort` sur le
  `<th>`) pour les 5 colonnes qui ont un sens à trier (Date, Compte, Libellé, Montant,
  Catégorie) - "Note" (texte libre) et "Récurrente" (bouton d'action) restent de simples
  `<th>`, trier dessus n'aurait pas de sens. Premier clic sur une colonne = ascendant,
  deuxième clic = descendant (convention tableur), et changer de colonne ou de direction
  remet `page` à 1 comme un changement de filtre.
- Validé : 100 tests Vitest/RTL frontend (4 nouveaux : défaut Date desc, clic Montant asc
  puis desc, retour à la page 1 au changement de tri) et 166 tests xUnit backend (4
  nouveaux sur `TransactionServiceTests` : tri Montant
  ascendant, Libellé descendant, Date ascendant, `sortBy` inconnu retombant sur Date
  descendant). **Non validé en conditions réelles** : Docker n'était pas disponible dans
  cette session, donc ni les tests d'intégration `BudgetDbContextPostgresTests`/
  `BudgetPrevisionnel.Api.Tests` (Testcontainers) ni un test manuel contre le vrai Postgres
  local n'ont pu tourner - à refaire par Ethan avant de faire confiance au tri sur
  `Category`/`BankAccount` en particulier (jointures EF jamais exécutées contre un vrai
  Postgres pour cette fonctionnalité).

### Revenus récurrents (fait)
Demande d'Ethan, partie du même échange que le tri des colonnes : pourquoi une transaction
positive (salaire, virement entrant) ne peut pas être marquée comme récurrente sur
`/transactions` ? Réponse trouvée dans le code : décision délibérée du Lot 6/12
(`RecurringMarkCell` : `if (transaction.isInternalTransfer || transaction.amount >= 0)
return null`, commentaire "RecurringExpense has no income counterpart in the domain
model"). Plutôt que de laisser la limitation, ajout du pendant complet côté revenus.

- **`RecurringIncome`** (Domain/Application/Infrastructure/API) : mirror quasi exact de
  `RecurringExpense` à chaque couche (entité, `IRecurringIncomeRepository`/
  `RecurringIncomeRepository`, `RecurringIncomeService` avec la même validation "montant
  strictement positif, la direction est encodée par le nom de l'entité", contrôleur REST
  `/api/recurring-incomes` en CRUD, contrats + validators FluentValidation). Table
  `RecurringIncomes` séparée (migration `AddRecurringIncomes`), pas une colonne "type" sur
  `RecurringExpense` - `RecurringExpense` reste nommée et pensée comme expense-only,
  cohérent avec sa doc existante.
- **`RecurringIncomeProjector`** : copie volontaire (pas une extraction partagée) de
  `RecurringExpenseProjector` - même convention déjà en place dans ce repo ("extract only
  past two consumers", voir la note de `TransactionsPage` sur `FREQUENCY_LABELS`), et
  `RecurringExpense`/`RecurringIncome` sont exactement les deux consommateurs de ce calcul
  d'occurrences (hebdomadaire/mensuel/annuel).
- **Intégration Prévisionnel** : `MonthlyForecast` gagne `TotalRecurringIncome` et
  `NetBalance` (= revenus - `Total`), **additifs** - `Total`/`CategoryLines` gardent
  exactement leur sens actuel ("dépenses prévues"), aucun consommateur existant n'est
  affecté par un changement de signe caché. Le tableau mensuel du Prévisionnel affiche
  "Revenus récurrents prévus" et "Solde net prévisionnel" seulement quand il y a un revenu
  récurrent ce mois-ci (sinon ces lignes n'apparaissent pas). La vue annuelle n'a pas été
  retouchée (reste "Total dépenses" par mois) - pas demandé, aurait changé le sens de
  cette vue au-delà de ce qui était utile ici.
- **Intégration Calendrier** : `CalendarEntryType` gagne `RecurringIncome`, même
  granularité par date que `RecurringExpense`/`Loan`. Couleur dédiée côté frontend
  (`bg-positive/15 text-positive`, cohérent avec la convention "vert = argent qui rentre"
  déjà utilisée pour les montants positifs dans les tableaux).
- **`TransactionsPage`** : `RecurringMarkCell` n'exclut plus que les virements internes ;
  le signe du montant choisit l'endpoint (`/api/recurring-expenses` vs
  `/api/recurring-incomes`) et le texte du bouton/dialogue ("dépense récurrente" vs
  "revenu récurrent").
- Validé : 185 tests xUnit backend (19 nouveaux : `RecurringIncomeServiceTests`,
  `RecurringIncomeProjectorTests`, cas revenu ajoutés à `ForecastServiceTests`/
  `CalendarServiceTests`) et 108 tests Vitest/RTL frontend (`RecurringIncomesSection.test.tsx`,
  cas ajoutés à `ForecastPage.test.tsx`/`TransactionsPage.test.tsx`). **Testé en conditions
  réelles** (Docker était disponible cette fois) : migration `AddRecurringIncomes` appliquée
  au vrai Postgres local, création/liste/suppression d'un revenu récurrent, vérification que
  `/api/forecast/monthly` et `/api/calendar/monthly` le reflètent bien, via un utilisateur et
  des données jetables supprimés immédiatement après (`DELETE FROM "Users" WHERE Email = ...`,
  cascade sur `RecurringIncomes`).
- Point ouvert non traité : le bouton "+" sur une transaction crée toujours un enregistrement
  sans lien persistant vers la transaction source (déjà vrai pour les dépenses avant ce lot) -
  `isDone` ne survit pas au rechargement de la page.

### Historique des imports CSV (fait)
Demande d'Ethan à la suite du lot précédent : voir l'historique des fichiers CSV importés
sur `/accounts`. Rien n'existait côté backend - `BankStatementImportService.CommitAsync` ne
persistait que les `Transaction`, jamais une trace du fichier importé lui-même
(nom, date, compteurs) ; le nom de fichier était même déjà disponible côté frontend
(`ImportCsvForm`'s `file.name`) mais jeté après l'appel de preview, jamais envoyé au commit.

- **`ImportBatch`** (nouvelle entité) : `BankAccountId`, `FileName`, `ImportedAtUtc`,
  et les 4 compteurs d'`ImportSummary` dupliqués au moment du commit (pas de FK vers les
  `Transaction` créées - une liste de compteurs suffit, pas besoin de savoir *lesquelles*
  transactions viennent de quel import). Cascade depuis `BankAccount` (même convention que
  `Transaction`) - supprimer un compte supprime son historique d'import avec lui. Migration
  `AddImportBatches`.
- **`BankStatementImportService.CommitAsync`** gagne un paramètre `fileName` (le frontend
  l'envoie maintenant dans `ImportCommitRequest`) et enregistre un `ImportBatch` après la
  détection de virements internes - **même quand `NewTransactionsImported` est 0** (import
  entièrement composé de doublons) : l'utilisateur a quand même choisi un fichier et lancé un
  import, l'historique doit pouvoir expliquer pourquoi rien de nouveau n'est apparu, pas
  seulement les imports "réussis". Nouvelle méthode `GetHistoryAsync` (vérifie la propriété du
  compte, même pattern que le reste du service) exposée en `GET
  /api/bank-accounts/{id}/import/history`, triée du plus récent au plus ancien.
- **Frontend** : `ImportCsvForm` retient le nom de fichier au moment où le *preview* réussit
  (`previewFileName`, pas une lecture directe de `file.name` au moment du commit - l'input
  fichier reste interactif tant que la modale de review est ouverte, un changement de fichier
  pendant la review aurait désynchronisé le nom envoyé des lignes réellement réviewées).
  `ImportHistorySection` (nouveau, sous le formulaire d'import de chaque compte) : liste
  toujours affichée (même convention que `RecurringExpensesSection` : état vide explicite
  plutôt que rien), invalidée après chaque commit.
- **Bug trouvé en conditions réelles** : après avoir généré la migration
  `AddImportBatches`, un premier test contre le vrai Postgres local a échoué avec `relation
  "ImportBatches" does not exist` - la migration avait été *générée* mais jamais *appliquée*
  (`dotnet ef database update` oublié après `migrations add`). Résultat instructif : comme
  `TransactionRepository.AddRangeAsync` fait son propre `SaveChangesAsync` avant l'écriture de
  l'`ImportBatch`, la transaction importée avait bien été persistée malgré l'erreur 500
  renvoyée au client - un import "en échec" avait donc quand même laissé une transaction en
  base. Un second essai après la migration a été correctement compté comme doublon (0 nouvelle
  transaction, 1 doublon), confirmant que la déduplication protège bien ce genre de
  scénario, mais ça vaut la peine qu'Ethan le sache : une erreur 500 sur `/import/commit` ne
  garantit pas que rien n'a été écrit.
- Validé : 210 tests xUnit backend (11 nouveaux sur `BankStatementImportServiceTests` +
  1 sur `BankAccountImportFlowTests`) et 108 tests Vitest/RTL frontend (2 nouveaux, plus 9
  tests existants adaptés - la nouvelle requête de `ImportHistorySection` se déclenche dès
  qu'un compte s'affiche, ce qui décalait les files `mockResolvedValueOnce` de plusieurs
  tests existants). **Testé en conditions réelles** contre le vrai Postgres local une fois
  la migration réellement appliquée : commit avec nom de fichier, vérification de
  `/import/history`, nettoyage immédiat des données jetables.

### Téléchargement du fichier importé (fait)
Demande d'Ethan à la suite du lot précédent : pouvoir rouvrir/retélécharger le CSV
exact qui a été importé, pas seulement voir son nom dans l'historique.
`ImportBatch` ne gardait que des métadonnées (nom, date, compteurs) - le contenu du
fichier n'était jamais persisté nulle part, jeté juste après le parsing en mémoire.

- **`ImportBatch.FileContent`** (`byte[]?`, `bytea` nullable en base - migration
  `AddImportBatchFileContent`) : nul pour les imports antérieurs à ce lot (colonne
  ajoutée après coup) ou si le client n'envoie rien. `ImportBatchResponse.HasStoredFile`
  reflète cette présence côté frontend pour savoir s'il faut proposer le lien.
- **Transport du fichier au commit** : `/import/commit` restait un body JSON (pas
  multipart, pour ne pas complexifier le binding ASP.NET Core d'une liste + un fichier
  dans le même formulaire) - `ImportCommitRequest` gagne un `FileContentBase64` optionnel,
  validé côté `ImportCommitRequestValidator` (`Convert.TryFromBase64String`, pour éviter
  qu'une entrée invalide remonte en `FormatException` non gérée plutôt qu'en 400 propre -
  même raisonnement que la validation des lignes juste au-dessus). Le frontend garde
  désormais le `File` complet (pas juste son nom) entre preview et commit, le relit en
  base64 par blocs de 32 Ko avant l'envoi (`String.fromCharCode(...bytes)` sur un tableau
  complet peut dépasser la pile d'appels sur un relevé volumineux).
- **`GET /import/history/{batchId}/file`** (nouveau) : renvoie les octets stockés avec le
  nom de fichier d'origine (`ImportBatchNotFoundException` → 404 si le batch n'existe pas,
  n'appartient pas à ce compte, ou n'a pas de fichier stocké - même exception pour les
  deux derniers cas, le résultat pour l'appelant est identique). `ImportBatchRepository`
  a une méthode dédiée `GetByIdForAccountAsync` pour ce cas, séparée de
  `GetForAccountAsync` : cette dernière ne charge plus les octets (projection explicite
  sans `FileContent`) puisqu'elle tourne à chaque affichage de `/accounts` et n'a besoin
  que des compteurs - sans ça, chaque chargement de page aurait tiré en mémoire tous les
  CSV jamais importés sur le compte.
- **Téléchargement côté frontend** : un `<a href>` classique ne peut pas porter le header
  `Authorization` qu'exige l'API, donc `useFileDownload` (nouveau hook, à côté de
  `useApiClient`) récupère le fichier en `Blob` via `apiFetchBlob` (nouvelle fonction dans
  `api/client.ts`, parallèle à `apiFetch` mais qui ne fait pas `response.json()`) puis le
  fait sauvegarder via une balise `<a>` jetable + `URL.createObjectURL`. Le nom de fichier
  d'origine est repris du header `Content-Disposition` de la réponse - qui n'est pas dans
  la liste des headers exposés par défaut en CORS, ajout explicite de
  `WithExposedHeaders("Content-Disposition")` à la policy CORS (`Program.cs`), sans quoi
  `response.headers.get(...)` aurait toujours renvoyé `null` en cross-origin malgré un
  header bien présent sur le fil.
- **Bloqueur trouvé en conditions réelles** : un `dotnet run` d'une session précédente
  tournait encore (même symptôme que le lot précédent - voir plus haut), verrouillant les
  DLL et empêchant `dotnet ef migrations add`. Confirmé avant de tuer le processus
  (`tasklist`, PID 58884) puisqu'Ethan l'avait probablement laissé ouvert pour tester
  `/accounts` en live (il regardait cette page au moment de la demande).
- Validé : 5 nouveaux tests `BankStatementImportServiceTests` (contenu stocké/absent,
  téléchargement, batch introuvable, compte d'un autre utilisateur) + 2 nouveaux
  `BankAccountImportFlowTests` (aller-retour upload→commit→téléchargement en HTTP réel,
  et cas sans fichier stocké → 404), tous passés contre le vrai Postgres local
  (Testcontainers) une fois la migration appliquée. Frontend : 3 nouveaux tests
  (`apiFetchBlob` parsing du nom de fichier, présence/absence du lien "Télécharger", clic
  déclenchant bien `createObjectURL`/`revokeObjectURL`), 1 test existant adapté
  (`fileContentBase64` maintenant dans le body du commit). 218 tests xUnit backend et 112
  tests Vitest/RTL frontend au total, tous verts. **Pas de vérification visuelle
  navigateur** (pas d'automatisation navigateur disponible dans cet environnement) - à
  faire par Ethan : relancer `dotnet run` (tué pour cette session) et confirmer que le
  bouton "Télécharger" fonctionne réellement dans le navigateur, notamment que le fichier
  téléchargé s'ouvre correctement.

### Lot 16 — Déploiement
- VPS OVH ou Railway/Fly.io, pipeline CI/CD, application automatique des
  migrations au déploiement.

## Points ouverts (non bloquants)

Repris du brief, à trancher pendant le développement :
- Alertes/notifications (échéances, dépassement de budget).
- Multi-devise (trace du montant d'origine avant conversion).
- Export de données utilisateur (PDF, CSV).
- RGPD / conformité, si le projet dépasse le cercle familial.
