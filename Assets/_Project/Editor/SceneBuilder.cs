using System;
using System.IO;
using AS.Combat;
using AS.Core;
using AS.Player;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
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

        // Interior of the arena, in world units. Walls sit just outside it.
        const float HalfWidth = 16f;
        const float HalfHeight = 10f;

        static readonly Color FloorColor = new(0.09f, 0.1f, 0.14f);
        static readonly Color GridColor = new(1f, 1f, 1f, 0.04f);
        static readonly Color WallColor = new(0.32f, 0.36f, 0.48f);
        static readonly Color PlayerColor = new(0.35f, 0.9f, 1f);
        static readonly Color BulletColor = new(1f, 0.9f, 0.35f);

        [MenuItem("AS/Setup/Build Arena Scene")]
        public static void Run()
        {
            var layers = ConfigurePhysics();
            AddInputActions();

            var circle = CreateSprite("Circle", 128, CircleAlpha, FilterMode.Bilinear);
            var square = CreateSprite("Square", 4, (_, _, _) => 1f, FilterMode.Point);

            var input = CreateAsset(ScriptableObject.CreateInstance<InputReader>(), Root + "/Data/Input/InputReader.asset");
            SetRef(input, "actions", AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath));

            var bullet = CreateBulletPrefab(circle, layers.PlayerShot);
            var blaster = CreateAsset(ScriptableObject.CreateInstance<WeaponData>(), Root + "/Data/Weapons/Weapon_Blaster.asset");
            SetRef(blaster, "projectilePrefab", bullet);

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

        // ------------------------------------------------------------------ scene

        static void BuildArenaScene(InputReader input, WeaponData weapon, Sprite circle, Sprite square, LayerSet layers)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            var cameraBounds = BuildArena(square, layers.Wall);
            var player = BuildPlayer(input, weapon, circle, square, layers.Player);
            BuildCamera(player.transform, cameraBounds);

            EditorSceneManager.SaveScene(scene, ArenaScenePath);
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
            player.AddComponent<CircleCollider2D>().radius = 0.45f;

            Quad("Body", player.transform, Vector2.zero, new Vector2(0.9f, 0.9f), circle, PlayerColor, 10).layer = layer;

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
            return player;
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

        static void SetRef(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(property);
            if (prop == null)
            {
                Debug.LogError($"[SceneBuilder] {target.GetType().Name} has no serialized field '{property}'.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
