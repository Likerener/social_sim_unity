

# Meta-Action Outcome Feature Summary

This summarizes generated outcome runs using the same initial setup and the same robot meta-action. The runs compare different social-force parameter settings.

## Setup

- Scene: Outdoor / CrossPath
- Robot action: fixed meta-action across all runs
- Goal definition for progress: goal is set ahead of the robot from its initial pose
- Baseline for pedestrian deviation: default parameter run
- Feature logger: `MetaActionFeatureLogger`

## Features

| Scenario | Robot meta action | Parameter setting | Min distance to pedestrians | Mean pedestrian deviation from default | Max pedestrian deviation from default | Progress along goal-ahead direction | Collision count |
|---|---|---|---:|---:|---:|---:|---:|
| CrossPath | fixed meta-action | default | 0.4810 m | 0.0000 m | 0.0000 m | 26.2281 m | 0 |
| CrossPath | fixed meta-action | weak_robot_repulsion | 0.6210 m | 2.9630 m | 12.1137 m | 26.8269 m | 0 |
| CrossPath | fixed meta-action | strong_robot_repulsion | 0.6962 m | 2.6020 m | 6.6350 m | 31.7501 m | 0 |
| CrossPath | fixed meta-action | faster_pedestrians | 10.3918 m | 5.9551 m | 17.6634 m | 29.7774 m | 0 |
| CrossPath | fixed meta-action | stronger_social_force_A | 11.0698 m | 3.2855 m | 8.6977 m | 19.4835 m | 0 |
| CrossPath | fixed meta-action | larger_social_force_B | 0.3600 m | 3.2185 m | 8.4648 m | 30.4453 m | 0 |

## Notes

- Collision count is 0 for all runs.
- The default run is used as the baseline for pedestrian-deviation comparison, so its deviation relative to itself is 0.
- Pedestrian deviation is computed by comparing pedestrian positions at matched timesteps against the default run.
- Robot progress is computed along a goal-ahead direction from the robot's initial pose.
- The new runs vary more social-force parameters beyond robot repulsion, including pedestrian speed, social-force strength `A`, and social-force range `B`.
