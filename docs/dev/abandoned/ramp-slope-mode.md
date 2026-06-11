# Ramp Slope Mode (Abandoned)

## Overview

This feature was an attempt to allow players to construct transports (belts and pipes) on a continuous 25% incline/decline, matching the slope of artificial vehicle ramps.

It was abandoned because it inherently violates the core principle of Designer Toolkit: **"Designer-only, consumer-free"**.

## Findings

Vanilla Captain of Industry has a strict transport pathfinding and trajectory building system. The `TransportProto` sets `ZStepLength = 2`, which corresponds to a 50% ramp. A vehicle ramp is a 25% slope (dropping 1 Z level over 4 X tiles). Because vanilla enforces `ZStepLength = 2`, it natively renders 25% slopes as stairs (2 tiles of 50% ramp, 2 tiles of flat belt).

To create a continuous 25% slope, the mod needed to:
1. Dynamically change `ZStepLength` to 4 for affected prototypes.
2. Patch `TransportPathFinder.InitPathFinding` to bypass the `m_startMustBeFlat` and `m_goalMustBeFlat` restrictions, allowing paths to start/end on a slope.
3. Patch `TransportTrajectory.TryCreateFromPivots` to force `allowDenormalizedStartEndDirections = true`, allowing the game to preserve the Z-axis slope on the ends of the trajectory.
4. Patch `TransportTrajectory.ComputeStartAndEndDirections` to calculate vectors with Z components instead of flattening them to 0.

### The Problem

If a player places a blueprint containing a continuous 25% slope in a *vanilla* game, the vanilla pathfinder reconstructs the visual trajectory from the pivot data in the blueprint. 

However, because the vanilla engine:
- Defaults to `allowDenormalizedStartEndDirections = false`, it strips the vertical direction from the start and end tiles, flattening the ends.
- Uses strict coarse terrain collision checks (`isCollidingWithTerrain` relying on discrete tile elevations), it often rejects belts that tightly hug a 25% terrain slope, citing terrain collision errors.
- Runs strict occupancy checks, making it impossible to draw pillars down the exact center of a vehicle ramp building.

If we bypassed the collision and occupancy checks in the mod to let the player build a ground-hugging slope, the resulting blueprint would be rejected by vanilla's collision checks and fail to build. 

If we kept the collision checks, the player was forced to elevate the belt 1 Z-level above the slope to clear the bounding box, which looked awkward, and the vanilla game still flattened the ends.

Since every feature in Designer Toolkit must produce blueprints that are 100% playable and valid in a vanilla game, this feature was incompatible with the mod's scope.

## Code Preservation

The core logic remains in the repository as `BDT.RampSlopeMode.cs` for backup purposes, but all settings, localization, and initialization hooks have been stripped out. It will be moved to an independent standalone mod at a later date.
