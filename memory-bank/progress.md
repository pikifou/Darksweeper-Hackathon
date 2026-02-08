# DarkSweeper — Suivi de progression

*(Derniere mise a jour : 8 fevrier 2026)*

---

## Etat global : Prototype jouable + Mine Events + Audio

Le module Sweeper est jouable de bout en bout : grille, fog of war, eclairage, input, reveal, flood fill, flags, victoire/defaite, HUD. Le level design est operationnel avec l'outil de peinture editeur.

Le module Mine Events est entierement code : les mines ne causent plus de defaite instantanee mais declenchent des evenements interactifs (Combat, Coffre, Dialogue, Shrine) via clic droit. Le systeme attend la creation des assets ScriptableObject et le placement dans la scene pour etre testable end-to-end.

Le systeme audio est en place : MusicManager (crossfade entre scenes) et SFXManager (pool AudioSources pour sons one-shot). Les deux sont des singletons auto-bootstrap via `[RuntimeInitializeOnLoadMethod]`, independants de la scene de depart. Les prefabs et assets SO doivent etre crees dans l'editeur Unity.

---

## Fonctionnalites implementees

### Core gameplay

| Fonctionnalite | Etat | Notes |
|----------------|------|-------|
| Grille 2D (GridModel + CellData) | DONE | Pure C#, pas de dependance Unity |
| Placement de mines (random) | DONE | Fisher-Yates shuffle, first-click safety |
| Placement de mines (level design) | DONE | Via `LevelDataSO` + `CellTag.Mine` |
| Calcul d'adjacence | DONE | 8 voisins, classique |
| Reveal single cell | DONE | `MinesweeperLogic.Reveal()` |
| Flood fill (BFS iteratif) | DONE | Borne par zone eclairee (`light > 0`), bloque aux mines |
| Toggle flag (clic droit) | DONE | Pas de cout en HP |
| Verification de victoire | DONE | `RevealedCount == total - MineCount` |
| Systeme de HP | DONE | Chaque clic gauche coute 1 HP |
| Machine d'etat | DONE | `WaitingForFirstClick → Playing → Won / Lost` |

### Fog of War & eclairage

| Fonctionnalite | Etat | Notes |
|----------------|------|-------|
| LightBrush (propagation BFS) | DONE | Mines et inactifs bloquent. Distance euclidienne. Max-blend |
| Lightmap texture (FogOfWarManager) | DONE | 1px/cellule, bilinear, clamp |
| Shader FogOfWar (background plane) | DONE | World-space UVs via `_DSGridBounds` global |
| Shader CellOverlay (quads) | DONE | Brightness-driven alpha + bordure UV-based |
| SparseLightGrid (Point Lights) | DONE | 1 light par bloc NxN, intensite = moyenne du bloc |
| Global shader variable `_DSGridBounds` | DONE | Bypasse CBUFFER, aligne lightmap → monde |

### Presentation & UX

| Fonctionnalite | Etat | Notes |
|----------------|------|-------|
| Grille de quads 3D (XZ plane) | DONE | PrimitiveType.Quad, rotation 90° X |
| BackgroundPlane persistant | DONE | Objet scene, pas cree au runtime |
| TextMeshPro 3D pour chiffres | DONE | Couleurs classiques Minesweeper |
| Camera orthographique top-down | DONE | `autoFitCamera` optionnel (desactive par defaut) |
| HUD UGUI (HP, mines, status) | DONE | `SweeperHUD.cs` |
| Input System (nouveau) | DONE | `UnityEngine.InputSystem` package |
| Clic bloque dans le noir | DONE | `cell.light <= 0 → return` |
| Entry point (debut de partie) | DONE | `CellTag.Entry` ou centre de la grille |
| Hover feedback : bordure blanche (cliquable) | DONE | `_BorderColor` dans CellOverlay.shader |
| Hover feedback : carre rouge (non-cliquable) | DONE | Fill rouge + emission visible dans le noir |
| Cellules non revelees semi-transparentes | DONE | Alpha 0.35 pour voir le fond a travers |
| Live Inspector : `cellSize`, `quadScale`, `gridOffset` | DONE | `LateUpdate` + `OnValidate` pattern |
| Editor gizmos (grille en scene view) | DONE | `OnDrawGizmos` dans GridRenderer |

### Level Design tools

| Fonctionnalite | Etat | Notes |
|----------------|------|-------|
| `LevelDataSO` (ScriptableObject) | DONE | Grille de `CellTag[]`, texture de fond, `cellSize` |
| `LevelPainterTool` (EditorTool) | DONE | Peinture clic/drag en Scene View, raccourcis clavier 1-5 |
| `LevelDataSOEditor` (custom Inspector) | DONE | Init/resize, stats, bouton "Open Painter" |
| `SceneSetup` (creation de scene) | DONE | Menu item, cree la scene complete, confirmation destructive |
| CellTags supportes | DONE | Empty, Mine, Inactive, Wall, Special, Entry |
| Preview texture de fond dans le Painter | DONE | GL calls, UVs corriges pour rotation 180° |
| Affichage "M" et "E" sur cellules taggees | DONE | Handles.Label dans le Painter |

---

## Problemes resolus

| Probleme | Cause | Solution |
|----------|-------|----------|
| Input System `InvalidOperationException` | Unity 6 desactive Legacy Input par defaut | Migration vers `UnityEngine.InputSystem` |
| Pas de lumiere visible | `lightRange` trop faible, ambient light | Augmente range, desactive ambient |
| Background plane pas eclaire | Materiau standard incompatible | Shader custom `FogOfWar.shader` |
| Cellules revelees cachent le fond | Quad opaque reste visible | `ShowQuad(false)` pour les cellules revelees |
| Grille desalignee avec le fond | `cellSize` hardcode | `[SerializeField]` avec live update |
| Dimming avec plusieurs clics | URP Forward : 8 lights max par pixel | Passe en **Forward+** (pas de limite) |
| Tonemapping ecrase les couleurs | Neutral Tonemapping actif par defaut | Desactive dans le Volume Profile |
| `RevealInRadius` = defaite sur toute mine | Ancien systeme revelait tout dans le rayon | Refactore vers flood fill classique Minesweeper |
| Scene wipe accidentelle | `EditorSceneManager.NewScene(EmptyScene)` | Ajoute `DisplayDialog` de confirmation |
| Texture de fond a 180° | Rotation du plan par defaut | `Euler(90, 180, 0)` + flip UVs GL preview |
| Camera se repositionne au lancement | `FitCamera()` appele inconditionnellement | Ajoute toggle `autoFitCamera = false` |
| Lightmap ignore les mines (geometrique) | `LightBrush` utilisait un cercle simple | Refactore en BFS avec blocage aux mines/inactifs |
| Lightmap inversee (clic bas → lumiere haut) | Flip de pixels dans FogOfWarManager | **Supprime le flip**, passe en world-space pur |
| Lightmap etiree/decalee | `_LightmapTex_ST` / CBUFFER mal aligne | **`Shader.SetGlobalVector`** bypasse le CBUFFER |
| Lightmap regression (1 seule cellule) | Shader world-space avec properties CBUFFER | Simplifie : `_DSGridBounds` global hors CBUFFER |

---

## Problemes connus / a investiguer

| Probleme | Priorite | Notes |
|----------|----------|-------|
| La lightmap pourrait ne pas etre parfaitement alignee sur certaines tailles de grille/plan | Moyenne | Le systeme world-space est robuste mais a valider sur differentes configs |
| Performance avec grilles tres grandes (>50x50) | Basse | 2500+ quads + materials instancies + SparseLights. Pas teste au-dela de 50x30 |
| Pas de pooling des quads au restart | Basse | `DestroyGrid()` + `CreateGrid()` a chaque restart |

---

## Modifications au module Sweeper (pour integration Mines)

| Fichier modifie | Changement | Impact |
|----------------|------------|--------|
| `SweeperGameController.cs` | Ajout events `OnGridReady`, `OnLeftClickMine`, `OnRightClickMine` | Le controller fire des events au lieu de gerer directement les mines |
| `SweeperGameController.cs` | Ajout accesseurs `Grid`, `CurrentHP`, `MaxHP`, `CurrentState`, `Config` | Permet au module Mines de lire l'etat du jeu |
| `SweeperGameController.cs` | Ajout `ApplyHPDelta(int delta)` | Permet au combat/rewards de modifier les HP |
| `SweeperGameController.cs` | Ajout `BuffCombatsRemaining` (int) | Tracking du buff "prochains combats renforces" |
| `SweeperGameController.cs` | Clic gauche sur mine → `OnLeftClickMine` (plus de defaite) | Changement fondamental de gameplay |
| `SweeperGameController.cs` | Clic droit sur mine eclairee → `OnRightClickMine` (pas de flag toggle) | Mines eclairees sont des points d'interet |
| `InputHandler.cs` | Ajout `inputBlocked` (bool public) | Bloque tous les clics quand panel modal ouvert |
| `CellView.cs` | Ajout `ShowMineResolved()` | Affiche checkmark + dimming apres resolution |

**Volume de changements** : ~35 lignes ajoutees au module Sweeper. Aucune logique existante supprimee, seulement re-routee.

---

## Module Mine Events (nouveau)

### Core Mine Event System

| Fonctionnalite | Etat | Notes |
|----------------|------|-------|
| Data Layer (enums, classes pures C#) | DONE | `Mines/Data/` — MineEventType, MineState, RewardType, PlayerChoice, MineEventData, RunEvent, RunLog |
| Combat Logic (resolution deterministe) | DONE | `Mines/Logic/CombatLogic.cs` — F vs Fc, echanges auto-resolus, penalite x2 |
| Mine Event Logic (assignment + resolution) | DONE | `Mines/Logic/MineEventLogic.cs` — AssignEvents, GetInteraction, ResolveChest/Dialogue/Shrine |
| Reward Logic | DONE | `Mines/Logic/RewardLogic.cs` — HP_GAIN, VISION_GAIN, BUFF |
| MineDistributionSO (config gameplay) | DONE | `Mines/Flow/MineDistributionSO.cs` — poids fallback, playerForce |
| Encounter SOs (4 types) | DONE | `CombatEncounterSO`, `ChestEncounterSO`, `DialogueEncounterSO`, `ShrineEncounterSO` — templates individuels |
| EncounterPoolSO (pool reutilisable) | DONE | `Mines/Flow/EncounterPoolSO.cs` — groupes d'encounters par type, ref par LevelDataSO |
| MineEventController (orchestrateur) | DONE | `Mines/Flow/MineEventController.cs` — gere le dictionnaire, tire du pool, route les events |
| MineEventPanel (UI modale UGUI) | DONE | `Mines/Presentation/MineEventPanel.cs` — panel unique data-driven, construit au runtime |
| MineEventSetup (editor script) | DONE | `Mines/Editor/MineEventSetup.cs` — menu non-destructif, cree assets + GO + cable tout |
| Integration SweeperGameController | DONE | Events `OnGridReady`, `OnLeftClickMine`, `OnRightClickMine`, `ApplyHPDelta`, `BuffCombatsRemaining` |
| Integration InputHandler | DONE | `inputBlocked` bool pour bloquer les clics pendant un panel modal |
| Integration CellView | DONE | `ShowMineResolved()` — indicateur visuel mine resolue (checkmark) |
| Integration LevelDataSO | DONE | Champ `encounterPool` pour lier le pool d'encounters au niveau |

### Comportements implementes

| Comportement | Etat | Notes |
|-------------|------|-------|
| Clic droit sur mine eclairee → interaction | DONE | Ouvre le panel modal avec choix |
| Clic droit sur non-mine → toggle flag | DONE | Comportement existant preserve |
| Clic gauche sur mine combat → x2 degats | DONE | Penalite puis combat auto-resolu |
| Clic gauche sur mine non-combat → destruction | DONE | Interaction perdue, mine marquee resolue |
| 4 types d'evenements (Combat, Chest, Dialogue, Shrine) | DONE | Chacun avec ses choix et effets |
| Logging RunEvent | DONE | Choix, HP avant/apres, type, coords |
| Mines invisibles (deduction par chiffres) | DONE | Pas d'icone visible avant interaction |
| Mine reste mine apres resolution | DONE | Bloque toujours lumiere/flood fill |

### Audio

| Fonctionnalite | Etat | Notes |
|----------------|------|-------|
| MusicManager (singleton auto-bootstrap) | DONE | `DontDestroyOnLoad`, deux AudioSources A/B pour crossfade |
| MusicConfigSO (config scene → clip) | DONE | ScriptableObject, menu `DarkSweeper/Music Config` |
| Crossfade entre scenes | DONE | Fade out/in configurable, no-op si meme clip, `Time.unscaledDeltaTime` |
| SFXManager (singleton auto-bootstrap) | DONE | `DontDestroyOnLoad`, pool de 8 AudioSources round-robin |
| SFXLibrarySO (catalogue id → clip) | DONE | ScriptableObject, menu `DarkSweeper/SFX Library` |
| API double : `Play(clip)` + `Play(id)` | DONE | Pluggable par reference directe ou par ID string |
| Prefabs dans Resources/ | A FAIRE | Creer manuellement dans l'editeur Unity |
| Assets SO (MusicConfig, SFXLibrary) | A FAIRE | Creer manuellement, assigner les clips |

---

## Prochaines etapes possibles

| Tache | Priorite | Complexite |
|-------|----------|------------|
| Ouvrir la scene et executer DarkSweeper > Add Mine Event System | Haute | Faible |
| Tester end-to-end les 5 scenarios d'interaction | Haute | Faible |
| Creer des encounter SOs supplementaires pour varier le contenu | Moyenne | Faible |
| Integrer le module ChatGPT (dieu questionnaire → generation de niveau) | Haute | Moyenne |
| Systeme d'eveil (awakening) — scaling de difficulte | Haute | Moyenne |
| Animer la propagation de lumiere (lerp progressif) | Moyenne | Faible |
| Effets visuels sur defaite/victoire | Moyenne | Faible |
| Creer prefabs Audio (MusicManager + SFXManager) dans Resources/ | Haute | Faible |
| Creer assets SO audio (MusicConfig + SFXLibrary) dans Data/ | Haute | Faible |
| Brancher les SFX sur les interactions (clic, reveal, flag, defaite, victoire) | Moyenne | Faible |
| Tutoriel in-game | Moyenne | Moyenne |
| Optimiser : pooling de quads, GPU instancing | Basse | Moyenne |
| Support multi-niveaux / progression | Haute | Moyenne |
| Animations de reveal (tiles qui se retournent) | Basse | Moyenne |

---

## Historique des sessions

### Session 1 — Setup initial
- Architecture 3 couches definie
- `CellData`, `GridModel`, `MinesweeperLogic`, `LightBrush` implementes
- `SweeperGameController`, `SweeperConfig`, `GridRenderer`, `CellView`, `InputHandler`, `SweeperHUD` crees
- Rendu 2D avec SpriteRenderers (abandonne ensuite)

### Session 2 — Rework 3D
- Migration du rendu vers des Quads 3D (XZ plane) avec MeshRenderer
- Shaders custom URP : `CellOverlay.shader`, `FogOfWar.shader`
- Camera orthographique top-down
- TextMeshPro 3D pour les chiffres d'adjacence
- SparseLightGrid pour eclairage additionnel
- URP configure en Forward+ avec Tonemapping desactive

### Session 3 — Level Design & Editor Tools
- `LevelDataSO` remplace `MineLayoutSO` (format texte → grille structuree)
- `LevelPainterTool` : outil de peinture Scene View
- `LevelDataSOEditor` : Inspector custom
- `SceneSetup` : creation de scene automatisee
- BackgroundPlane devenu un objet scene persistant

### Session 4 — Lightmap & Game Rules
- Lightmap texture system (1px/cellule) via `FogOfWarManager`
- Multiples iterations sur l'alignement lightmap :
  - Flip de pixels ❌
  - `_LightmapTex_ST` CBUFFER ❌
  - World-space `_GridWorldMin`/`_GridWorldSize` en CBUFFER ❌
  - **`Shader.SetGlobalVector("_DSGridBounds")` + world-space dans le shader ✅**
- `LightBrush` refactore en BFS (bloque aux mines/inactifs)
- `CellTag.Entry` pour le point d'entree
- `LightEntryPoint()` : eclairage initial + flood fill sans cout HP
- Blocage des clics dans le noir (`cell.light <= 0`)
- Hover feedback : bordure blanche (cliquable) / carre rouge (non-cliquable)
- Cellules non revelees rendues semi-transparentes (alpha 0.35)
- `autoFitCamera` toggle (desactive par defaut)

### Session 5 — Mine Events System
- Module `Mines/` cree en parallele du module `Sweeper/` (meme architecture 3 couches)
- Data Layer : 13 fichiers purs C# (enums, data classes, RunLog)
- Logic Layer : `CombatLogic` (combat deterministe F vs Fc), `MineEventLogic` (assignment + resolution), `RewardLogic`
- Flow Layer : `MineEventController` (orchestrateur), `MineDistributionSO`, `MineEventContentSO`
- Presentation Layer : `MineEventPanel` (panel UGUI modal unique, construit au runtime)
- Integration dans SweeperGameController :
  - Clic gauche sur mine : plus de defaite. Delegue au systeme mine event (penalite)
  - Clic droit sur mine eclairee : ouvre l'interaction au lieu de toggle flag
  - Events `OnGridReady`, `OnLeftClickMine`, `OnRightClickMine`
  - Accesseurs publics : `CurrentHP`, `Grid`, `ApplyHPDelta()`, `BuffCombatsRemaining`
- Integration InputHandler : `inputBlocked` pour bloquer les clics pendant panel modal
- Integration CellView : `ShowMineResolved()` indicateur visuel

### Session 5b — Encounter Pools + Scene Integration
- Refactor du systeme de contenu : remplacement du monolithique `MineEventContentSO` par des encounter SOs individuels
- 4 nouveaux types SO : `CombatEncounterSO`, `ChestEncounterSO`, `DialogueEncounterSO`, `ShrineEncounterSO`
- `EncounterPoolSO` : pool reutilisable groupant des encounters par type
- Lien level design : `LevelDataSO.encounterPool` reference le pool pour chaque niveau
- `MineEventLogic.DrawFromPool<T>()` : tirage shuffle + unique + repetitions si N > pool.Length
- `MineEventSetup.cs` : editor script (menu DarkSweeper > Add Mine Event System) qui en un clic :
  - Cree ~11 encounter SO d'exemple avec textes francais
  - Cree un EncounterPool_Default.asset et MineDistribution_Default.asset
  - Cree le GO MineEventSystem avec MineEventController + MineEventPanel
  - Auto-cable toutes les references (sweeper, inputHandler, gridRenderer, pool, distribution)
- `SceneSetup.cs` mis a jour pour inclure le systeme Mine dans les futures reconstructions de scene
- `MineEventContentSO` supprime (remplace par les encounter SOs)

### Session 6 — Systeme Audio (session actuelle)
- Module `Audio/` cree dans `Assets/Scripts/Audio/` (4 fichiers)
- `MusicManager.cs` : singleton auto-bootstrap (`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`), `DontDestroyOnLoad`, deux AudioSources A/B pour crossfade seamless
- `MusicConfigSO.cs` : ScriptableObject mappant scene → clip + volume, duree de fade configurable
- `SFXManager.cs` : singleton auto-bootstrap, pool de 8 AudioSources round-robin, API `Play(clip)` et `Play(id)`
- `SFXLibrarySO.cs` : ScriptableObject catalogue id string → clip + volume, lookup via `TryGet()`
- Design : independant de la scene de depart, meme pattern pour les deux managers
- Reste a faire : creer les prefabs dans `Resources/` et les assets SO dans `Data/` via l'editeur Unity
