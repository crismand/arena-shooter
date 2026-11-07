"""Compare the trained agent with a random and an idle one on the training build.

Each policy plays the same number of episodes. The agent reports every episode's survival time
(game seconds), kills, whether it died and the highest wave it reached through ML-Agents' stats
channel, and this script prints the means and how often it reached the first boss. The trained
model passes when it beats both baselines on mean survival and on mean kills; with --against, it
must also beat that earlier evaluation's trained model on both and reach the boss in most episodes.

    ml/.venv/Scripts/python ml/evaluate.py --model ml/results/m1_001/ArenaAgent.onnx
    ml/.venv/Scripts/python ml/evaluate.py --episodes 50 --json ml/results/m1_001/evaluation.json
    ml/.venv/Scripts/python ml/evaluate.py --model ml/results/m2_001/ArenaAgent.onnx --against ml/results/m1_001/evaluation_v1.3.json
"""

import argparse
import json
import statistics
import sys
import time
from pathlib import Path

import numpy as np
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.side_channel.stats_side_channel import StatsSideChannel

ROOT = Path(__file__).resolve().parent.parent
DEFAULT_BUILD = ROOT / "Builds" / "Training" / "ArenaTraining.exe"

# Must match the constants in ArenaAgent.cs.
SURVIVAL = "Arena/Survival Time"
KILLS = "Arena/Kills"
DIED = "Arena/Died"
WAVE = "Arena/Wave Reached"
# The first boss wave (WaveDirector's bossEvery).
BOSS_WAVE = 5


class IdlePolicy:
    name = "idle"

    def __call__(self, spec, steps):
        return spec.action_spec.empty_action(len(steps))


class RandomPolicy:
    name = "random"

    def __call__(self, spec, steps):
        return spec.action_spec.random_action(len(steps))


class OnnxPolicy:
    """Runs a model exported by mlagents-learn, taking its deterministic outputs when it has them."""

    name = "trained"

    def __init__(self, path):
        import onnxruntime

        self.session = onnxruntime.InferenceSession(str(path), providers=["CPUExecutionProvider"])
        self.inputs = [i.name for i in self.session.get_inputs()]
        outputs = {o.name for o in self.session.get_outputs()}
        self.continuous = self._pick(outputs, "continuous_actions")
        self.discrete = self._pick(outputs, "discrete_actions")

    @staticmethod
    def _pick(outputs, name):
        for candidate in ("deterministic_" + name, name):
            if candidate in outputs:
                return candidate
        return None

    def __call__(self, spec, steps):
        n = len(steps)
        feed = {}
        for name in self.inputs:
            if name.startswith("obs_"):
                feed[name] = steps.obs[int(name[4:])].astype(np.float32)
            elif name == "action_masks":
                feed[name] = np.ones((n, sum(spec.action_spec.discrete_branches)), dtype=np.float32)
        wanted = [o for o in (self.continuous, self.discrete) if o]
        result = dict(zip(wanted, self.session.run(wanted, feed)))

        action = spec.action_spec.empty_action(n)
        if self.continuous:
            action.add_continuous(np.clip(result[self.continuous], -1.0, 1.0).astype(np.float32))
        if self.discrete:
            action.add_discrete(np.asarray(result[self.discrete]).astype(np.int32).reshape(n, -1))
        return action


def play(build, policy, episodes, seed, worker_id, time_scale):
    engine = EngineConfigurationChannel()
    stats = StatsSideChannel()
    env = UnityEnvironment(
        file_name=str(build), worker_id=worker_id, seed=seed, no_graphics=True, side_channels=[engine, stats]
    )
    collected = {SURVIVAL: [], KILLS: [], DIED: [], WAVE: []}
    try:
        engine.set_configuration_parameters(time_scale=time_scale)
        env.reset()
        behavior = next(iter(env.behavior_specs))
        spec = env.behavior_specs[behavior]
        # An episode's stats arrive when the next one begins.
        while len(collected[KILLS]) < episodes:
            decisions, _ = env.get_steps(behavior)
            if len(decisions) > 0:
                env.set_actions(behavior, policy(spec, decisions))
            env.step()
            for key, values in stats.get_and_reset_stats().items():
                if key in collected:
                    collected[key].extend(value for value, _ in values)
    finally:
        env.close()
    return {key: values[:episodes] for key, values in collected.items()}


def summarise(name, results):
    survival, kills, died, waves = results[SURVIVAL], results[KILLS], results[DIED], results[WAVE]
    return {
        "policy": name,
        "episodes": len(kills),
        "survival_mean": statistics.fmean(survival),
        "survival_stdev": statistics.stdev(survival) if len(survival) > 1 else 0.0,
        "kills_mean": statistics.fmean(kills),
        "kills_stdev": statistics.stdev(kills) if len(kills) > 1 else 0.0,
        "death_rate": statistics.fmean(died),
        "wave_mean": statistics.fmean(waves),
        "boss_rate": statistics.fmean(1.0 if w >= BOSS_WAVE else 0.0 for w in waves),
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--model", type=Path, help="exported .onnx to evaluate; without it, only the baselines run")
    parser.add_argument("--env", type=Path, default=DEFAULT_BUILD, help="training build (default: %(default)s)")
    parser.add_argument("--episodes", type=int, default=20, help="episodes per policy (default: %(default)s)")
    parser.add_argument("--seed", type=int, default=1)
    parser.add_argument("--time-scale", type=float, default=20.0)
    parser.add_argument("--worker-id", type=int, default=10, help="port offset, clear of a training run's (default: %(default)s)")
    parser.add_argument("--json", type=Path, help="also write the summary here")
    parser.add_argument("--against", type=Path, help="an earlier --json whose trained model this one must beat")
    args = parser.parse_args()

    if not args.env.exists():
        sys.exit(f"No training build at {args.env}. Build it with AS > Build > Training.")
    policies = [IdlePolicy(), RandomPolicy()]
    if args.model:
        if not args.model.exists():
            sys.exit(f"No model at {args.model}.")
        policies.append(OnnxPolicy(args.model))
    earlier = None
    if args.against:
        if not args.model:
            sys.exit("--against needs --model.")
        earlier = json.loads(args.against.read_text())["results"][-1]
        earlier["policy"] = "earlier"

    rows = []
    for policy in policies:
        started = time.monotonic()
        results = play(args.env, policy, args.episodes, args.seed, args.worker_id, args.time_scale)
        rows.append(summarise(policy.name, results))
        print(f"{policy.name}: {args.episodes} episodes in {time.monotonic() - started:.0f} s", file=sys.stderr)

    print(f"\n{'policy':<8} {'episodes':>8} {'survival (s)':>16} {'kills':>14} {'died':>6} {'wave':>6} {'boss':>6}")
    for r in rows + ([earlier] if earlier else []):
        print(
            f"{r['policy']:<8} {r['episodes']:>8} "
            f"{r['survival_mean']:>8.1f} +/- {r['survival_stdev']:<5.1f} "
            f"{r['kills_mean']:>6.1f} +/- {r['kills_stdev']:<5.1f} "
            f"{r['death_rate']:>6.0%} "
            f"{r.get('wave_mean', float('nan')):>6.1f} {r.get('boss_rate', float('nan')):>6.0%}"
        )

    if args.model:
        trained, baselines = rows[-1], rows[:-1]
        beats = all(
            trained["survival_mean"] > b["survival_mean"] and trained["kills_mean"] > b["kills_mean"] for b in baselines
        )
        print(f"\nTrained beats both baselines on survival and kills: {'yes' if beats else 'no'}")
        if earlier:
            better = trained["survival_mean"] > earlier["survival_mean"] and trained["kills_mean"] > earlier["kills_mean"]
            print(f"Trained beats the earlier model on survival and kills: {'yes' if better else 'no'}")
            print(f"Trained reaches the first boss in most episodes: {'yes' if trained['boss_rate'] > 0.5 else 'no'}")

    if args.json:
        args.json.parent.mkdir(parents=True, exist_ok=True)
        args.json.write_text(json.dumps({"model": str(args.model) if args.model else None, "results": rows}, indent=2))


if __name__ == "__main__":
    main()
