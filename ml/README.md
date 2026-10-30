# ml

Python side of the ML-Agents player: training config, scripts and the pinned environment. The plan, agent design and training setup are on the wiki's [ML Agent](https://github.com/crismand/arena-shooter/wiki/ML-Agent) page.

## Versions

| Unity package | Python package | Python | PyTorch |
|---|---|---|---|
| `com.unity.ml-agents` 4.1.0 | `mlagents` 1.1.0 | 3.10.12 | 2.5.1 (CUDA 12.1) |

The Unity and Python packages are released in pairs, so upgrade them together: `Packages/manifest.json` and `requirements.txt` in the same change. The pairs are listed in the ML-Agents installation docs.

## Setup

Install Python 3.10.12, then from the repo root:

```powershell
py -3.10 -m venv ml/.venv
ml/.venv/Scripts/python -m pip install -r ml/requirements.txt
ml/.venv/Scripts/mlagents-learn --help
```

If the `grpcio` wheel fails to build, install `grpcio==1.48.2` first and rerun the last install.

`ml/.venv/` and `ml/results/` are git-ignored.
