# DarkSweeper — Architecture Technique

*(Etat actuel du projet — derniere mise a jour : 7 fevrier 2026)*

---

## 1. Vue d'ensemble

DarkSweeper est un puzzle roguelite qui combine Minesweeper avec un systeme de brouillard de guerre (Fog of War). Le joueur depense des HP pour cliquer, ce qui eclaire une zone autour du clic et revele les cellules adjacentes selon les regles classiques du demineur. Les cases mines ne sont pas des dangers mortels mais des **points d'interet** : chacune contient un evenement (Combat, Coffre, Dialogue, Shrine) que le joueur peut declencher via clic droit apres deduction.

**Moteur** : Unity 6 avec Universal Render Pipeline 17 (Forward+)

**Philosophie** : simplicite, performance, lisibilite, zero magie.

**Deux modules paralleles** :
- **Sweeper** (`Assets/Scripts/Sweeper/`) — grille, fog of war, eclairage, demineur
- **Mines** (`Assets/Scripts/Mines/`) — evenements sur les cases mines, combat, UI modale

Les deux modules suivent la meme architecture 3 couches et communiquent par C# events.

---

## 2. Architecture en 3 couches

```
[Data / Logic]  →  [Flow]  →  [Presentation]
   Pure C#         MonoBehaviour    Rendering + UI
```

Chaque module (Sweeper et Mines) suit ce pattern. Le module Mines est un **overlay** qui ne modifie pas les donnees du Sweeper — il s'y connecte par coordonnees `(x, y)` et par events.

### 2.1 Data — Donnees pures (`Assets/Scripts/Sweeper/Data/`)

| Fichier | Role |
|---------|------|
| `CellData.cs` | Etat d'une cellule : `hasMine`, `adjacentMines`, `isRevealed`, `isFlagged`, `light` (float 0-1), `isActive` |
| `GridModel.cs` | Conteneur 2D `CellData[width, height]` + compteurs globaux (`MineCount`, `FlagCount`, `RevealedCount`) |
| `CellTag.cs` | Enum pour le level design : `Empty(0)`, `Mine(1)`, `Inactive(2)`, `Wall(3)`, `Special(4)`, `Entry(5)` |

Aucune dependance Unity dans `CellData` et `GridModel`. Testable sans Play Mode.

### 2.2 Logic — Regles de jeu pures (`Assets/Scripts/Sweeper/Logic/`)

| Fichier | Role |
|---------|------|
| `MinesweeperLogic.cs` | Fonctions statiques : `PlaceMinesRandom`, `PlaceMinesFromLayout`, `ComputeAdjacency`, `Reveal`, `FloodFill` (BFS iteratif), `ToggleFlag`, `CheckVictory` |
| `LightBrush.cs` | Propagation de lumiere par BFS. Les mines et cellules inactives **bloquent** la propagation. Valeur basee sur distance euclidienne : pleine intensite dans `radiusFull`, falloff lineaire jusqu'a `radiusFull + radiusFalloff` |

**Points cles du FloodFill** :
- BFS iteratif (pas de recursion — pas de stack overflow)
- Borne par la zone eclairee (`cell.light > 0`)
- Les mines bloquent le flow
- Les cellules avec `adjacentMines > 0` sont revelees mais stoppent la propagation

**Points cles du LightBrush** :
- BFS depuis le centre du clic
- Les mines recoivent de la lumiere (lueur faible) mais ne propagent PAS
- Les cellules inactives bloquent egalement
- Blending `max()` : la lumiere ne diminue jamais une valeur existante

### 2.3 Flow — Orchestration (`Assets/Scripts/Sweeper/Flow/`)

| Fichier | Role |
|---------|------|
| `SweeperGameController.cs` | Boucle de jeu complete : init, routage des clics, machine d'etat (`WaitingForFirstClick → Playing → Won / Lost`) |
| `SweeperConfig.cs` | ScriptableObject de configuration : `gridWidth`, `gridHeight`, `mineCount`, `hpStart`, `revealRadius`, `lightFalloff` |
| `LevelDataSO.cs` | ScriptableObject de level design : grille de `CellTag[]`, texture de fond, `cellSize`. Remplace l'ancien `MineLayoutSO` textuel |
| `FogOfWarManager.cs` | Gere une `Texture2D` lightmap (1 pixel = 1 cellule). Pixel `(x,y)` = lumiere de la cellule `(x,y)`. Pas de flip |
| `SparseLightGrid.cs` | Grille parsemee de vrais Unity Point Lights (1 par bloc NxN de cellules). Intensite = moyenne du bloc. Pour eclairage 3D |

**Pipeline du clic gauche** (dans `SweeperGameController`) :
1. Verifier que la partie n'est pas finie
2. **Bloquer si `cell.light <= 0`** (pas de clic dans le noir)
3. Si premier clic (mode random) : placer les mines, calculer adjacence, **fire `OnGridReady`** (Mine Events s'initialisent)
4. Depenser 1 HP
5. **Si la cellule est une mine** → **fire `OnLeftClickMine(x, y, cell)`** au lieu de declencher la defaite. Le `MineEventController` gere la penalite :
   - Mine combat : combat auto-resolu avec degats x2
   - Mine non-combat : interaction detruite, le joueur ne recoit rien
6. `MinesweeperLogic.Discover()` → lumiere + reveal en un seul BFS
7. `SyncPresentationState()` → met a jour lightmap, quads, sparse lights en un seul appel
8. Verifier victoire

**Pipeline du clic droit** (dans `SweeperGameController`) :
1. Verifier que la partie est en cours (`Playing` ou `WaitingForFirstClick`)
2. **Si la cellule est une mine ET eclairee (`light > 0`)** → **fire `OnRightClickMine(x, y, cell)`**. Le `MineEventController` ouvre le panel d'interaction. Le toggle flag est **ignore**.
3. Sinon → toggle flag (comportement classique)

**Systeme d'entree (Entry Point)** :
- Au demarrage, `LightEntryPoint()` cherche une cellule `CellTag.Entry` dans le `LevelDataSO`
- Si aucune trouvee, eclaire le centre de la grille
- Applique un brush de lumiere + flood fill initial sans cout en HP

### 2.4 Presentation — Rendu 3D + UI (`Assets/Scripts/Sweeper/Presentation/`)

| Fichier | Role |
|---------|------|
| `GridRenderer.cs` | Cree les quads (XZ plane) pour chaque cellule. Reference un **BackgroundPlane persistant** dans la scene. Gere le layout live (Inspector). Envoie `_DSGridBounds` comme variable globale shader |
| `CellView.cs` | Composant par cellule : gestion du materiau instancie, couleurs, bordures de hover, visibilite du quad. Cache l'etat de cliquabilite pour le feedback hover |
| `InputHandler.cs` | Convertit la position souris en coordonnees grille (XZ plane). Fires `OnLeftClick`, `OnRightClick`, `OnHoverChanged`. Utilise le nouveau Input System (`UnityEngine.InputSystem`) |
| `SweeperHUD.cs` | Canvas UGUI overlay : HP (`PV: X/Y`), mines restantes, messages victoire/defaite, bouton restart |

### 2.5 Module Mines — Data (`Assets/Scripts/Mines/Data/`)

Tous les fichiers sont du **pure C#** sans dependance Unity.

| Fichier | Role |
|---------|------|
| `MineEventType.cs` | Enum : `Combat`, `Chest`, `Dialogue`, `Shrine` |
| `MineState.cs` | Enum : `Hidden`, `Revealed`, `Resolved` |
| `RewardType.cs` | Enum : `None`, `HpGain`, `VisionGain`, `Buff` |
| `PlayerChoice.cs` | Enum : `Engage`, `Open`, `Ignore`, `Help`, `Harm`, `Sacrifice`, `Refuse` |
| `MineEventData.cs` | Payload par mine : coords, type, etat, parametres specifiques au type, resultat de resolution |
| `CombatParams.cs` | Config combat : `creatureForce`, `creatureName`, `isElite`, reward |
| `ChestParams.cs` | Config coffre : `isTrapped`, `trapDamage`, reward |
| `DialogueParams.cs` | Config dialogue : `characterName`, `promptText`, `choices[]` |
| `ShrineParams.cs` | Config shrine : `description`, `sacrificeCost`, reward |
| `InteractionDescriptor.cs` | DTO envoye a l'UI : titre, description, liste de choix textuels |
| `ResolutionResult.cs` | Resultat : `hpDelta`, `reward`, `rewardValue`, `resultText`, `playerDied` |
| `RunEvent.cs` | Entree de log : type, coords, choix, HP avant/apres (pour narration/dieux) |
| `RunLog.cs` | Liste append-only de `RunEvent`, queryable par type ou par coordonnees |

### 2.6 Module Mines — Logic (`Assets/Scripts/Mines/Logic/`)

Fonctions statiques pures, testables sans Unity.

| Fichier | Role |
|---------|------|
| `CombatLogic.cs` | Resolution deterministe : Force joueur (F) vs Force creature (Fc). Echanges auto-resolus en boucle. Si `isLeftClickPenalty` : degats x2. Retourne un `ResolutionResult` |
| `MineEventLogic.cs` | `AssignEvents()` : distribue aleatoirement les 4 types sur les mines selon les poids du SO. `GetInteraction()` : construit un `InteractionDescriptor` pour l'UI. `ResolveChest/Dialogue/Shrine()` : calculent les `ResolutionResult` |
| `RewardLogic.cs` | `ApplyReward()` : applique les effets au `SweeperGameController` — `HpGain` (via `ApplyHPDelta`), `VisionGain` (via `Config.revealRadius`), `Buff` (via `BuffCombatsRemaining`) |

**Combat deterministe** :
- Le joueur a une Force (F), la creature une Force (Fc)
- Chaque echange : creature perd F PV, joueur perd Fc PV
- Boucle jusqu'a ce qu'un des deux tombe a 0
- Si clic gauche (penalite) : les degats subis par le joueur sont doubles

### 2.7 Module Mines — Flow (`Assets/Scripts/Mines/Flow/`)

| Fichier | Role |
|---------|------|
| `MineEventController.cs` | MonoBehaviour orchestrateur. Gere un `Dictionary<(int,int), MineEventData>`. S'abonne a `OnGridReady`, `OnLeftClickMine`, `OnRightClickMine`. Delegue aux Logic et a `MineEventPanel` |
| `MineDistributionSO.cs` | ScriptableObject : poids par type d'evenement (fallback), `playerForce` |
| `CombatEncounterSO.cs` | ScriptableObject : template d'un combat (`monsterName`, `creatureForce`, `isElite`, `reward`) |
| `ChestEncounterSO.cs` | ScriptableObject : template d'un coffre (`description`, `isTrapped`, `trapDamage`, `reward`) |
| `DialogueEncounterSO.cs` | ScriptableObject : template d'un dialogue (`characterName`, `promptText`, `choices[]` avec enums directs) |
| `ShrineEncounterSO.cs` | ScriptableObject : template d'un shrine (`shrineDescription`, `offerText`, `sacrificeCost`, `reward`) |
| `EncounterPoolSO.cs` | ScriptableObject : pool reutilisable regroupant des encounters par type (`combatPool[]`, `chestPool[]`, `dialoguePool[]`, `shrinePool[]`). Reference par `LevelDataSO` |

**Pipeline d'un clic droit sur mine (interaction complete)** :
1. `SweeperGameController.OnRightClickMine` fire
2. `MineEventController.HandleRightClickMine()` recoit les coords
3. Lookup dans le dictionnaire → `MineEventData`
4. Si `Resolved` → ignore (fall-through vers flag toggle, mais bloque car event consomme)
5. `MineEventLogic.GetInteraction(data)` → `InteractionDescriptor`
6. `MineEventPanel.Show(descriptor, callback)` → panel modal s'affiche, `inputBlocked = true`
7. Joueur clique un choix → `OnPlayerChoice(index)` callback
8. Controller resout via `CombatLogic` ou `MineEventLogic` → `ResolutionResult`
9. `RewardLogic.ApplyReward()` si applicable
10. `RunLog.Add(event)` pour le tracking narration/dieux
11. `MineEventPanel.ShowResult()` → affiche le texte resultat
12. Joueur clique Continuer → `data.state = Resolved`, `inputBlocked = false`
13. `CellView.ShowMineResolved()` → checkmark sur la cellule

### 2.8 Module Mines — Presentation (`Assets/Scripts/Mines/Presentation/`)

| Fichier | Role |
|---------|------|
| `MineEventPanel.cs` | Panel UGUI modal unique, **construit au runtime** si pas assigne. Data-driven : recoit un `InteractionDescriptor`, genere titre + description + boutons de choix dynamiques. Deux etats : choix (boutons visibles) et resultat (texte + bouton Continuer) |

**Integration UI** :
- Le panel est centre en overlay Canvas
- `InputHandler.inputBlocked = true` pendant toute la duree d'affichage
- Les boutons sont generes dynamiquement (1 par choix dans le descripteur)
- Apres resolution, le panel passe en mode "resultat" avec un seul bouton Continuer

### 2.9 Communication inter-modules (Events)

```
SweeperGameController                    MineEventController
  │                                           │
  ├─ OnGridReady(GridModel) ───────────────►  HandleGridReady()
  │                                           │  → AssignEvents() sur toutes les mines
  │                                           │
  ├─ OnLeftClickMine(x, y, cell) ──────────►  HandleLeftClickMine()
  │                                           │  → Penalite + resolution auto
  │                                           │
  ├─ OnRightClickMine(x, y, cell) ─────────►  HandleRightClickMine()
  │                                           │  → Panel modal → choix → resolution
  │                                           │
  ◄─ ApplyHPDelta(delta) ◄──────────────────  RewardLogic / CombatLogic
  ◄─ BuffCombatsRemaining++ ◄───────────────  RewardLogic (buff)
  │                                           │
  InputHandler                               MineEventPanel
  ├─ inputBlocked = true ◄──────────────────  Show()
  ├─ inputBlocked = false ◄─────────────────  Hide()
  │                                           │
  CellView                                    │
  ├─ ShowMineResolved() ◄──────────────────  FinalizeResolution()
```

---

## 3. Rendu 3D et Fog of War

### 3.1 Architecture du rendu

```
[Camera Orthographique (top-down, rotation 90° X)]
         ↓
[BackgroundPlane]  ← Quad persistant dans la scene, y=-0.01
   Material: FogOfWar.shader (background texture * lightmap)
         ↓
[Cell Quads]  ← 1 Quad par cellule, y=0, rotation 90° X
   Material: CellOverlay.shader (couleur + bordure + emission)
         ↓
[TextMeshPro 3D]  ← Chiffres d'adjacence, enfant de chaque quad
         ↓
[Point Lights (sparse)]  ← 1 par bloc NxN, y=3, eclairage additionnel
```

### 3.2 BackgroundPlane (objet scene persistant)

- `GameObject.CreatePrimitive(PrimitiveType.Quad)` nomme "BackgroundPlane"
- Rotation : `Quaternion.Euler(90f, 180f, 0f)` (face vers le haut en XZ, 180° Y pour corriger l'orientation UV)
- Position : `(0, -0.01, 0)` (juste sous les quads de cellules)
- En editeur : materiau `BackgroundEditor.mat` (Unlit) pour voir la texture
- Au runtime : materiau instancie depuis `FogOfWar.mat` (shader custom URP)

### 3.3 Shaders custom

**`FogOfWar.shader`** (pour le BackgroundPlane) :
- Properties : `_MainTex` (texture de fond), `_LightmapTex` (lightmap), `_FogColor`, `_LitTint`
- CBUFFER contient uniquement `_MainTex_ST`, `_FogColor`, `_LitTint`
- **`_DSGridBounds`** : variable globale shader (set via `Shader.SetGlobalVector`), bypasse le CBUFFER
  - `xy` = coin min de la grille en world XZ
  - `zw` = taille de la grille en world units
- Le fragment shader calcule les UVs lightmap depuis la position monde : `lmUV = (positionWS.xz - _DSGridBounds.xy) / _DSGridBounds.zw`
- Hors grille = completement noir
- Interpolation : `lerp(_FogColor, mainColor * _LitTint, lightValue)`

**`CellOverlay.shader`** (pour les quads de cellules) :
- Properties : `_BaseColor`, `_Brightness`, `_EmissionColor`, `_BorderColor`, `_BorderWidth`
- Transparent (Blend SrcAlpha OneMinusSrcAlpha, ZWrite Off)
- Detection de bordure par UV : `min(uv, 1-uv)` < `_BorderWidth` avec `smoothstep` anti-aliase
- Alpha pilote par `_Brightness` (0 = invisible, 1 = visible)
- Emission visible meme dans le noir total (pour le hover rouge "interdit")

### 3.4 Systeme de lightmap

La lightmap est une `Texture2D` basse resolution (1 pixel = 1 cellule) :
1. `FogOfWarManager` cree la texture (`TextureFormat.RGBA32`, bilinear, clamp)
2. Le pixel `(x,y)` stocke directement la valeur lumiere de la cellule `(x,y)`. **Pas de flip**
3. `GridRenderer.SetGlobalGridBounds()` envoie les bornes monde via `Shader.SetGlobalVector("_DSGridBounds", ...)`
4. Le shader `FogOfWar` calcule les UVs lightmap en espace monde → completement independant de la rotation du plan

### 3.5 Feedback hover interactif

| Etat de la cellule | Hover ON | Hover OFF |
|---------------------|----------|-----------|
| **Cliquable** (eclairee, non revelee, active, pas de drapeau) | Bordure **blanche brillante** (alpha 0.9) | Bordure blanche subtile (alpha 0.5) |
| **Non-cliquable** (noir, revelee, inactive, drapeau) | **Carre rouge rempli** (alpha 0.55) + bordure rouge + emission rouge | Invisible ou bordure grise |

Le `CellView` cache l'etat `isClickable` a chaque `UpdateVisual()` et applique les couleurs correctes dans `ApplyColors()`.

---

## 4. Editor Tools (`Assets/Scripts/Sweeper/Editor/`)

| Fichier | Role |
|---------|------|
| `SceneSetup.cs` | Menu `DarkSweeper > Create DarkSweeper Scene` : cree la scene, le BackgroundPlane persistant, le SweeperGameController, les composants, configure URP |
| `LevelPainterTool.cs` | `EditorTool` custom pour peindre les cellules dans la Scene View. Brush : Empty, Mine, Inactive, Wall, Special, Entry. Raccourcis clavier (1-5). Preview de la texture de fond via GL. Affiche "M" et "E" sur les cellules taggees |
| `LevelDataSOEditor.cs` | Inspector custom pour `LevelDataSO` : init/resize grille, stats (mines, inactives, entry), bouton "Open Painter" |
| `SweeperSetup.cs` | Utilitaire complementaire de setup |

---

## 5. Arborescence des fichiers

```
Assets/
  Scripts/Sweeper/
    Data/
      CellData.cs                 ← Etat par cellule (pure C#)
      CellTag.cs                  ← Enum de tags level design
      GridModel.cs                ← Conteneur grille 2D (pure C#)
    Logic/
      MinesweeperLogic.cs         ← Regles du demineur (statique)
      LightBrush.cs               ← Propagation de lumiere BFS
    Flow/
      SweeperGameController.cs    ← Boucle de jeu
      SweeperConfig.cs            ← SO config (dimensions, HP, radii)
      LevelDataSO.cs              ← SO level design (cellules, texture, cellSize)
      FogOfWarManager.cs          ← Gestion de la texture lightmap
      SparseLightGrid.cs          ← Grille parsemee de Point Lights
    Presentation/
      GridRenderer.cs             ← Rendu de la grille 3D + grid bounds
      CellView.cs                 ← Visuel par cellule (shader-driven)
      InputHandler.cs             ← Souris → coordonnees grille
      SweeperHUD.cs               ← UI overlay UGUI
    Editor/
      SceneSetup.cs               ← Creation de scene automatisee
      LevelPainterTool.cs         ← Outil de peinture de niveaux
      LevelDataSOEditor.cs        ← Inspector custom pour LevelDataSO
      SweeperSetup.cs             ← Setup complementaire
  Art/Sweeper/
    Shaders/
      FogOfWar.shader             ← Shader URP pour le plan de fond
      CellOverlay.shader          ← Shader URP pour les quads de cellules
    Materials/
      FogOfWar.mat                ← Materiau runtime (fog)
      CellOverlay.mat             ← Materiau de base (instancie par cellule)
      CellBase.mat                ← Materiau alternatif
      BackgroundEditor.mat        ← Materiau Unlit pour preview editeur
    Maps/                         ← Images de fond des niveaux
    Sprites/                      ← Sprites legacy (cell_*, fog_overlay, etc.)
  Scripts/Mines/
    Data/
      MineEventType.cs            ← Enum (Combat, Chest, Dialogue, Shrine)
      MineState.cs                ← Enum (Hidden, Revealed, Resolved)
      RewardType.cs               ← Enum (None, HpGain, VisionGain, Buff)
      PlayerChoice.cs             ← Enum (Engage, Open, Ignore, Help, Harm, Sacrifice, Refuse)
      MineEventData.cs            ← Payload par mine (coords, type, params, resolution)
      CombatParams.cs             ← Config combat (creatureForce, isElite, reward)
      ChestParams.cs              ← Config coffre (isTrapped, trapDamage, reward)
      DialogueParams.cs           ← Config dialogue (characterName, promptText, choices)
      ShrineParams.cs             ← Config shrine (description, sacrificeCost, reward)
      InteractionDescriptor.cs    ← Ce que l'UI recoit pour afficher
      ResolutionResult.cs         ← Resultat d'une resolution (hpDelta, reward, text)
      RunEvent.cs                 ← Entree de log (pour narration/dieux)
      RunLog.cs                   ← Liste append-only de RunEvent
    Logic/
      CombatLogic.cs              ← Resolution deterministe F vs Fc (statique)
      MineEventLogic.cs           ← Assignment + resolution des 4 types (statique)
      RewardLogic.cs              ← Application des rewards (statique)
    Flow/
      MineEventController.cs      ← Orchestrateur MonoBehaviour
      MineDistributionSO.cs       ← SO config distribution (poids fallback, playerForce)
      CombatEncounterSO.cs        ← SO template combat individuel
      ChestEncounterSO.cs         ← SO template coffre individuel
      DialogueEncounterSO.cs      ← SO template dialogue individuel
      ShrineEncounterSO.cs        ← SO template shrine individuel
      EncounterPoolSO.cs          ← SO pool reutilisable (arrays par type)
    Presentation/
      MineEventPanel.cs           ← Panel UGUI modal (construit au runtime)
    Editor/
      MineEventSetup.cs           ← Menu "Add Mine Event System" (non-destructif)
  Data/
    SweeperConfig_Default.asset   ← Config par defaut
    MineDistribution_Default.asset ← Config distribution mines (cree par setup)
    EncounterPool_Default.asset   ← Pool d'encounters par defaut (cree par setup)
    LVL1_Data.asset               ← Niveau 1 (LevelDataSO)
    Encounters/                   ← Encounter SOs individuels (crees par setup)
    Levels/
      Level_Tutorial.asset        ← Niveau tutoriel
      Level_Test_5x5.asset        ← Niveau test 5x5
```

---

## 6. Choix techniques et justifications

| Composant | Choix | Justification |
|-----------|-------|---------------|
| Rendu | Quads 3D (PrimitiveType.Quad) dans le plan XZ | Controle total par cellule, compatible URP et Point Lights |
| Camera | Orthographique top-down (rotation 90° X) | Vue de dessus parfaite pour un demineur |
| Fog of War | Texture lightmap (1px/cellule) + shader world-space | Performant, pas de limite de lights, aligne automatiquement |
| Lightmap UV mapping | `Shader.SetGlobalVector("_DSGridBounds")` + world-space calc dans le shader | Bypasse les problemes de CBUFFER/SRP Batcher, independant de la rotation du plan |
| Eclairage 3D | SparseLightGrid (1 Point Light par bloc NxN) + Forward+ renderer | Evite la limite de 256 lights, Forward+ = no per-pixel limit |
| Input | `UnityEngine.InputSystem` package | Requis par Unity 6 (Legacy Input desactive par defaut) |
| Propagation lumiere | BFS (pas geometrique) | Respecte les regles du jeu : mines et inactifs bloquent |
| Level design | `LevelDataSO` + `LevelPainterTool` (EditorTool) | Visuel, rapide, extensible vs l'ancien format texte |
| Logique | Pure C# statique | Testable, decouple du rendu |
| Config | ScriptableObject | Editable dans l'Inspector, presets par drag-and-drop |
| Communication | C# events (`System.Action<T>`) | Decoupage propre inter-systemes |
| Mine Events — stockage | `Dictionary<(int,int), MineEventData>` overlay | Pas de modification du `CellData`/`GridModel` existant |
| Mine Events — UI | Panel UGUI modal unique, construit au runtime | Pas besoin de prefab, data-driven, flexible |
| Mine Events — combat | Resolution deterministe (boucle F vs Fc) | Pas de choix joueur en combat, simple et previsible |
| Mine Events — integration | Events C# (3 events) + ~35 lignes dans Sweeper | Invasion minimale du module existant stable |
| Mine Events — contenu | Encounter SOs individuels + `EncounterPoolSO` | Chaque encounter est un asset editable, drag-drop dans un pool reference par le niveau |
| Mine Events — tirage | `DrawFromPool<T>()` : shuffle + unique + repetitions si besoin | Chaque SO utilise au moins une fois, variete maximale |
| Mine Events — scene setup | `MineEventSetup.cs` : menu editor non-destructif | Cree assets + GO + cable tout en un clic |

---

## 7. Systeme Audio (`Assets/Scripts/Audio/`)

Le systeme audio est compose de deux singletons independants, chacun auto-bootstrap via `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`. Ils s'instancient depuis un prefab dans `Resources/` **avant le chargement de la premiere scene**, quel que soit le point d'entree. Les deux persistent via `DontDestroyOnLoad`.

### 7.1 MusicManager — Musique de fond

| Fichier | Role |
|---------|------|
| `MusicManager.cs` | Singleton MonoBehaviour. Deux AudioSources (A/B) pour crossfade. Ecoute `SceneManager.sceneLoaded`, lookup dans le SO config, lance un crossfade si le clip change |
| `MusicConfigSO.cs` | ScriptableObject : tableau `SceneMusicEntry[]` (sceneName, clip, volume) + `fadeDuration`. Menu : `DarkSweeper/Music Config` |

**Fonctionnement** :
1. Auto-bootstrap : `Resources.Load<MusicManager>("MusicManager")` → `Instantiate` → `DontDestroyOnLoad`
2. A chaque changement de scene, `OnSceneLoaded` consulte `MusicConfigSO.GetClipForScene(scene.name)`
3. Si pas de mapping → la musique actuelle continue (pas d'interruption)
4. Si meme clip deja en lecture → no-op (ex: Intro → Questionnaire = meme musique)
5. Si clip different → crossfade : source sortante fade out, source entrante fade in, duree configurable
6. Crossfade utilise `Time.unscaledDeltaTime` (fonctionne meme si `Time.timeScale == 0`)

**AudioSources** : `loop = true`, `playOnAwake = false`, `spatialBlend = 0` (2D), `priority = 0` (jamais cull)

### 7.2 SFXManager — Effets sonores

| Fichier | Role |
|---------|------|
| `SFXManager.cs` | Singleton MonoBehaviour. Pool de N AudioSources (defaut 8) en round-robin pour sons one-shot superposables |
| `SFXLibrarySO.cs` | ScriptableObject : tableau `SFXEntry[]` (id string, clip, volume). Lookup par `TryGet(id)`. Menu : `DarkSweeper/SFX Library` |

**Deux facons de jouer un son** :
- Par ID depuis la bibliotheque : `SFXManager.Instance.Play("cell_reveal")`
- Par reference directe : `SFXManager.Instance.Play(monClip, 0.5f)`

**Pool** : Les AudioSources utilisent `PlayOneShot` (plusieurs sons simultanes sur une meme source). Le round-robin repartit la charge sur N sources pour eviter la saturation en cas de clics rapides.

**AudioSources** : `loop = false`, `playOnAwake = false`, `spatialBlend = 0` (2D), `priority = 128` (la musique a priorite 0, les SFX cedent en cas de saturation)

### 7.3 Arborescence Audio

```
Assets/Scripts/Audio/
  MusicManager.cs           ← Singleton musique, crossfade, sceneLoaded
  MusicConfigSO.cs          ← SO config scene → clip musique
  SFXManager.cs             ← Singleton SFX, pool AudioSources, round-robin
  SFXLibrarySO.cs           ← SO catalogue id → clip SFX
Assets/Resources/
  MusicManager.prefab       ← Prefab auto-instancie (manuellement cree)
  SFXManager.prefab         ← Prefab auto-instancie (manuellement cree)
Assets/Data/
  MusicConfig.asset         ← Instance MusicConfigSO (scene → clip)
  SFXLibrary.asset          ← Instance SFXLibrarySO (id → clip)
Assets/SFX/
  music/                    ← Fichiers MP3 musique
```

### 7.4 Setup Unity Editor

Pour chaque manager (Music et SFX) :
1. Creer l'asset SO via `Assets > Create > DarkSweeper > [Music Config | SFX Library]`
2. Remplir les entrees dans l'Inspector
3. Creer un GameObject vide, ajouter le composant manager, assigner le SO
4. Drag le GameObject dans `Assets/Resources/` pour creer le prefab (nom exact : `MusicManager` ou `SFXManager`)
5. Supprimer l'instance de la scene — le bootstrap fait le reste

---

## 8. URP Configuration

- **Render Pipeline Asset** : `PC_RPAsset.asset`
  - Additional Lights : `PerPixel`
- **Renderer Data** : `PC_Renderer.asset`
  - Rendering Path : **Forward+** (pas de limite per-pixel d'additional lights)
- **Volume Profile** : Tonemapping desactive (sinon les couleurs sont compressees)
- **Ambient Light** : desactive ou tres faible (le noir total est voulu)
- **Per-cell lights** : `shadows = LightShadows.None` (performance)
