

\# Rerun Instructions for Meta-Action Dataset



This document explains how to rerun the saved meta-action scenarios in Unity.



\## Environment



\- Unity version: 2020.3.48f1

\- Scene: `Assets/Scenes/SEAN/Outdoor.unity`

\- Dataset folder: `Output/MetaActionDataset/`

\- Scenario config file: `Output/MetaActionDataset/scenario\_configs.csv`

\- Feature logger: `MetaActionFeatureLogger`



\## General Rerun Procedure



For each row in `scenario\_configs.csv`:



1\. Open the Unity project.

2\. Open the Outdoor scene.

3\. Set the robot meta-action to the saved `robot\_meta\_action`.

4\. Set the social-force parameters in `Assets/Scripts/Agents/Parameters.cs`.

5\. Set the `Run Label` in `MetaActionFeatureLogger` to the `run\_id`.

6\. Make sure `Use Goal Ahead Of Robot` is enabled.

7\. Press Play.

8\. Let the scenario run.

9\. Press Stop.

10\. The new trajectory/features CSV will be saved under:



`Output/MetaActionFeatures/`



\## Parameter Settings



Default parameters:



```csharp

public const float DESIRED\_SPEED = 0.6f;

public const float T = 0.5f;

public const float A = 2000f / 4;

public const float B = 0.08f \* 2;

public const float MAX\_VEL = 0.6f;

public const float LATERAL\_DAMPENING = 5;

public const float ROBOT\_REPULSION\_DAMPENING\_MIN = 0.5f;

public const float ROBOT\_REPULSION\_DAMPENING\_MAX = 1.0f;

```






\### default



Use the default parameters above.



\### weak\_robot\_repulsion



```csharp

public const float ROBOT\_REPULSION\_DAMPENING\_MIN = 0.1f;

public const float ROBOT\_REPULSION\_DAMPENING\_MAX = 0.3f;

```



\### strong\_robot\_repulsion



```csharp

public const float ROBOT\_REPULSION\_DAMPENING\_MIN = 2.0f;

public const float ROBOT\_REPULSION\_DAMPENING\_MAX = 3.0f;

```



\### faster\_pedestrians



```csharp

public const float DESIRED\_SPEED = 0.9f;

public const float MAX\_VEL = 0.9f;

```



\### stronger\_social\_force\_A



```csharp

public const float A = 2000f;

```



\### larger\_social\_force\_B



```csharp

public const float B = 0.08f \* 4;

```



\## Saved Dataset Files



The already generated runs are saved in:



`Output/MetaActionDataset/trajectories/`



The extracted run-level features are saved in:



`Output/MetaActionDataset/meta\_action\_outcome\_summary.csv`



The scenario/config settings are saved in:



`Output/MetaActionDataset/scenario\_configs.csv`



\## Notes



The default run is used as the baseline for pedestrian deviation, so its pedestrian deviation relative to itself is 0.



The weak/strong robot-repulsion values were recorded from the manual run setup. The current saved dataset uses:



\- weak\_robot\_repulsion: min = 0.1, max = 0.3

\- strong\_robot\_repulsion: min = 2.0, max = 3.0



\## Current Limitation



This is currently a manual rerun procedure. A future improvement is to add an automated scenario runner that reads `scenario\_configs.csv`, applies parameter settings automatically, runs each scenario, and regenerates trajectory/features.





