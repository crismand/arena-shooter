using System;
using System.IO;
using AS.Combat;
using AS.Core;
using AS.Enemies;
using AS.Player;
using AS.UI;
using AS.Waves;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AS.EditorTools
{
    /// <summary>
    /// Builds the Arena scene, placeholder sprites, prefabs, data assets, physics layers and the
    /// extra input actions. Data assets are only created if missing, so tuning survives a rebuild;
    /// the scene and prefabs are regenerated every run.
    /// Run from the menu (AS/Setup/Build Arena Scene) or headless:
    ///   -executeMethod AS.EditorTools.SceneBuilder.Run
    /// </summary>
    public static class SceneBuilder
    {
        const string Root = "Assets/_Project";
        const string InputActionsPath = "Assets/Settings/InputSystem_Actions.inputactions";
        const string ArenaScenePath = Root + "/Scenes/Arena.unity";
        const string ChaserPrefabPath = Root + "/Prefabs/Enemies/Chaser.prefab";

        // Authored waves (all Chasers until step 4 adds enemy types). Endless scaling takes over after the last.
        static readonly (int count, float interval)[] AuthoredWaves = { (6, 1f), (10, 0.8f), (16, 0.6f) };
        static string WavePath(int index) => $"{Root}/Data/Waves/Wave_{index + 1:00}.asset";

        // Interior of the arena, in world units. Walls sit just outside it.
        const float HalfWidth = 16f;
        const float HalfHeight = 10f;

        static readonly Color FloorColor = new(0.09f, 0.1f, 0.14f);
        static readonly Color GridColor = new(1f, 1f, 1f, 0.04f);
        static readonly Color WallColor = new(0.32f, 0.36f, 0.48f);
        static readonly Color PlayerColor = new(0.35f, 0.9f, 1f);
        static readonly Color BulletColor = new(1f, 0.9f, 0.35f);
        static readonly Color ChaserColor = new(1f, 0.32f, 0.36f);

        [MenuItem("AS/Setup/Build Arena Scene")]
        public static void Run()
        {
            var layers = ConfigurePhysics();
            AddInputActions();

            var circle = CreateSprite("Circle", 128, CircleAlpha, FilterMode.Bilinear);
            var square = CreateSprite("Square", 4, (_, _, _) => 1f, FilterMode.Point);
            var triangle = CreateSprite("Triangle", 128, TriangleAlpha, FilterMode.Bilinear);

            var input = CreateAsset(ScriptableObject.CreateInstance<InputReader>(), Root + "/Data/Input/InputReader.asset");
            SetRef(input, "actions", AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath));

            var bullet = CreateBulletPrefab(circle, layers.PlayerShot);
            var blaster = CreateAsset(ScriptableObject.CreateInstance<WeaponData>(), Root + "/Data/Weapons/Weapon_Blaster.asset");
            SetRef(blaster, "projectilePrefab", bullet);

            var chaserData = CreateAsset(ScriptableObject.CreateInstance<EnemyData>(), Root + "/Data/Enemies/Enemy_Chaser.asset");
            CreateChaserPrefab(chaserData, triangle, layers);
            CreateWaves(AssetDatabase.LoadAssetAtPath<Chaser>(ChaserPrefabPath));

            BuildArenaScene(input, blaster, circle, square, layers);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ArenaScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] Done. Open Assets/_Project/Scenes/Arena.unity and press Play.");
        }

        // ------------------------------------------------------------------ project settings

        readonly struct LayerSet
        {
            public readonly int Player, Enemy, PlayerShot, EnemyShot, Wall;

            public LayerSet(int player, int enemy, int playerShot, int enemyShot, int wall)
            {
                Player = player;
                Enemy = enemy;
                PlayerShot = playerShot;
                EnemyShot = enemyShot;
                Wall = wall;
            }
        }

        static LayerSet ConfigurePhysics()
        {
            var layers = new LayerSet(
                EnsureLayer(Layers.Player), EnsureLayer(Layers.Enemy),
                EnsureLayer(Layers.PlayerShot), EnsureLayer(Layers.EnemyShot), EnsureLayer(Layers.Wall));

            Physics2D.gravity = Vector2.zero;

            // Our layers only touch what they're meant to; everything else is ignored.
            int[] ours = { layers.Player, layers.Enemy, layers.PlayerShot, layers.EnemyShot, layers.Wall };
            foreach (int a in ours)
                for (int b = 0; b < 32; b++)
                    Physics2D.IgnoreLayerCollision(a, b, true);

            void Allow(int a, int b) => Physics2D.IgnoreLayerCollision(a, b, false);
            Allow(layers.Player, layers.Wall);
            Allow(layers.Player, layers.Enemy);
            Allow(layers.Player, layers.EnemyShot);
            Allow(layers.Enemy, layers.Enemy);
            Allow(layers.Enemy, layers.Wall);
            Allow(layers.Enemy, layers.PlayerShot);
            Allow(layers.PlayerShot, layers.Wall);
            Allow(layers.EnemyShot, layers.Wall);

            EditorUtility.SetDirty(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/Physics2DSettings.asset")[0]);
            return layers;
        }

        static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var layer = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(layer.stringValue)) continue;
                layer.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
            throw new InvalidOperationException("No free user layer for " + layerName);
        }

        static void AddInputActions()
        {
            var asset = InputActionAsset.FromJson(File.ReadAllText(InputActionsPath));
            var map = asset.FindActionMap("Player", throwIfNotFound: true);
            bool changed = false;

            changed |= AddAction(map, "Aim", InputActionType.Value, "Vector2", ("<Gamepad>/rightStick", "Gamepad"));
            changed |= AddAction(map, "Point", InputActionType.Value, "Vector2", ("<Mouse>/position", "Keyboard&Mouse"));
            changed |= AddAction(map, "Pause", InputActionType.Button, null, ("<Keyboard>/escape", "Keyboard&Mouse"), ("<Gamepad>/start", "Gamepad"));

            if (changed)
            {
                File.WriteAllText(InputActionsPath, asset.ToJson());
                AssetDatabase.ImportAsset(InputActionsPath);
            }
            Object.DestroyImmediate(asset);
        }

        static bool AddAction(InputActionMap map, string actionName, InputActionType type, string layout,
                              params (string path, string group)[] bindings)
        {
            if (map.FindAction(actionName) != null) return false;
            var action = map.AddAction(actionName, type, expectedControlLayout: layout);
            foreach (var (path, group) in bindings) action.AddBinding(path, groups: group);
            return true;
        }

        // ------------------------------------------------------------------ assets

        static T CreateAsset<T>(T asset, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                Object.DestroyImmediate(asset);
                return existing;
            }
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static float CircleAlpha(int x, int y, int size)
        {
            float radius = size * 0.5f;
            float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
            return Mathf.Clamp01(radius - distance);
        }

        /// <summary>Isosceles triangle pointing right (+X), so a rotation of 0 faces east.</summary>
        static float TriangleAlpha(int x, int y, int size)
        {
            float u = (x + 0.5f) / size;
            float v = (y + 0.5f) / size;
            return Mathf.Clamp01((0.5f * (1f - u) - Mathf.Abs(v - 0.5f)) * size);
        }

        /// <summary>White sprite, one world unit across, so scale = size in units.</summary>
        static Sprite CreateSprite(string name, int size, Func<int, int, int, float> alpha, FilterMode filter)
        {
            string path = $"{Root}/Art/Sprites/{name}.png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha(x, y, size)));
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = size;
                importer.filterMode = filter;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Projectile CreateBulletPrefab(Sprite circle, int layer)
        {
            var go = new GameObject("PlayerBullet") { layer = layer };
            go.transform.localScale = new Vector3(0.4f, 0.22f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = circle;
            sr.color = BulletColor;
            sr.sortingOrder = 5;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            go.AddComponent<CircleCollider2D>().isTrigger = true;
            go.AddComponent<Projectile>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/Projectiles/PlayerBullet.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<Projectile>();
        }

        static void CreateChaserPrefab(EnemyData data, Sprite triangle, LayerSet layers)
        {
            var go = new GameObject("Chaser") { layer = layers.Enemy };

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            go.AddComponent<CircleCollider2D>().radius = 0.4f;
            var health = go.AddComponent<Health>();

            var visual = Quad("Visual", go.transform, Vector2.zero, new Vector2(0.9f, 0.9f), triangle, ChaserColor, 8);
            visual.layer = layers.Enemy;

            var flash = go.AddComponent<DamageFlash>();
            SetRef(flash, "health", health);
            SetRef(flash, "target", visual.GetComponent<SpriteRenderer>());

            var chaser = go.AddComponent<Chaser>();
            SetRef(chaser, "data", data);
            SetRef(chaser, "visual", visual.transform);
            SetInt(chaser, "contactTargets", 1 << layers.Player);

            PrefabUtility.SaveAsPrefabAsset(go, ChaserPrefabPath);
            Object.DestroyImmediate(go);
        }

        // ------------------------------------------------------------------ scene

        static void BuildArenaScene(InputReader input, WeaponData weapon, Sprite circle, Sprite square, LayerSet layers)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // NewScene unloads unreferenced assets, which invalidates handles taken before it:
            // load prefabs and the wave list by path from here on.
            var waves = new Object[AuthoredWaves.Length];
            for (int i = 0; i < waves.Length; i++) waves[i] = AssetDatabase.LoadAssetAtPath<WaveDefinition>(WavePath(i));

            var gameManager = new GameObject("GameManager").AddComponent<GameManager>();
            SetRef(gameManager, "input", input);

            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            var cameraBounds = BuildArena(square, layers.Wall);
            var player = BuildPlayer(input, weapon, circle, square, layers.Player);
            BuildCamera(player.transform, cameraBounds);
            var spawner = BuildSpawner(player.transform);

            var director = new GameObject("WaveDirector").AddComponent<WaveDirector>();
            SetRef(director, "spawner", spawner);
            SetList(director, "waves", waves);

            BuildUI(gameManager, square);

            EditorSceneManager.SaveScene(scene, ArenaScenePath);
        }

        static void CreateWaves(Chaser chaser)
        {
            for (int i = 0; i < AuthoredWaves.Length; i++)
            {
                string path = WavePath(i);
                if (AssetDatabase.LoadAssetAtPath<WaveDefinition>(path) != null) continue;

                var wave = ScriptableObject.CreateInstance<WaveDefinition>();
                AssetDatabase.CreateAsset(wave, path);
                SetFloat(wave, "spawnInterval", AuthoredWaves[i].interval);
                int count = AuthoredWaves[i].count;
                Modify(wave, "groups", p =>
                {
                    p.arraySize = 1;
                    var group = p.GetArrayElementAtIndex(0);
                    group.FindPropertyRelative("enemy").objectReferenceValue = chaser;
                    group.FindPropertyRelative("count").intValue = count;
                });
            }
        }

        static PolygonCollider2D BuildArena(Sprite square, int wallLayer)
        {
            var arena = new GameObject("Arena").transform;

            Quad("Floor", arena, Vector2.zero, new Vector2(HalfWidth * 2f, HalfHeight * 2f), square, FloorColor, -20);

            // Faint grid so movement reads even with nothing else on screen.
            var grid = new GameObject("Grid").transform;
            grid.SetParent(arena);
            for (float x = -HalfWidth + 2f; x < HalfWidth; x += 2f)
                Quad("GridX", grid, new Vector2(x, 0f), new Vector2(0.05f, HalfHeight * 2f), square, GridColor, -19);
            for (float y = -HalfHeight + 2f; y < HalfHeight; y += 2f)
                Quad("GridY", grid, new Vector2(0f, y), new Vector2(HalfWidth * 2f, 0.05f), square, GridColor, -19);

            var walls = new GameObject("Walls").transform;
            walls.SetParent(arena);
            float outerW = HalfWidth * 2f + 2f;
            Wall("Wall_N", walls, new Vector2(0f, HalfHeight + 0.5f), new Vector2(outerW, 1f), square, wallLayer);
            Wall("Wall_S", walls, new Vector2(0f, -HalfHeight - 0.5f), new Vector2(outerW, 1f), square, wallLayer);
            Wall("Wall_W", walls, new Vector2(-HalfWidth - 0.5f, 0f), new Vector2(1f, HalfHeight * 2f), square, wallLayer);
            Wall("Wall_E", walls, new Vector2(HalfWidth + 0.5f, 0f), new Vector2(1f, HalfHeight * 2f), square, wallLayer);

            var pillars = new GameObject("Pillars").transform;
            pillars.SetParent(arena);
            foreach (var p in new[] { new Vector2(-8f, 5f), new Vector2(8f, 5f), new Vector2(-8f, -5f), new Vector2(8f, -5f) })
                Wall("Pillar", pillars, p, new Vector2(2f, 2f), square, wallLayer);

            var bounds = new GameObject("CameraBounds");
            bounds.transform.SetParent(arena);
            var poly = bounds.AddComponent<PolygonCollider2D>();
            poly.isTrigger = true;
            float bx = HalfWidth + 1f, by = HalfHeight + 1f;
            poly.points = new[] { new Vector2(-bx, -by), new Vector2(-bx, by), new Vector2(bx, by), new Vector2(bx, -by) };
            return poly;
        }

        static GameObject BuildPlayer(InputReader input, WeaponData weaponData, Sprite circle, Sprite square, int layer)
        {
            var player = new GameObject("Player") { layer = layer };

            var body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.mass = 3f; // heavier than enemies so a crowd can't shove the player around
            player.AddComponent<CircleCollider2D>().radius = 0.45f;

            var health = player.AddComponent<Health>();
            SetFloat(health, "maxHealth", 5f);
            SetFloat(health, "invulnerabilityTime", 1f);

            var bodyVisual = Quad("Body", player.transform, Vector2.zero, new Vector2(0.9f, 0.9f), circle, PlayerColor, 10);
            bodyVisual.layer = layer;
            var flash = player.AddComponent<DamageFlash>();
            SetRef(flash, "health", health);
            SetRef(flash, "target", bodyVisual.GetComponent<SpriteRenderer>());
            SetColor(flash, "flashColor", new Color(1f, 0.25f, 0.25f));
            SetFloat(flash, "duration", 0.15f);

            var pivot = new GameObject("AimPivot") { layer = layer }.transform;
            pivot.SetParent(player.transform, false);
            Quad("Barrel", pivot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.18f), square, Color.Lerp(PlayerColor, Color.white, 0.5f), 9).layer = layer;
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(pivot, false);
            muzzle.localPosition = new Vector3(0.8f, 0f);

            var weapon = player.AddComponent<Weapon>();
            SetRef(weapon, "data", weaponData);
            SetRef(weapon, "muzzle", muzzle);

            var aim = player.AddComponent<PlayerAim>();
            SetRef(aim, "input", input);
            SetRef(aim, "pivot", pivot);

            var controller = player.AddComponent<PlayerController>();
            SetRef(controller, "input", input);
            SetRef(controller, "aim", aim);
            SetRef(controller, "weapon", weapon);
            SetRef(controller, "health", health);
            return player;
        }

        static EnemySpawner BuildSpawner(Transform target)
        {
            var spawnerGo = new GameObject("EnemySpawner");
            var spawner = spawnerGo.AddComponent<EnemySpawner>();

            // Corners and edge midpoints, just inside the walls.
            float x = HalfWidth - 1.5f, y = HalfHeight - 1.5f;
            var positions = new[]
            {
                new Vector2(-x, y), new Vector2(0f, y), new Vector2(x, y), new Vector2(x, 0f),
                new Vector2(x, -y), new Vector2(0f, -y), new Vector2(-x, -y), new Vector2(-x, 0f),
            };
            var points = new Object[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                var point = new GameObject($"SpawnPoint_{i}").transform;
                point.SetParent(spawnerGo.transform, false);
                point.position = positions[i];
                points[i] = point;
            }

            SetRef(spawner, "target", target);
            SetList(spawner, "spawnPoints", points);
            return spawner;
        }

        // ------------------------------------------------------------------ UI

        static readonly Color PanelTextColor = new(0.92f, 0.95f, 1f);

        static void BuildUI(GameManager gameManager, Sprite square)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGo = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var canvas = canvasGo.transform;

            // HUD
            var hudGo = UIElement("HUD", canvas);
            Stretch(hudGo);
            var hud = hudGo.AddComponent<HUD>();

            var barBg = UIImage("HealthBarBg", hudGo.transform, square, new Color(0f, 0f, 0f, 0.6f));
            Anchor(barBg.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(360f, 28f));
            var fill = UIImage("HealthFill", barBg.transform, square, PlayerColor);
            Stretch(fill.gameObject, 4f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;

            var score = UIText("Score", hudGo.transform, "SCORE 0", 40, TextAnchor.UpperRight);
            Anchor(score.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -30f), new Vector2(500f, 60f));
            var wave = UIText("Wave", hudGo.transform, "", 36, TextAnchor.UpperCenter);
            Anchor(wave.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(400f, 60f));
            var banner = UIText("Banner", hudGo.transform, "", 110, TextAnchor.MiddleCenter);
            banner.fontStyle = FontStyle.Bold;
            Anchor(banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(1400f, 200f));

            SetRef(hud, "healthFill", fill);
            SetRef(hud, "scoreText", score);
            SetRef(hud, "waveText", wave);
            SetRef(hud, "bannerText", banner);

            // Pause menu
            var pause = Panel<PauseMenu>("PauseMenu", canvas, square, new Color(0f, 0f, 0f, 0.65f));
            Anchor(UIText("Title", pause.transform, "PAUSED", 96, TextAnchor.MiddleCenter).rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(1000f, 140f));
            var resume = UIButton("Resume", pause.transform, square, new Vector2(0f, 10f));
            var pauseRestart = UIButton("Restart", pause.transform, square, new Vector2(0f, -80f));
            UnityEventTools.AddPersistentListener(resume.onClick, gameManager.Resume);
            UnityEventTools.AddPersistentListener(pauseRestart.onClick, gameManager.Restart);
            SetRef(pause, "firstSelected", resume.gameObject);

            // Game over
            var gameOver = Panel<GameOverScreen>("GameOverScreen", canvas, square, new Color(0.08f, 0f, 0.02f, 0.78f));
            var title = UIText("Title", gameOver.transform, "GAME OVER", 120, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(1f, 0.4f, 0.42f);
            Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(1200f, 160f));
            var summary = UIText("Summary", gameOver.transform, "", 44, TextAnchor.MiddleCenter);
            Anchor(summary.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1000f, 140f));
            var restart = UIButton("Restart", gameOver.transform, square, new Vector2(0f, -100f));
            UnityEventTools.AddPersistentListener(restart.onClick, gameManager.Restart);
            SetRef(gameOver, "summaryText", summary);
            SetRef(gameOver, "firstSelected", restart.gameObject);
        }

        /// <summary>Full-screen panel with a dimming background, hidden in the editor so the scene stays readable.</summary>
        static T Panel<T>(string name, Transform parent, Sprite square, Color background) where T : UIPanel
        {
            var go = UIElement(name, parent);
            Stretch(go);
            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            var image = go.AddComponent<Image>();
            image.sprite = square;
            image.color = background;
            return go.AddComponent<T>();
        }

        static GameObject UIElement(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static Image UIImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var image = UIElement(name, parent).AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Text UIText(string name, Transform parent, string content, int size, TextAnchor alignment)
        {
            var text = UIElement(name, parent).AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = PanelTextColor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static Button UIButton(string label, Transform parent, Sprite square, Vector2 position)
        {
            var go = UIElement(label + "Button", parent);
            Anchor(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), position, new Vector2(360f, 70f));
            var image = go.AddComponent<Image>();
            image.sprite = square;
            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.2f, 0.24f, 0.32f);
            colors.highlightedColor = new Color(0.3f, 0.55f, 0.7f);
            colors.selectedColor = new Color(0.3f, 0.55f, 0.7f);
            colors.pressedColor = new Color(0.2f, 0.42f, 0.55f);
            button.colors = colors;

            var text = UIText("Label", go.transform, label.ToUpperInvariant(), 34, TextAnchor.MiddleCenter);
            Stretch(text.gameObject);
            return button;
        }

        static void Stretch(GameObject go, float inset = 0f)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        static void BuildCamera(Transform target, PolygonCollider2D bounds)
        {
            const float orthoSize = 7f;

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthoSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.04f, 0.06f);
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            cameraGo.AddComponent<CinemachineBrain>();

            var vcamGo = new GameObject("CM Camera");
            vcamGo.transform.position = new Vector3(0f, 0f, -10f);
            var vcam = vcamGo.AddComponent<CinemachineCamera>();
            vcam.Follow = target;
            var lens = vcam.Lens;
            lens.OrthographicSize = orthoSize;
            vcam.Lens = lens;
            var composer = vcamGo.AddComponent<CinemachinePositionComposer>();
            composer.Damping = new Vector3(0.4f, 0.4f, 0f);
            composer.CameraDistance = 10f;
            vcamGo.AddComponent<CinemachineConfiner2D>().BoundingShape2D = bounds;
        }

        // ------------------------------------------------------------------ helpers

        static GameObject Quad(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return go;
        }

        static void Wall(string name, Transform parent, Vector2 position, Vector2 size, Sprite square, int layer)
        {
            var go = Quad(name, parent, position, size, square, WallColor, 0);
            go.layer = layer;
            go.AddComponent<BoxCollider2D>();
        }

        static void Modify(Object target, string property, Action<SerializedProperty> set)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(property);
            if (prop == null)
            {
                Debug.LogError($"[SceneBuilder] {target.GetType().Name} has no serialized field '{property}'.");
                return;
            }
            set(prop);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        static void SetRef(Object target, string property, Object value) => Modify(target, property, p =>
        {
            p.objectReferenceValue = value;
            // Unity silently stores null for destroyed handles or mismatched types.
            if (!ReferenceEquals(value, null) && p.objectReferenceValue == null)
                Debug.LogError($"[SceneBuilder] {target.GetType().Name}.{property} could not be set (destroyed or wrong type).");
        });
        static void SetFloat(Object target, string property, float value) => Modify(target, property, p => p.floatValue = value);
        static void SetInt(Object target, string property, int value) => Modify(target, property, p => p.intValue = value);
        static void SetColor(Object target, string property, Color value) => Modify(target, property, p => p.colorValue = value);

        static void SetList(Object target, string property, Object[] values) => Modify(target, property, p =>
        {
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        });
    }
}
