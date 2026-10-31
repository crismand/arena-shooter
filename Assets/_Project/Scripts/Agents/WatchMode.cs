using AS.Core;
using AS.Player;
using UnityEngine;

namespace AS.Agents
{
    /// <summary>
    /// Watch AI in the game: while the game is <see cref="GameState.Watching"/>, the player takes its
    /// commands from <see cref="AgentInput"/> and the agent object is switched on to play with the
    /// shipped model; otherwise the agent is off and the person is back in control. Listens to game
    /// state on the global bus, so the game itself never references ML-Agents.
    /// The agent object is saved inactive, so ML-Agents doesn't start at all until someone watches.
    /// </summary>
    public class WatchMode : MonoBehaviour
    {
        [SerializeField] GameObject agent;
        [SerializeField] PlayerController player;
        [SerializeField] PlayerAim aim;
        [SerializeField] HumanPlayerInput humanInput;
        [SerializeField] AgentInput agentInput;

        public bool IsWatching { get; private set; }

        void OnEnable() => EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        void OnDisable() => EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);

        void OnGameStateChanged(GameStateChanged evt) => SetWatching(evt.Current == GameState.Watching);

        void SetWatching(bool watching)
        {
            if (watching == IsWatching) return;
            IsWatching = watching;
            agentInput.Clear();
            IPlayerInput source = watching ? agentInput : humanInput;
            player.InputSource = source;
            aim.InputSource = source;
            agent.SetActive(watching);
        }
    }
}
