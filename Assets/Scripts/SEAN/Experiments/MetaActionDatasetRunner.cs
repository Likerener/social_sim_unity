using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SEAN.Scenario.Agents;

public class MetaActionDatasetRunner : MonoBehaviour
{
    [System.Serializable]
    public class RunConfig
    {
        public string runId;
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

    [Header("Scene References")]
    public MetaActionController metaActionController;
    public MetaActionFeatureLogger featureLogger;

    private List<RunConfig> configs = new List<RunConfig>();

    private void Start()
    {
        BuildDefaultConfigs();

        if (runOnStart)
        {
            StartCoroutine(RunAllConfigs());
        }
    }

    public void StartDatasetRun()
    {
        BuildDefaultConfigs();
        StartCoroutine(RunAllConfigs());
    }

    private void BuildDefaultConfigs()
    {
        configs.Clear();

        configs.Add(new RunConfig
        {
            runId = "auto_default",
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

        configs.Add(new RunConfig
        {
            runId = "auto_weak_robot_repulsion",
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

        configs.Add(new RunConfig
        {
            runId = "auto_strong_robot_repulsion",
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

        configs.Add(new RunConfig
        {
            runId = "auto_faster_pedestrians",
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

        configs.Add(new RunConfig
        {
            runId = "auto_stronger_social_force_A",
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

        configs.Add(new RunConfig
        {
            runId = "auto_larger_social_force_B",
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

    private IEnumerator RunAllConfigs()
    {
        Debug.Log("[MetaActionDatasetRunner] Starting automated dataset run.");

        for (int i = 0; i < configs.Count; i++)
        {
            RunConfig config = configs[i];

            Debug.Log("[MetaActionDatasetRunner] Starting run: " + config.runId);

            ApplyConfig(config);

            if (featureLogger != null)
            {
                featureLogger.runLabel = config.runId;
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

            yield return new WaitForSeconds(runDurationSeconds);

            Debug.Log("[MetaActionDatasetRunner] Finished run: " + config.runId);

            if (metaActionController != null)
            {
                metaActionController.currentAction = MetaActionController.MetaAction.Stop;
            }

            yield return new WaitForSeconds(delayBetweenRunsSeconds);
        }

        Parameters.ResetToDefault();

        Debug.Log("[MetaActionDatasetRunner] Finished all automated dataset runs.");
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
            "[MetaActionDatasetRunner] Applied config " + config.runId +
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

