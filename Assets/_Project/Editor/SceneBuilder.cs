using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AS.Agents;
using AS.Combat;
using AS.Core;
using AS.Enemies;
using AS.Feedback;
using AS.Pickups;
using AS.Player;
using AS.UI;
using AS.Waves;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AS.EditorTools
{
    /// <summary>
    /// Builds the Arena and Training scenes, placeholder sprites and sounds, prefabs, data assets,
    /// the audio mixer, physics layers, tags and the extra input actions, and imports TextMesh Pro's
    /// essential resources if they're missing. Data assets, sprites and sounds are only created if
    /// missing, so tuning (or a real replacement file) survives a rebuild; the scenes and prefabs are
    /// regenerated every run. The Training scene is in SceneBuilder.Training.cs.
    /// Run from the menu (AS/Setup/Build Arena Scene) or headless:
    ///   -executeMethod AS.EditorTools.SceneBuilder.Run
    /// </summary>
    public static partial class SceneBuilder
    {
        const string Root = "Assets/_Project";
        const string InputActionsPath = "Assets/Settings/InputSystem_Actions.inputactions";
        const string ArenaScenePath = Root + "/Scenes/Arena.unity";
        const string PlayerBulletPrefabPath = Root + "/Prefabs/Projectiles/PlayerBullet.prefab";
        const string EnemyBulletPrefabPath = Root + "/Prefabs/Projectiles/EnemyBullet.prefab";
        const string ChaserPrefabPath = Root + "/Prefabs/Enemies/Chaser.prefab";
        const string ShooterPrefabPath = Root + "/Prefabs/Enemies/Shooter.prefab";
        const string TankPrefabPath = Root + "/Prefabs/Enemies/Tank.prefab";
        const string PickupPrefabPath = Root + "/Prefabs/Pickups/Pickup.prefab";
        const string TmpFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        const string MixerPath = Root + "/Audio/ArenaMixer.mixer";
        const string MusicPath = Root + "/Audio/Music/ArenaLoop.wav";
        static string SfxPath(string name) => $"{Root}/Audio/SFX/{name}.wav";
        static string EffectPath(string name) => $"{Root}/Prefabs/Effects/{name}.prefab";
        static readonly string[] PowerUpPaths =
        {
            Root + "/Data/PowerUps/PowerUp_Spread.asset",
            Root + "/Data/PowerUps/PowerUp_Rapid.asset",
            Root + "/Data/PowerUps/PowerUp_Shield.asset",
        };

        // Authored waves; endless scaling continues from the last one. Each group is (enemy prefab, count).
        static readonly (float interval, (string enemy, int count)[] groups)[] AuthoredWaves =
        {
            (1.0f, new[] { (ChaserPrefabPath, 6) }),
            (0.85f, new[] { (ChaserPrefabPath, 8), (ShooterPrefabPath, 2) }),
            (0.7f, new[] { (ChaserPrefabPath, 10), (ShooterPrefabPath, 3), (TankPrefabPath, 1) }),
            (0.6f, new[] { (ChaserPrefabPath, 12), (ShooterPrefabPath, 4), (TankPrefabPath, 2) }),
        };
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
        static readonly Color ShooterColor = new(0.78f, 0.45f, 1f);
        static readonly Color TankColor = new(0.62f, 0.16f, 0.2f);
        static readonly Color EnemyBulletColor = new(1f, 0.45f, 0.85f);
        static readonly Color ShieldColor = new(0.45f, 0.8f, 1f, 0.35f);

        [MenuItem("AS/Setup/Build Arena Scene")]
        public static void Run()
        {
            EnsureFolders("Art/Materials", "Audio/SFX", "Audio/Music", "Prefabs/Effects");
            EnsureTmpEssentials();
            var layers = ConfigurePhysics();
            foreach (string tag in Tags.All) EnsureTag(tag);
            AddInputActions();

            var circle = CreateSprite("Circle", 128, CircleAlpha, FilterMode.Bilinear);
            var square = CreateSprite("Square", 4, (_, _, _) => 1f, FilterMode.Point);
            var triangle = CreateSprite("Triangle", 128, TriangleAlpha, FilterMode.Bilinear);

            var input = CreateAsset(ScriptableObject.CreateInstance<InputReader>(), Root + "/Data/Input/InputReader.asset");
            SetRef(input, "actions", AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath));

            // Weapons
            var bullet = CreateBulletPrefab("PlayerBullet", PlayerBulletPrefabPath, circle, BulletColor, new Vector2(0.4f, 0.22f), layers.PlayerShot);
            var enemyBullet = CreateBulletPrefab("EnemyBullet", EnemyBulletPrefabPath, circle, EnemyBulletColor, new Vector2(0.32f, 0.32f), layers.EnemyShot, Tags.EnemyProjectile);

            var blaster = CreateAsset(ScriptableObject.CreateInstance<WeaponData>(), Root + "/Data/Weapons/Weapon_Blaster.asset");
            SetRef(blaster, "projectilePrefab", bullet);
            var spread = CreateOrLoad<WeaponData>(Root + "/Data/Weapons/Weapon_Spread.asset", w =>
            {
                SetFloat(w, "fireRate", 5f);
                SetFloat(w, "projectileSpeed", 16f);
                SetFloat(w, "projectileLifetime", 1.2f);
                SetInt(w, "projectileCount", 5);
                SetFloat(w, "spreadAngle", 40f);
            });
            SetRef(spread, "projectilePrefab", bullet);
            var rapid = CreateOrLoad<WeaponData>(Root + "/Data/Weapons/Weapon_Rapid.asset", w =>
            {
                SetFloat(w, "fireRate", 16f);
                SetFloat(w, "projectileSpeed", 20f);
                SetFloat(w, "projectileLifetime", 1.2f);
            });
            SetRef(rapid, "projectilePrefab", bullet);
            var enemyBlaster = CreateOrLoad<WeaponData>(Root + "/Data/Weapons/Weapon_EnemyBlaster.asset", w =>
            {
                SetFloat(w, "fireRate", 0.8f);
                SetFloat(w, "projectileSpeed", 9f);
                SetFloat(w, "projectileLifetime", 3f);
            });
            SetRef(enemyBlaster, "projectilePrefab", enemyBullet);

            // Enemies
            var chaserData = CreateAsset(ScriptableObject.CreateInstance<EnemyData>(), Root + "/Data/Enemies/Enemy_Chaser.asset");
            var shooterData = CreateOrLoad<EnemyData>(Root + "/Data/Enemies/Enemy_Shooter.asset", d =>
                SetEnemyStats(d, health: 2f, speed: 3f, acceleration: 15f, contactDamage: 1f, score: 25, dropChance: 0.15f));
            var tankData = CreateOrLoad<EnemyData>(Root + "/Data/Enemies/Enemy_Tank.asset", d =>
                SetEnemyStats(d, health: 12f, speed: 1.6f, acceleration: 6f, contactDamage: 2f, score: 50, dropChance: 0.5f));

            CreateEnemyPrefab<Chaser>("Chaser", ChaserPrefabPath, chaserData, triangle, ChaserColor, 0.9f, 0.4f, 1f, layers);
            // A Tank is just a Chaser with different data and a bigger body: no new code needed.
            CreateEnemyPrefab<Chaser>("Tank", TankPrefabPath, tankData, square, TankColor, 1.4f, 0.7f, 4f, layers);
            CreateEnemyPrefab<Shooter>("Shooter", ShooterPrefabPath, shooterData, circle, ShooterColor, 0.85f, 0.42f, 1f, layers, (shooter, visual) =>
            {
                Quad("Barrel", visual, new Vector2(0.45f, 0f), new Vector2(0.45f, 0.16f), square, Color.Lerp(ShooterColor, Color.white, 0.4f), 7);
                var weapon = shooter.gameObject.AddComponent<Weapon>();
                SetRef(weapon, "data", enemyBlaster);
                SetRef(shooter, "weapon", weapon);
            });
            CreateWaves();

            // Power-ups
            var spreadUp = CreateOrLoad<WeaponPowerUp>(PowerUpPaths[0], p => SetPowerUp(p, "SPREAD", new Color(1f, 0.6f, 0.2f), 8f));
            SetRef(spreadUp, "weapon", spread);
            var rapidUp = CreateOrLoad<WeaponPowerUp>(PowerUpPaths[1], p => SetPowerUp(p, "RAPID", new Color(0.6f, 1f, 0.35f), 8f));
            SetRef(rapidUp, "weapon", rapid);
            CreateOrLoad<ShieldPowerUp>(PowerUpPaths[2], p => SetPowerUp(p, "SHIELD", new Color(0.4f, 0.75f, 1f), 6f));
            CreatePickupPrefab(square, circle, layers.Pickup);

            // Feedback
            CreateEffectPrefabs(CreateParticleMaterial(circle));
            CreateSounds();
            CreateMixer();

            BuildArenaScene(layers);
            // Last, so the Arena scene is what's left open.
            BuildTrainingScene(layers);
            EditorSceneManager.OpenScene(ArenaScenePath);

            // The Training scene stays out of the game build; the training build adds it itself.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ArenaScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] Done. Open Assets/_Project/Scenes/Arena.unity and press Play.");
        }

        // ------------------------------------------------------------------ project settings

        readonly struct LayerSet
        {
            public readonly int Player, Enemy, PlayerShot, EnemyShot, Wall, Pickup;

            public LayerSet(int player, int enemy, int playerShot, int enemyShot, int wall, int pickup)
            {
                Player = player;
                Enemy = enemy;
                PlayerShot = playerShot;
                EnemyShot = enemyShot;
                Wall = wall;
                Pickup = pickup;
            }
        }

        static LayerSet ConfigurePhysics()
        {
            var layers = new LayerSet(
                EnsureLayer(Layers.Player), EnsureLayer(Layers.Enemy),
                EnsureLayer(Layers.PlayerShot), EnsureLayer(Layers.EnemyShot), EnsureLayer(Layers.Wall),
                EnsureLayer(Layers.Pickup));

            Physics2D.gravity = Vector2.zero;

            // Our layers only touch what they're meant to; everything else is ignored.
            int[] ours = { layers.Player, layers.Enemy, layers.PlayerShot, layers.EnemyShot, layers.Wall, layers.Pickup };
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
            Allow(layers.Pickup, layers.Player);

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

        static void EnsureTag(string tagName)
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tags = tagManager.FindProperty("tags");
            for (int i = 0; i < tags.arraySize; i++)
                if (tags.GetArrayElementAtIndex(i).stringValue == tagName) return;

            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tagName;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
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

        /// <summary>Creates folders under <see cref="Root"/> that assets are about to be saved into.</summary>
        static void EnsureFolders(params string[] folders)
        {
            bool created = false;
            foreach (string folder in folders)
            {
                string path = $"{Root}/{folder}";
                if (Directory.Exists(path)) continue;
                Directory.CreateDirectory(path);
                created = true;
            }
            if (created) AssetDatabase.Refresh();
        }

        /// <summary>
        /// TextMesh Pro ships its default font and settings as a package inside com.unity.ugui that
        /// each project imports once (normally via a popup). Starts that import if it hasn't happened.
        /// </summary>
        static void EnsureTmpEssentials()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath) != null) return;

            var ugui = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui");
            string package = ugui != null ? Path.Combine(ugui.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage") : null;
            if (package == null || !File.Exists(package))
            {
                Debug.LogError("[SceneBuilder] Couldn't find TMP Essential Resources in com.unity.ugui. Import them from Window > TextMeshPro.");
                return;
            }
            // The import is queued and only lands after this method returns, so this run can't use it.
            AssetDatabase.ImportPackage(package, false);
            Debug.LogError("[SceneBuilder] TMP Essential Resources were missing and are being imported: run the builder again " +
                           "once the import finishes. (Headless, pass -importPackage \"" + package + "\" in a run before this one.)");
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

        /// <summary>Loads the asset at <paramref name="path"/>, or creates it and runs <paramref name="initialize"/> to set starting values.</summary>
        static T CreateOrLoad<T>(string path, Action<T> initialize) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            initialize(asset);
            return asset;
        }

        static void SetEnemyStats(EnemyData data, float health, float speed, float acceleration, float contactDamage, int score, float dropChance)
        {
            SetFloat(data, "maxHealth", health);
            SetFloat(data, "moveSpeed", speed);
            SetFloat(data, "acceleration", acceleration);
            SetFloat(data, "contactDamage", contactDamage);
            SetInt(data, "scoreValue", score);
            SetFloat(data, "dropChance", dropChance);
        }

        static void SetPowerUp(PowerUp powerUp, string label, Color color, float duration)
        {
            SetString(powerUp, "label", label);
            SetColor(powerUp, "color", color);
            SetFloat(powerUp, "duration", duration);
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

        static Projectile CreateBulletPrefab(string name, string path, Sprite circle, Color color, Vector2 size, int layer, string tag = "Untagged")
        {
            var go = new GameObject(name) { layer = layer, tag = tag };
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = circle;
            sr.color = color;
            sr.sortingOrder = 5;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            go.AddComponent<CircleCollider2D>().isTrigger = true;
            go.AddComponent<Projectile>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<Projectile>();
        }

        /// <summary>
        /// Builds an enemy prefab: physics body, Health, flash-on-hit visual and the enemy behaviour
        /// <typeparamref name="T"/>. <paramref name="extra"/> adds anything type-specific (e.g. a gun).
        /// </summary>
        static void CreateEnemyPrefab<T>(string name, string path, EnemyData data, Sprite sprite, Color color,
                                         float size, float radius, float mass, LayerSet layers,
                                         Action<T, Transform> extra = null) where T : EnemyBase
        {
            var go = new GameObject(name) { layer = layers.Enemy, tag = Tags.Enemy };

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.mass = mass;
            go.AddComponent<CircleCollider2D>().radius = radius;
            var health = go.AddComponent<Health>();

            var visual = Quad("Visual", go.transform, Vector2.zero, new Vector2(size, size), sprite, color, 8);
            visual.layer = layers.Enemy;

            var flash = go.AddComponent<DamageFlash>();
            SetRef(flash, "health", health);
            SetRef(flash, "target", visual.GetComponent<SpriteRenderer>());

            var enemy = go.AddComponent<T>();
            SetRef(enemy, "data", data);
            SetRef(enemy, "visual", visual.transform);
            SetInt(enemy, "contactTargets", 1 << layers.Player);
            extra?.Invoke(enemy, visual.transform);

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        static void CreatePickupPrefab(Sprite square, Sprite circle, int layer)
        {
            var go = new GameObject("Pickup") { layer = layer, tag = Tags.Pickup };
            go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.6f;

            Quad("Glow", go.transform, Vector2.zero, new Vector2(1.3f, 1.3f), circle, new Color(1f, 1f, 1f, 0.15f), 5);
            var visual = Quad("Visual", go.transform, Vector2.zero, new Vector2(0.6f, 0.6f), square, Color.white, 6);
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            var pickup = go.AddComponent<Pickup>();
            SetRef(pickup, "spriteRenderer", visual.GetComponent<SpriteRenderer>());

            PrefabUtility.SaveAsPrefabAsset(go, PickupPrefabPath);
            Object.DestroyImmediate(go);
        }

        // ------------------------------------------------------------------ feedback assets

        /// <summary>Unlit, so bursts glow at full brightness whatever the 2D lighting does.</summary>
        static Material CreateParticleMaterial(Sprite circle)
        {
            string path = Root + "/Art/Materials/Particle.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.mainTexture = circle.texture;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void CreateEffectPrefabs(Material material)
        {
            // (count, speed range, lifetime range, size range, colour). Enemy deaths and pickups are tinted at runtime.
            CreateEffectPrefab("EnemyDeath", material, 22, new Vector2(3f, 9f), new Vector2(0.25f, 0.55f), new Vector2(0.12f, 0.32f), Color.white);
            CreateEffectPrefab("Impact", material, 6, new Vector2(2f, 6f), new Vector2(0.08f, 0.18f), new Vector2(0.06f, 0.14f), new Color(1f, 0.92f, 0.6f));
            CreateEffectPrefab("PlayerHit", material, 14, new Vector2(3f, 8f), new Vector2(0.2f, 0.4f), new Vector2(0.1f, 0.22f), new Color(1f, 0.3f, 0.3f));
            CreateEffectPrefab("PlayerDeath", material, 60, new Vector2(2f, 13f), new Vector2(0.5f, 1.1f), new Vector2(0.12f, 0.4f), PlayerColor);
            CreateEffectPrefab("PickupCollected", material, 16, new Vector2(1.5f, 4.5f), new Vector2(0.3f, 0.5f), new Vector2(0.1f, 0.2f), Color.white, shapeRadius: 0.5f);
        }

        /// <summary>A single burst of particles flying outward in the XY plane, slowing, shrinking and fading.</summary>
        static void CreateEffectPrefab(string name, Material material, int count, Vector2 speed, Vector2 lifetime,
                                       Vector2 size, Color color, float shapeRadius = 0.1f)
        {
            var go = new GameObject(name);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.duration = lifetime.y;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = color;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Callback;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = shapeRadius;
            shape.radiusThickness = 1f;

            // Dampen with no speed limit acts like drag: a fast pop that settles.
            var drag = particles.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 0f;
            drag.dampen = 0.12f;

            var shrink = particles.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 15;

            go.AddComponent<PooledEffect>();
            PrefabUtility.SaveAsPrefabAsset(go, EffectPath(name));
            Object.DestroyImmediate(go);
        }

        static void CreateSounds()
        {
            const PlaceholderAudio.Wave sine = PlaceholderAudio.Wave.Sine, square = PlaceholderAudio.Wave.Square;
            const PlaceholderAudio.Wave saw = PlaceholderAudio.Wave.Saw, triangle = PlaceholderAudio.Wave.Triangle;

            PlaceholderAudio.Ensure(SfxPath("PlayerShot"), false, () => PlaceholderAudio.Sweep(0.08f, 1400f, 500f, square, 5f, noise: 0.1f, gain: 0.25f));
            PlaceholderAudio.Ensure(SfxPath("EnemyShot"), false, () => PlaceholderAudio.Sweep(0.14f, 700f, 250f, triangle, 4f, gain: 0.35f));
            PlaceholderAudio.Ensure(SfxPath("EnemyHit"), false, () => PlaceholderAudio.Sweep(0.05f, 420f, 200f, square, 6f, noise: 0.3f, gain: 0.3f));
            PlaceholderAudio.Ensure(SfxPath("EnemyDeath"), false, () => PlaceholderAudio.Sweep(0.35f, 220f, 40f, square, 5f, noise: 0.6f, gain: 0.45f));
            PlaceholderAudio.Ensure(SfxPath("PlayerHurt"), false, () => PlaceholderAudio.Sweep(0.25f, 330f, 110f, saw, 3f, noise: 0.25f, gain: 0.45f));
            PlaceholderAudio.Ensure(SfxPath("PlayerDeath"), false, () => PlaceholderAudio.Sweep(1f, 440f, 30f, saw, 3f, noise: 0.5f, gain: 0.5f));
            PlaceholderAudio.Ensure(SfxPath("Pickup"), false, () => PlaceholderAudio.Notes(0.07f, square, 3f, 0.3f, 84, 88, 91, 96));
            PlaceholderAudio.Ensure(SfxPath("WaveStarted"), false, () => PlaceholderAudio.Notes(0.12f, triangle, 2f, 0.4f, 69, 76));
            PlaceholderAudio.Ensure(SfxPath("WaveCleared"), false, () => PlaceholderAudio.Notes(0.1f, sine, 2f, 0.45f, 72, 76, 79, 84));
            PlaceholderAudio.Ensure(MusicPath, true, PlaceholderAudio.MusicLoop);
        }

        /// <summary>
        /// Creates the mixer (Master with Music and SFX groups) if it's missing. Unity has no public
        /// API for authoring mixers, so this calls the editor's internal one by reflection; if that
        /// ever changes, it logs a warning and the game simply plays without a mixer. The settings menu
        /// needs each group's Volume exposed as MusicVolume and SfxVolume. The committed mixer has
        /// them; a newly created one needs them exposed by hand (right-click Volume in the group's
        /// inspector).
        /// </summary>
        static void CreateMixer()
        {
            if (AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath) != null) return;

            var controllerType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEditor.Audio.AudioMixerController"))
                .FirstOrDefault(t => t != null);
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            MethodInfo Method(string name, int parameters) => controllerType?.GetMethods(flags)
                .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == parameters);

            var create = Method("CreateMixerControllerAtPath", 1);
            var createGroup = Method("CreateNewGroup", 2);
            var addChild = Method("AddChildToParent", 2);
            var master = controllerType?.GetProperty("masterGroup", flags);
            if (create == null || createGroup == null || addChild == null || master == null)
            {
                Debug.LogWarning("[SceneBuilder] Couldn't find Unity's internal mixer API; skipping the audio mixer. Create " +
                                 $"{MixerPath} with Music and SFX groups by hand and rerun to route audio through it.");
                return;
            }

            var controller = create.Invoke(null, new object[] { MixerPath });
            var masterGroup = master.GetValue(controller);
            foreach (string groupName in new[] { "Music", "SFX" })
            {
                var group = createGroup.Invoke(controller, new object[] { groupName, false });
                addChild.Invoke(controller, new[] { group, masterGroup });
            }
            EditorUtility.SetDirty((Object)controller);
            AssetDatabase.SaveAssets();
        }

        static AudioMixerGroup MixerGroup(string groupName)
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            return mixer != null ? mixer.FindMatchingGroups(groupName).FirstOrDefault() : null;
        }

        // ------------------------------------------------------------------ scene

        static void BuildArenaScene(LayerSet layers)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // NewScene unloads unreferenced assets, which invalidates handles taken before it
            // (saved assets go too, not just prefabs): load everything by path from here on.
            var input = AssetDatabase.LoadAssetAtPath<InputReader>(Root + "/Data/Input/InputReader.asset");
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(Root + "/Data/Weapons/Weapon_Blaster.asset");
            var circle = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Sprites/Circle.png");
            var square = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Sprites/Square.png");

            var gameManager = new GameObject("GameManager").AddComponent<GameManager>();
            SetRef(gameManager, "input", input);

            BuildGlobalLight();

            // Everything that plays the game lives under the Arena root and uses its events; the
            // camera, feedback and UI stay outside and listen on the global bus.
            var arena = BuildArena(square, layers.Wall, out var cameraBounds);
            SetRef(gameManager, "arena", arena);
            var player = BuildPlayer(input, weapon, circle, square, layers.Player);
            player.transform.SetParent(arena.transform, false);
            BuildCamera(player.transform, cameraBounds);
            BuildGameplay(arena, player.transform);

            BuildFeedback();
            BuildUI(gameManager, square);

            EditorSceneManager.SaveScene(scene, ArenaScenePath);
        }

        static void BuildGlobalLight()
        {
            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
        }

        /// <summary>The enemy spawner, wave director and pickup dropper, under <paramref name="arena"/>.</summary>
        static void BuildGameplay(Arena arena, Transform player)
        {
            var spawner = BuildSpawner(player);
            spawner.transform.SetParent(arena.transform, false);

            var waves = new Object[AuthoredWaves.Length];
            for (int i = 0; i < waves.Length; i++) waves[i] = AssetDatabase.LoadAssetAtPath<WaveDefinition>(WavePath(i));
            var director = new GameObject("WaveDirector").AddComponent<WaveDirector>();
            director.transform.SetParent(arena.transform, false);
            SetRef(director, "spawner", spawner);
            SetList(director, "waves", waves);

            var powerUps = new Object[PowerUpPaths.Length];
            for (int i = 0; i < powerUps.Length; i++) powerUps[i] = AssetDatabase.LoadAssetAtPath<PowerUp>(PowerUpPaths[i]);
            var dropper = new GameObject("PickupDropper").AddComponent<PickupDropper>();
            dropper.transform.SetParent(arena.transform, false);
            SetRef(dropper, "pickupPrefab", AssetDatabase.LoadAssetAtPath<Pickup>(PickupPrefabPath));
            SetList(dropper, "powerUps", powerUps);
        }

        /// <summary>Screen shake, hit-stop, particles, sound effects and music: all driven by events.</summary>
        static void BuildFeedback()
        {
            var feedback = new GameObject("Feedback");

            var impulse = feedback.AddComponent<CinemachineImpulseSource>();
            impulse.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            impulse.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            impulse.ImpulseDefinition.ImpulseDuration = 0.2f;
            SetRef(feedback.AddComponent<ScreenShake>(), "source", impulse);
            feedback.AddComponent<HitStop>();

            var effects = feedback.AddComponent<ParticleEffects>();
            foreach (var (field, prefab) in new[]
                     {
                         ("enemyDeath", "EnemyDeath"), ("impact", "Impact"), ("playerHit", "PlayerHit"),
                         ("playerDeath", "PlayerDeath"), ("pickupCollected", "PickupCollected"),
                     })
                SetRef(effects, field, AssetDatabase.LoadAssetAtPath<PooledEffect>(EffectPath(prefab)));

            var sfxGo = new GameObject("SoundEffects");
            sfxGo.transform.SetParent(feedback.transform, false);
            var sfx = sfxGo.AddComponent<SoundEffects>();
            SetRef(sfx, "output", MixerGroup("SFX"));
            // (cue field, clip, volume, pitch jitter, min interval)
            SetCue(sfx, "playerShot", "PlayerShot", 0.5f, 0.06f, 0.05f);
            SetCue(sfx, "enemyShot", "EnemyShot", 0.6f, 0.08f, 0.08f);
            SetCue(sfx, "enemyHit", "EnemyHit", 0.55f, 0.1f, 0.04f);
            SetCue(sfx, "enemyDeath", "EnemyDeath", 0.8f, 0.12f, 0.04f);
            SetCue(sfx, "playerHurt", "PlayerHurt", 1f, 0.04f, 0.1f);
            SetCue(sfx, "playerDeath", "PlayerDeath", 1f, 0f, 0.5f);
            SetCue(sfx, "pickup", "Pickup", 0.8f, 0f, 0.05f);
            SetCue(sfx, "waveStarted", "WaveStarted", 0.7f, 0f, 0.2f);
            SetCue(sfx, "waveCleared", "WaveCleared", 0.7f, 0f, 0.2f);

            var musicGo = new GameObject("Music");
            musicGo.transform.SetParent(feedback.transform, false);
            var source = musicGo.AddComponent<AudioSource>();
            source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = MixerGroup("Music");
            musicGo.AddComponent<MusicPlayer>();
        }

        static void SetCue(SoundEffects sfx, string cue, string clip, float volume, float pitchJitter, float minInterval)
        {
            SetRef(sfx, cue + ".clip", AssetDatabase.LoadAssetAtPath<AudioClip>(SfxPath(clip)));
            SetFloat(sfx, cue + ".volume", volume);
            SetFloat(sfx, cue + ".pitchJitter", pitchJitter);
            SetFloat(sfx, cue + ".minInterval", minInterval);
        }

        static void CreateWaves()
        {
            for (int i = 0; i < AuthoredWaves.Length; i++)
            {
                var (interval, groups) = AuthoredWaves[i];
                CreateOrLoad<WaveDefinition>(WavePath(i), wave =>
                {
                    SetFloat(wave, "spawnInterval", interval);
                    Modify(wave, "groups", p =>
                    {
                        p.arraySize = groups.Length;
                        for (int g = 0; g < groups.Length; g++)
                        {
                            var group = p.GetArrayElementAtIndex(g);
                            var enemy = AssetDatabase.LoadAssetAtPath<EnemyBase>(groups[g].enemy);
                            if (enemy == null) Debug.LogError($"[SceneBuilder] No enemy prefab at {groups[g].enemy}.");
                            group.FindPropertyRelative("enemy").objectReferenceValue = enemy;
                            group.FindPropertyRelative("count").intValue = groups[g].count;
                        }
                    });
                });
            }
        }

        static Arena BuildArena(Sprite square, int wallLayer, out PolygonCollider2D cameraBounds)
        {
            var arenaRoot = new GameObject("Arena").AddComponent<Arena>();
            var arena = arenaRoot.transform;

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
            cameraBounds = poly;
            return arenaRoot;
        }

        /// <summary>The player, driven by keyboard, mouse and gamepad, or by an agent when <paramref name="agentControlled"/>.</summary>
        static GameObject BuildPlayer(InputReader input, WeaponData weaponData, Sprite circle, Sprite square, int layer,
                                      bool agentControlled = false)
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

            var shield = Quad("Shield", player.transform, Vector2.zero, new Vector2(1.5f, 1.5f), circle, ShieldColor, 11);
            shield.layer = layer;
            var receiver = player.AddComponent<PowerUpReceiver>();
            SetRef(receiver, "weapon", weapon);
            SetRef(receiver, "health", health);
            SetRef(receiver, "shieldVisual", shield);

            if (agentControlled)
                player.AddComponent<AgentInput>();
            else
                SetRef(player.AddComponent<HumanPlayerInput>(), "input", input);

            var aim = player.AddComponent<PlayerAim>();
            SetRef(aim, "pivot", pivot);

            var controller = player.AddComponent<PlayerController>();
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
            var powerUpLine = UIText("PowerUps", hudGo.transform, "", 28, TextAlignmentOptions.TopLeft);
            Anchor(powerUpLine.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -80f), new Vector2(700f, 40f));

            var score = UIText("Score", hudGo.transform, "SCORE 0", 40, TextAlignmentOptions.TopRight);
            Anchor(score.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -30f), new Vector2(500f, 60f));
            var best = UIText("Best", hudGo.transform, "BEST 0", 26, TextAlignmentOptions.TopRight);
            best.color = new Color(PanelTextColor.r, PanelTextColor.g, PanelTextColor.b, 0.6f);
            Anchor(best.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -82f), new Vector2(500f, 40f));
            var wave = UIText("Wave", hudGo.transform, "", 36, TextAlignmentOptions.Top);
            Anchor(wave.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(400f, 60f));
            var banner = UIText("Banner", hudGo.transform, "", 110, TextAlignmentOptions.Center);
            banner.fontStyle = FontStyles.Bold;
            Anchor(banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(1400f, 200f));

            SetRef(hud, "healthFill", fill);
            SetRef(hud, "scoreText", score);
            SetRef(hud, "bestText", best);
            SetRef(hud, "waveText", wave);
            SetRef(hud, "bannerText", banner);
            SetRef(hud, "powerUpText", powerUpLine);

            // Settings, built first so the menus below can open it; moved last at the end so it draws on top.
            var settings = BuildSettingsMenu(canvas, square);

            // Title screen
            var titleScreen = Panel<TitleScreen>("TitleScreen", canvas, square, new Color(0.03f, 0.04f, 0.07f, 0.9f));
            var gameTitle = UIText("Title", titleScreen.transform, "ARENA SHOOTER", 130, TextAlignmentOptions.Center);
            gameTitle.fontStyle = FontStyles.Bold;
            gameTitle.color = PlayerColor;
            Anchor(gameTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 230f), new Vector2(1400f, 170f));
            var titleBest = UIText("Best", titleScreen.transform, "", 40, TextAlignmentOptions.Center);
            Anchor(titleBest.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(800f, 60f));
            var play = UIButton("Play", titleScreen.transform, square, new Vector2(0f, -10f));
            var titleSettings = UIButton("Settings", titleScreen.transform, square, new Vector2(0f, -100f));
            var titleQuit = UIButton("Quit", titleScreen.transform, square, new Vector2(0f, -190f));
            UnityEventTools.AddPersistentListener(play.onClick, gameManager.Play);
            UnityEventTools.AddObjectPersistentListener<UIPanel>(titleSettings.onClick, settings.Open, titleScreen);
            UnityEventTools.AddPersistentListener(titleQuit.onClick, gameManager.Quit);
            SetRef(titleScreen, "bestText", titleBest);
            SetRef(titleScreen, "firstSelected", play.gameObject);

            // Pause menu
            var pause = Panel<PauseMenu>("PauseMenu", canvas, square, new Color(0f, 0f, 0f, 0.65f));
            Anchor(UIText("Title", pause.transform, "PAUSED", 96, TextAlignmentOptions.Center).rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(1000f, 140f));
            var resume = UIButton("Resume", pause.transform, square, new Vector2(0f, 40f));
            var pauseRestart = UIButton("Restart", pause.transform, square, new Vector2(0f, -50f));
            var pauseSettings = UIButton("Settings", pause.transform, square, new Vector2(0f, -140f));
            var pauseQuit = UIButton("Quit", pause.transform, square, new Vector2(0f, -230f));
            UnityEventTools.AddPersistentListener(resume.onClick, gameManager.Resume);
            UnityEventTools.AddPersistentListener(pauseRestart.onClick, gameManager.Restart);
            UnityEventTools.AddObjectPersistentListener<UIPanel>(pauseSettings.onClick, settings.Open, pause);
            UnityEventTools.AddPersistentListener(pauseQuit.onClick, gameManager.Quit);
            SetRef(pause, "firstSelected", resume.gameObject);

            // Game over
            var gameOver = Panel<GameOverScreen>("GameOverScreen", canvas, square, new Color(0.08f, 0f, 0.02f, 0.78f));
            var title = UIText("Title", gameOver.transform, "GAME OVER", 120, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            title.color = new Color(1f, 0.4f, 0.42f);
            Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(1200f, 160f));
            var summary = UIText("Summary", gameOver.transform, "", 44, TextAlignmentOptions.Center);
            Anchor(summary.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1000f, 180f));
            var restart = UIButton("Restart", gameOver.transform, square, new Vector2(0f, -130f));
            UnityEventTools.AddPersistentListener(restart.onClick, gameManager.Restart);
            var quit = UIButton("Quit", gameOver.transform, square, new Vector2(0f, -220f));
            UnityEventTools.AddPersistentListener(quit.onClick, gameManager.Quit);
            SetRef(gameOver, "summaryText", summary);
            SetRef(gameOver, "firstSelected", restart.gameObject);

            settings.transform.SetAsLastSibling();
        }

        static SettingsMenu BuildSettingsMenu(Transform canvas, Sprite square)
        {
            var settings = Panel<SettingsMenu>("SettingsMenu", canvas, square, new Color(0.03f, 0.04f, 0.07f, 0.92f));
            Anchor(UIText("Title", settings.transform, "SETTINGS", 96, TextAlignmentOptions.Center).rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(1000f, 140f));
            var music = UISliderRow("Music", settings.transform, square, 80f);
            var sfx = UISliderRow("Effects", settings.transform, square, 0f);
            var fullscreen = UIButton("Fullscreen", settings.transform, square, new Vector2(0f, -100f));
            fullscreen.GetComponent<RectTransform>().sizeDelta = new Vector2(460f, 70f);
            var back = UIButton("Back", settings.transform, square, new Vector2(0f, -200f));
            UnityEventTools.AddPersistentListener(fullscreen.onClick, settings.ToggleFullscreen);
            UnityEventTools.AddPersistentListener(back.onClick, settings.Back);
            SetRef(settings, "mixer", AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath));
            SetRef(settings, "musicSlider", music);
            SetRef(settings, "sfxSlider", sfx);
            SetRef(settings, "fullscreenLabel", fullscreen.GetComponentInChildren<TMP_Text>());
            SetRef(settings, "firstSelected", music.gameObject);
            return settings;
        }

        /// <summary>A label and a 0 to 1 slider side by side at height <paramref name="y"/>.</summary>
        static Slider UISliderRow(string label, Transform parent, Sprite square, float y)
        {
            var text = UIText(label + "Label", parent, label.ToUpperInvariant(), 34, TextAlignmentOptions.Right);
            Anchor(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-150f, y), new Vector2(260f, 60f));
            text.rectTransform.pivot = new Vector2(1f, 0.5f);

            var go = UIElement(label + "Slider", parent);
            var rt = go.GetComponent<RectTransform>();
            Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(-110f, y), new Vector2(400f, 28f));
            rt.pivot = new Vector2(0f, 0.5f);

            var background = UIImage("Background", go.transform, square, new Color(0.2f, 0.24f, 0.32f));
            background.raycastTarget = true;
            Stretch(background.gameObject);
            var fillArea = UIElement("Fill Area", go.transform);
            Stretch(fillArea);
            var fill = UIImage("Fill", fillArea.transform, square, new Color(0.3f, 0.55f, 0.7f));
            fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = UIElement("Handle Slide Area", go.transform);
            Stretch(handleArea);
            var handle = UIImage("Handle", handleArea.transform, square, PanelTextColor);
            handle.rectTransform.sizeDelta = new Vector2(24f, 20f);
            handle.raycastTarget = true;

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            var colors = slider.colors;
            colors.highlightedColor = colors.selectedColor = new Color(0.55f, 0.85f, 1f);
            slider.colors = colors;
            return slider;
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

        static TextMeshProUGUI UIText(string name, Transform parent, string content, int size, TextAlignmentOptions alignment)
        {
            var text = UIElement(name, parent).AddComponent<TextMeshProUGUI>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
            text.text = content;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = PanelTextColor;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
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

            var text = UIText("Label", go.transform, label.ToUpperInvariant(), 34, TextAlignmentOptions.Center);
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
            // The camera is built from scratch, so it doesn't get the listener a default scene camera has.
            cameraGo.AddComponent<AudioListener>();

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
            // Receives the ScreenShake impulses.
            vcamGo.AddComponent<CinemachineImpulseListener>();
            // A restart cuts back to the player instead of gliding.
            vcamGo.AddComponent<CameraSnap>();
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
            go.tag = Tags.Wall;
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
        static void SetString(Object target, string property, string value) => Modify(target, property, p => p.stringValue = value);

        static void SetList(Object target, string property, Object[] values) => Modify(target, property, p =>
        {
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        });
    }
}
