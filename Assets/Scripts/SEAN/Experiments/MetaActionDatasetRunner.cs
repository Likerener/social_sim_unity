

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SEAN.Scenario.Agents;

public class MetaActionDatasetRunner : MonoBehaviour
{
    [System.Serializable]
    public class RunConfig
    {
        public string parameterId;
        public MetaActionController.MetaAction action;

        public float desiredSpeed;
        public float maxVel;
        public float A;
        public float B;
        public float T;
        public float lateralDampening;
        public float robotRepulsionMin;
        public float robotRepulsionMax;
    }

    [Header("Runner Settings")]
    public bool runOnStart = false;
    public float runDurationSeconds = 20.0f;
    public float delayBetweenRunsSeconds = 2.0f;

    [Header("Scenario Settings")]
    public string[] scenarioTaskNames = new string[]
    {
        "Random",
        "JoinGroup",
        "LeaveGroup"
    };

    [Header("Scene References")]
    public MetaActionController metaActionController;
    public MetaActionFeatureLogger featureLogger;

    private SEAN.SEAN sean;
    private List<RunConfig> parameterConfigs = new List<RunConfig>();

    private void Start()
    {
        sean = SEAN.SEAN.instance;
        BuildParameterConfigs();

        if (runOnStart)
        {
            StartCoroutine(RunAllScenariosAndConfigs());
        }
    }

    public void StartDatasetRun()
    {
        sean = SEAN.SEAN.instance;
        BuildParameterConfigs();
        StartCoroutine(RunAllScenariosAndConfigs());
    }

    private void BuildParameterConfigs()
    {
        parameterConfigs.Clear();

        parameterConfigs.Add(new RunConfig
        {
            parameterId = "default",
            action = MetaActionController.MetaAction.Straight,
            desiredSpeed = 0.6f,
            maxVel = 0.6f,
            A = 2000f / 4f,
            B = 0.08f * 2f,
            T = 0.5f,
            lateralDampening = 5f,
            robotRepulsionMin = 0.5f,
            robotRepulsionMax = 1.0f
        });

        parameterConfigs.Add(new RunConfig
        {
            parameterId = "weak_robot_repulsion",
            action = MetaActionController.MetaAction.Straight,
            desiredSpeed = 0.6f,
            maxVel = 0.6f,
            A = 2000f / 4f,
            B = 0.08f * 2f,
            T = 0.5f,
            lateralDampening = 5f,
            robotRepulsionMin = 0.1f,
            robotRepulsionMax = 0.3f
        });

        parameterConfigs.Add(new RunConfig
        {
            parameterId = "strong_robot_repulsion",
            action = MetaActionController.MetaAction.Straight,
            desiredSpeed = 0.6f,
            maxVel = 0.6f,
            A = 2000f / 4f,
            B = 0.08f * 2f,
            T = 0.5f,
            lateralDampening = 5f,
            robotRepulsionMin = 2.0f,
            robotRepulsionMax = 3.0f
        });

        parameterConfigs.Add(new RunConfig
        {
            parameterId = "faster_pedestrians",
            action = MetaActionController.MetaAction.Straight,
            desiredSpeed = 0.9f,
            maxVel = 0.9f,
            A = 2000f / 4f,
            B = 0.08f * 2f,
            T = 0.5f,
            lateralDampening = 5f,
            robotRepulsionMin = 0.5f,
            robotRepulsionMax = 1.0f
        });

        parameterConfigs.Add(new RunConfig
        {
            parameterId = "stronger_social_force_A",
            action = MetaActionController.MetaAction.Straight,
            desiredSpeed = 0.6f,
            maxVel = 0.6f,
            A = 2000f,
            B = 0.08f * 2f,
            T = 0.5f,
            lateralDampening = 5f,
            robotRepulsionMin = 0.5f,
            robotRepulsionMax = 1.0f
        });

        parameterConfigs.Add(new RunConfig
        {
            parameterId = "larger_social_force_B",
            action = MetaActionController.MetaAction.Straight,
            desiredSpeed = 0.6f,
            maxVel = 0.6f,
            A = 2000f / 4f,
            B = 0.08f * 4f,
            T = 0.5f,
            lateralDampening = 5f,
            robotRepulsionMin = 0.5f,
            robotRepulsionMax = 1.0f
        });
    }

    private IEnumerator RunAllScenariosAndConfigs()
    {
        Debug.Log("[MetaActionDatasetRunner] Starting automated multi-scenario dataset run.");

        for (int scenarioIndex = 0; scenarioIndex < scenarioTaskNames.Length; scenarioIndex++)
        {
            string scenarioName = scenarioTaskNames[scenarioIndex];

            Debug.Log("[MetaActionDatasetRunner] Switching scenario/task to: " + scenarioName);

            bool taskSet = SetScenarioTask(scenarioName);

            if (!taskSet)
            {
                Debug.LogError("[MetaActionDatasetRunner] Failed to set scenario/task: " + scenarioName);
                continue;
            }

            yield return new WaitForSeconds(delayBetweenRunsSeconds);

            for (int configIndex = 0; configIndex < parameterConfigs.Count; configIndex++)
            {
                RunConfig config = parameterConfigs[configIndex];

                string runLabel = "auto_" + scenarioName + "_" + config.parameterId;

                Debug.Log("[MetaActionDatasetRunner] Starting run: " + runLabel);

                ApplyConfig(config);

                if (featureLogger != null)
                {
                    featureLogger.runLabel = runLabel;
                }
                else
                {
                    Debug.LogWarning("[MetaActionDatasetRunner] Feature logger is not assigned.");
                }

                if (metaActionController != null)
                {
                    metaActionController.currentAction = config.action;
                }
                else
                {
                    Debug.LogWarning("[MetaActionDatasetRunner] Meta action controller is not assigned.");
                }

                StartCurrentTaskSafely(runLabel);

                yield return new WaitForSeconds(runDurationSeconds);

                Debug.Log("[MetaActionDatasetRunner] Finished run: " + runLabel);

                if (metaActionController != null)
                {
                    metaActionController.currentAction = MetaActionController.MetaAction.Stop;
                }

                yield return new WaitForSeconds(delayBetweenRunsSeconds);
            }
        }

        Parameters.ResetToDefault();

        if (metaActionController != null)
        {
            metaActionController.currentAction = MetaActionController.MetaAction.Stop;
        }

        Debug.Log("[MetaActionDatasetRunner] Finished all automated multi-scenario dataset runs.");
    }

    private bool SetScenarioTask(string taskName)
    {
        if (sean == null)
        {
            sean = SEAN.SEAN.instance;
        }

        if (sean == null)
        {
            Debug.LogError("[MetaActionDatasetRunner] Could not find SEAN instance.");
            return false;
        }

        try
        {
            sean.SetTask(taskName);

            if (sean.robotTask == null)
            {
                Debug.LogError("[MetaActionDatasetRunner] Task was set, but sean.robotTask is null: " + taskName);
                return false;
            }

            Debug.Log("[MetaActionDatasetRunner] Active robot task is now: " + sean.robotTask.name);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("[MetaActionDatasetRunner] Error setting task " + taskName + ": " + e.Message);
            return false;
        }
    }

    private void StartCurrentTaskSafely(string runLabel)
    {
        if (sean == null)
        {
            sean = SEAN.SEAN.instance;
        }

        if (sean == null)
        {
            Debug.LogWarning("[MetaActionDatasetRunner] Cannot start task for " + runLabel + " because SEAN instance is null.");
            return;
        }

        if (sean.robotTask == null)
        {
            Debug.LogWarning("[MetaActionDatasetRunner] Cannot start task for " + runLabel + " because robotTask is null.");
            return;
        }

        try
        {
            sean.robotTask.StartNewTask();
            Debug.Log("[MetaActionDatasetRunner] Started task for run: " + runLabel);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[MetaActionDatasetRunner] Could not start task for run " + runLabel + ": " + e.Message);
        }
    }

    private void ApplyConfig(RunConfig config)
    {
        Parameters.DESIRED_SPEED = config.desiredSpeed;
        Parameters.MAX_VEL = config.maxVel;
        Parameters.A = config.A;
        Parameters.B = config.B;
        Parameters.T = config.T;
        Parameters.LATERAL_DAMPENING = config.lateralDampening;
        Parameters.ROBOT_REPULSION_DAMPENING_MIN = config.robotRepulsionMin;
        Parameters.ROBOT_REPULSION_DAMPENING_MAX = config.robotRepulsionMax;

        Debug.Log(
            "[MetaActionDatasetRunner] Applied config " + config.parameterId +
            " | DESIRED_SPEED=" + Parameters.DESIRED_SPEED +
            " | MAX_VEL=" + Parameters.MAX_VEL +
            " | A=" + Parameters.A +
            " | B=" + Parameters.B +
            " | T=" + Parameters.T +
            " | LATERAL_DAMPENING=" + Parameters.LATERAL_DAMPENING +
            " | ROBOT_REPULSION_MIN=" + Parameters.ROBOT_REPULSION_DAMPENING_MIN +
            " | ROBOT_REPULSION_MAX=" + Parameters.ROBOT_REPULSION_DAMPENING_MAX
        );
    }
}

