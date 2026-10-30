using System.Collections.Generic;
using AS.Agents;
using AS.Combat;
using AS.Core;
using AS.Player;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AS.EditorTools
{
    public static partial class SceneBuilder
    {
        const string TrainingScenePath = Root + "/Scenes/Training.unity";

        /// <summary>
        /// The Training scene: one arena with the same player, enemies, waves and pickups as the game,
        /// but the player is driven by an <see cref="ArenaAgent"/>. There is no GameManager, UI,
        /// music, screen shake or hit-stop, so nothing touches Time.timeScale, which training runs
        /// at 20; the agent resets the arena itself at the start of each episode. The arena doesn't
        /// forward to the global bus, which nothing in this scene listens on anyway.
        /// </summary>
        static void BuildTrainingScene(LayerSet layers)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // As in BuildArenaScene: load assets by path after NewScene.
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(Root + "/Data/Weapons/Weapon_Blaster.asset");
            var circle = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Sprites/Circle.png");
            var square = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Sprites/Square.png");

            BuildGlobalLight();
            var arena = BuildArena(square, layers.Wall, out _);
            Modify(arena, "forwardToGlobal", p => p.boolValue = false);
            var player = BuildPlayer(null, weapon, circle, square, layers.Player, agentControlled: true);
            player.transform.SetParent(arena.transform, false);
            BuildGameplay(arena, player.transform);
            BuildAgent(arena, player, layers);
            BuildOverviewCamera();

            EditorSceneManager.SaveScene(scene, TrainingScenePath);
        }

        static void BuildAgent(Arena arena, GameObject player, LayerSet layers)
        {
            var go = new GameObject("ArenaAgent");
            go.transform.SetParent(arena.transform, false);

            // Before the agent, which requires it.
            var parameters = go.AddComponent<BehaviorParameters>();
            parameters.BehaviorName = ArenaAgent.Behavior;
            parameters.BrainParameters.VectorObservationSize = ArenaAgent.VectorObservationSize;
            parameters.BrainParameters.ActionSpec = ArenaAgent.Actions;

            // World-aligned rays all around the player. Only the layers worth seeing, which also
            // keeps the camera-bounds trigger and the player's own collider out of them.
            var rays = go.AddComponent<RayPerceptionSensorComponent2D>();
            rays.SensorName = "Rays";
            rays.DetectableTags = new List<string>(Tags.All);
            rays.RaysPerDirection = 8;
            rays.MaxRayDegrees = 180f;
            rays.RayLength = 20f;
            rays.SphereCastRadius = 0.3f;
            rays.RayLayerMask = (1 << layers.Enemy) | (1 << layers.EnemyShot) | (1 << layers.Wall) | (1 << layers.Pickup);

            var agent = go.AddComponent<ArenaAgent>();
            agent.MaxStep = 3000;
            SetRef(agent, "arena", arena);
            SetRef(agent, "input", player.GetComponent<AgentInput>());
            SetRef(agent, "body", player.GetComponent<Rigidbody2D>());
            SetRef(agent, "health", player.GetComponent<Health>());
            SetRef(agent, "aim", player.GetComponent<PlayerAim>());
            SetRef(agent, "weapon", player.GetComponent<Weapon>());
            Modify(agent, "arenaHalfSize", p => p.vector2Value = new Vector2(HalfWidth, HalfHeight));

            go.AddComponent<DecisionRequester>().DecisionPeriod = 5;
        }

        /// <summary>A fixed camera showing the whole arena, for watching training in the editor.</summary>
        static void BuildOverviewCamera()
        {
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = HalfHeight + 1.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.04f, 0.06f);
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            cameraGo.AddComponent<AudioListener>();
        }
    }
}
