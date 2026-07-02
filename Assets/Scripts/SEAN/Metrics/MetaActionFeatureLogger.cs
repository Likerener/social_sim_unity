using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

public class MetaActionFeatureLogger : MonoBehaviour
{
    [Header("Logging")]
    public bool enableLogging = true;

    // Keep this true for old/manual experiments.
    // Set it to false when using the rollout runner.
    public bool autoStartLogging = true;

    public string runLabel = "default";
    public float logInterval = 0.1f;

    [Header("Rollout Metadata")]
    public string scenarioId = "manual_scenario";
    public string metaActionName = "Straight";
    public int rolloutId = 0;
    public int rolloutSeed = 0;

    [Header("Goal / Progress")]
    public bool useGoalAheadOfRobot = true;
    public float goalDistanceAhead = 20.0f;
    public Transform goalTransform;
    public Vector3 fallbackGoalPosition =
        new Vector3(3.82f, 0.5f, -22.65f);

    [Header("Pedestrian Detection")]
    public string pedestrianTag = "";
    public float collisionDistanceThreshold = 0.35f;

    private Transform robotBaseLink;
    private readonly List<Transform> pedestrians =
        new List<Transform>();

    private float nextLogTime;
    private float rolloutStartTime;

    private float minDistanceToPedestrian =
        float.PositiveInfinity;

    private int collisionCount;
    private bool currentlyInCollision;

    private Vector3 startRobotPosition;
    private Vector3 goalPosition;
    private bool goalInitialized;

    // Path smoothness is represented as cumulative absolute
    // change in robot heading, measured in radians.
    // Lower values indicate a smoother trajectory.
    private bool previousHeadingInitialized;
    private float previousHeadingDegrees;
    private float cumulativeHeadingChangeRadians;

    private string outputPath;
    private StreamWriter writer;

    private void Start()
    {
        TryFindRobotBaseLink();
        FindPedestrians();

        if (autoStartLogging && enableLogging)
        {
            BeginRollout(
                scenarioId,
                metaActionName,
                rolloutId,
                rolloutSeed,
                runLabel
            );
        }
    }

    private void Update()
    {
        if (!enableLogging || writer == null)
        {
            return;
        }

        if (robotBaseLink == null)
        {
            TryFindRobotBaseLink();

            if (robotBaseLink == null)
            {
                return;
            }
        }

        if (!goalInitialized)
        {
            InitializeGoal();
        }

        if (Time.time < nextLogTime)
        {
            return;
        }

        nextLogTime = Time.time + logInterval;

        if (pedestrians.Count == 0)
        {
            FindPedestrians();
        }

        LogFrame();
    }

    public void BeginRollout(
        string newScenarioId,
        string newMetaActionName,
        int newRolloutId,
        int newRolloutSeed,
        string newRunLabel
    )
    {
        CloseWriter();

        enableLogging = true;

        scenarioId = newScenarioId;
        metaActionName = newMetaActionName;
        rolloutId = newRolloutId;
        rolloutSeed = newRolloutSeed;
        runLabel = newRunLabel;

        ResetRolloutState();

        TryFindRobotBaseLink();
        FindPedestrians();

        if (robotBaseLink != null)
        {
            InitializeGoal();
        }

        string folder = Path.Combine(
            Application.dataPath,
            "../Output/MetaActionFeatures"
        );

        Directory.CreateDirectory(folder);

        string timestamp =
            DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");

        string safeScenario = SanitizeFileName(scenarioId);
        string safeAction = SanitizeFileName(metaActionName);
        string safeLabel = SanitizeFileName(runLabel);

        string fileName =
            "meta_features_" +
            safeScenario + "_" +
            safeAction + "_" +
            "rollout" + rolloutId + "_" +
            "seed" + rolloutSeed + "_" +
            safeLabel + "_" +
            timestamp +
            ".csv";

        outputPath = Path.Combine(folder, fileName);

        writer = new StreamWriter(
            outputPath,
            false,
            Encoding.UTF8
        );

        writer.WriteLine(
            "time," +
            "elapsed_time," +
            "run_label," +
            "scenario_id," +
            "meta_action," +
            "rollout_id," +
            "rollout_seed," +
            "robot_x," +
            "robot_y," +
            "robot_z," +
            "goal_x," +
            "goal_y," +
            "goal_z," +
            "dist_to_goal," +
            "progress_along_goal_direction," +
            "path_smoothness," +
            "min_dist_to_ped_so_far," +
            "current_min_dist_to_ped," +
            "collision_count," +
            "num_pedestrians," +
            "pedestrian_positions"
        );

        writer.Flush();

        Debug.Log(
            "[MetaActionFeatureLogger] Begin rollout: " +
            outputPath
        );
    }

    public void EndRollout()
    {
        CloseWriter();

        enableLogging = false;

        Debug.Log(
            "[MetaActionFeatureLogger] End rollout: " +
            runLabel
        );
    }

    public string GetCurrentOutputPath()
    {
        return outputPath;
    }

    private void ResetRolloutState()
    {
        nextLogTime = Time.time;
        rolloutStartTime = Time.time;

        minDistanceToPedestrian =
            float.PositiveInfinity;

        collisionCount = 0;
        currentlyInCollision = false;

        goalInitialized = false;

        previousHeadingInitialized = false;
        previousHeadingDegrees = 0f;
        cumulativeHeadingChangeRadians = 0f;

        pedestrians.Clear();
    }

    private void TryFindRobotBaseLink()
    {
        if (SEAN.SEAN.instance != null &&
            SEAN.SEAN.instance.robot != null &&
            SEAN.SEAN.instance.robot.base_link != null)
        {
            robotBaseLink =
                SEAN.SEAN.instance.robot.base_link.transform;
        }
    }

    private void InitializeGoal()
    {
        if (robotBaseLink == null)
        {
            return;
        }

        startRobotPosition = robotBaseLink.position;

        if (useGoalAheadOfRobot)
        {
            Vector3 forward = robotBaseLink.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();

            goalPosition =
                startRobotPosition +
                forward * goalDistanceAhead;

            goalPosition.y = startRobotPosition.y;
        }
        else if (goalTransform != null)
        {
            goalPosition = goalTransform.position;
        }
        else
        {
            goalPosition = fallbackGoalPosition;
        }

        goalInitialized = true;

        InitializeHeading();

        Debug.Log(
            "[MetaActionFeatureLogger] Goal position: " +
            goalPosition
        );
    }

    private void InitializeHeading()
    {
        if (robotBaseLink == null)
        {
            previousHeadingInitialized = false;
            return;
        }

        previousHeadingDegrees =
            robotBaseLink.eulerAngles.y;

        previousHeadingInitialized = true;
    }

    private void FindPedestrians()
    {
        pedestrians.Clear();

        SEAN.Scenario.Agents.Base[] agents =
            GameObject.FindObjectsOfType<
                SEAN.Scenario.Agents.Base
            >();

        foreach (
            SEAN.Scenario.Agents.Base agent in agents
        )
        {
            if (
                agent != null &&
                agent.gameObject.activeInHierarchy
            )
            {
                pedestrians.Add(agent.transform);
            }
        }

        if (!string.IsNullOrEmpty(pedestrianTag))
        {
            GameObject[] taggedObjects =
                GameObject.FindGameObjectsWithTag(
                    pedestrianTag
                );

            foreach (GameObject obj in taggedObjects)
            {
                if (
                    obj != null &&
                    obj.activeInHierarchy &&
                    !pedestrians.Contains(obj.transform)
                )
                {
                    pedestrians.Add(obj.transform);
                }
            }
        }
    }

    private void LogFrame()
    {
        Vector3 robotPos = robotBaseLink.position;
        Vector3 goalPos = goalPosition;

        float elapsedTime =
            Time.time - rolloutStartTime;

        float distToGoal = Vector3.Distance(
            new Vector3(
                robotPos.x,
                0f,
                robotPos.z
            ),
            new Vector3(
                goalPos.x,
                0f,
                goalPos.z
            )
        );

        Vector3 goalDirection =
            goalPos - startRobotPosition;

        goalDirection.y = 0f;

        float progressAlongGoalDirection = 0f;

        if (goalDirection.sqrMagnitude > 0.0001f)
        {
            goalDirection.Normalize();

            Vector3 displacement =
                robotPos - startRobotPosition;

            displacement.y = 0f;

            progressAlongGoalDirection =
                Vector3.Dot(
                    displacement,
                    goalDirection
                );
        }

        UpdatePathSmoothness();

        float currentMinDist =
            float.PositiveInfinity;

        foreach (Transform ped in pedestrians)
        {
            if (
                ped == null ||
                !ped.gameObject.activeInHierarchy
            )
            {
                continue;
            }

            Vector3 pedPos = ped.position;

            float dist = Vector3.Distance(
                new Vector3(
                    robotPos.x,
                    0f,
                    robotPos.z
                ),
                new Vector3(
                    pedPos.x,
                    0f,
                    pedPos.z
                )
            );

            if (dist < currentMinDist)
            {
                currentMinDist = dist;
            }
        }

        if (float.IsInfinity(currentMinDist))
        {
            currentMinDist = -1f;
        }
        else
        {
            if (
                currentMinDist <
                minDistanceToPedestrian
            )
            {
                minDistanceToPedestrian =
                    currentMinDist;
            }

            bool collisionNow =
                currentMinDist <=
                collisionDistanceThreshold;

            if (
                collisionNow &&
                !currentlyInCollision
            )
            {
                collisionCount += 1;
            }

            currentlyInCollision =
                collisionNow;
        }

        string pedPositions =
            BuildPedestrianPositionString();

        writer.WriteLine(
            Time.time.ToString("F3") + "," +
            elapsedTime.ToString("F3") + "," +
            Escape(runLabel) + "," +
            Escape(scenarioId) + "," +
            Escape(metaActionName) + "," +
            rolloutId + "," +
            rolloutSeed + "," +
            robotPos.x.ToString("F4") + "," +
            robotPos.y.ToString("F4") + "," +
            robotPos.z.ToString("F4") + "," +
            goalPos.x.ToString("F4") + "," +
            goalPos.y.ToString("F4") + "," +
            goalPos.z.ToString("F4") + "," +
            distToGoal.ToString("F4") + "," +
            progressAlongGoalDirection.ToString("F4") + "," +
            cumulativeHeadingChangeRadians.ToString("F4") + "," +
            SafeFloat(minDistanceToPedestrian) + "," +
            currentMinDist.ToString("F4") + "," +
            collisionCount + "," +
            pedestrians.Count + "," +
            Escape(pedPositions)
        );

        writer.Flush();
    }

    private void UpdatePathSmoothness()
    {
        if (robotBaseLink == null)
        {
            return;
        }

        float currentHeadingDegrees =
            robotBaseLink.eulerAngles.y;

        if (!previousHeadingInitialized)
        {
            previousHeadingDegrees =
                currentHeadingDegrees;

            previousHeadingInitialized = true;
            return;
        }

        float headingChangeDegrees =
            Mathf.Abs(
                Mathf.DeltaAngle(
                    previousHeadingDegrees,
                    currentHeadingDegrees
                )
            );

        cumulativeHeadingChangeRadians +=
            headingChangeDegrees *
            Mathf.Deg2Rad;

        previousHeadingDegrees =
            currentHeadingDegrees;
    }

    private string BuildPedestrianPositionString()
    {
        List<string> parts =
            new List<string>();

        for (
            int i = 0;
            i < pedestrians.Count;
            i++
        )
        {
            Transform ped = pedestrians[i];

            if (
                ped == null ||
                !ped.gameObject.activeInHierarchy
            )
            {
                continue;
            }

            Vector3 p = ped.position;

            parts.Add(
                i + ":" +
                p.x.ToString("F3") + "|" +
                p.y.ToString("F3") + "|" +
                p.z.ToString("F3")
            );
        }

        return string.Join(";", parts);
    }

    private string SafeFloat(float value)
    {
        if (
            float.IsInfinity(value) ||
            float.IsNaN(value)
        )
        {
            return "-1";
        }

        return value.ToString("F4");
    }

    private string Escape(string value)
    {
        if (value == null)
        {
            return "";
        }

        return "\"" +
               value.Replace("\"", "\"\"") +
               "\"";
    }

    private string SanitizeFileName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "unknown";
        }

        foreach (
            char invalidChar in
            Path.GetInvalidFileNameChars()
        )
        {
            value =
                value.Replace(
                    invalidChar,
                    '_'
                );
        }

        return value.Replace(" ", "_");
    }

    private void OnApplicationQuit()
    {
        CloseWriter();
    }

    private void OnDisable()
    {
        CloseWriter();
    }

    private void CloseWriter()
    {
        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer = null;

            Debug.Log(
                "[MetaActionFeatureLogger] Saved: " +
                outputPath
            );
        }
    }
}

