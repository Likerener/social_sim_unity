\# Meta-Action Outcome Feature Summary



This summarizes three generated outcome runs using the same initial setup and the same robot meta-action. The runs compare different social-force robot-repulsion parameter settings.



\## Setup



\- Scene: Outdoor / CrossPath

\- Robot action: fixed meta-action across all runs

\- Goal definition for progress: goal is set ahead of the robot from its initial pose

\- Baseline for pedestrian deviation: default robot-repulsion run

\- Feature logger: `MetaActionFeatureLogger`



\## Features



| Run | Min distance to pedestrians | Mean pedestrian deviation from default | Max pedestrian deviation from default | Progress along goal-ahead direction | Collision count |

|---|---:|---:|---:|---:|---:|

| default | 0.4810 m | baseline | baseline | 26.2281 m | 0 |

| weak repulsion | 0.6210 m | 2.9630 m | 12.1137 m | 26.8269 m | 0 |

| strong repulsion | 0.6962 m | 2.6020 m | 6.6350 m | 31.7501 m | 0 |



\## Notes



\- Collision count is 0 for all three runs.

\- Pedestrian deviation is computed relative to the default robot-repulsion run by comparing pedestrian positions at matched timesteps.

\- Robot progress is computed along a goal-ahead direction from the robot's initial pose, instead of using the old fallback goal position.

\- The next step is to expand the generated outcome set by varying more social-force parameters beyond robot repulsion.



