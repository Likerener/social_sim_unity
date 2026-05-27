

\# Meta-Action Outcome Feature Summary



This summarizes three generated outcome runs using the same initial setup and the same robot meta-action. The runs compare different social-force robot-repulsion parameter settings.



\## Setup



\- Scene: Outdoor / CrossPath

\- Robot action: fixed meta-action across all runs

\- Runs:

&#x20; - Default robot repulsion

&#x20; - Weak robot repulsion

&#x20; - Strong robot repulsion

\- Feature logger: `MetaActionFeatureLogger`



\## Features



| Run | Min distance to pedestrians | Mean pedestrian deviation from default | Max pedestrian deviation from default | Robot progress toward fallback goal | Collision count |

|---|---:|---:|---:|---:|---:|

| default | 0.6068 m | baseline | baseline | -55.4574 m | 0 |

| weak repulsion | 1.7025 m | 2.6744 m | 15.5391 m | -55.8763 m | 0 |

| strong repulsion | 0.4067 m | 3.3530 m | 9.3191 m | -66.8855 m | 0 |



\## Notes



\- Collision count is 0 for all three runs.

\- Pedestrian deviation is computed relative to the default run by comparing pedestrian positions at matched timesteps.

\- Robot progress is computed relative to the logger's fallback goal position. Because the robot is controlled by a fixed meta-action rather than goal-directed navigation, progress can be negative if the action moves the robot away from that fallback goal.

\- The min-distance and pedestrian-deviation features are currently the most useful for comparing outcome differences across parameter settings.





