<#
.SYNOPSIS
    Trains the arena agent with mlagents-learn and our defaults.
.DESCRIPTION
    Runs ml/config/arena_ppo.yaml against the training build (AS > Build > Training), several copies
    at once without graphics, at mlagents-learn's default time scale of 20. Results, checkpoints and
    the exported .onnx go to ml/results/<RunId>/. With -Editor it trains against the Unity Editor
    instead: open the Training scene and press Play when asked. Slow, but the place to debug rewards.
    Watch progress with: ml/.venv/Scripts/tensorboard --logdir ml/results
.EXAMPLE
    ml/train.ps1 -RunId m1_001
.EXAMPLE
    ml/train.ps1 -RunId m1_002 -InitializeFrom m1_001
#>
param(
    [Parameter(Mandatory)][string]$RunId,
    [int]$NumEnvs = 4,
    [string]$InitializeFrom,
    [switch]$Resume,
    [switch]$Editor,
    [ValidateSet('cpu', 'cuda')][string]$Device
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$learn = Join-Path $PSScriptRoot '.venv/Scripts/mlagents-learn.exe'
$build = Join-Path $root 'Builds/Training/ArenaTraining.exe'

if (-not (Test-Path $learn)) { throw "No mlagents-learn at $learn. Set up ml/.venv first (see ml/README.md)." }

$arguments = @(
    (Join-Path $PSScriptRoot 'config/arena_ppo.yaml'),
    "--run-id=$RunId",
    "--results-dir=$(Join-Path $PSScriptRoot 'results')"
)
if (-not $Editor) {
    if (-not (Test-Path $build)) { throw "No training build at $build. Build it with AS > Build > Training." }
    $arguments += "--env=$build", "--num-envs=$NumEnvs", '--no-graphics'
}
if ($Resume) { $arguments += '--resume' }
if ($InitializeFrom) { $arguments += "--initialize-from=$InitializeFrom" }
if ($Device) { $arguments += "--torch-device=$Device" }

& $learn @arguments
exit $LASTEXITCODE
