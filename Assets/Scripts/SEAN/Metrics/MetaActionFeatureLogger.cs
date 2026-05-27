
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

public class MetaActionFeatureLogger : MonoBehaviour
{
    [Header("Logging")]
    public bool enableLogging = true;
    public string runLabel = "default";
    public float logInterval = 0.1f;

    [Header("Goal")]
    public Transform goalTransform;
    public Vector3 fallbackGoalPosition = new Vector3(3.82f, 0.5f, -22.65f);

    [Header("Pedestrian Detection")]
    public string pedestrianTag = "";
    public float collisionDistanceThreshold = 0.35f;

    private Transform robotBaseLink;
    private List<Transform> pedestrians = new List<Transform>();

    private float nextLogTime = 0f;
    private float minDistanceToPedestrian = float.PositiveInfinity;
    private int collisionCount = 0;
    private bool currentlyInCollision = false;

    private string outputPath;
    private StreamWriter writer;

    void Start()
    {
        if (!enableLogging)
        {
            return;
        }

        TryFindRobotBaseLink();
        FindPedestrians();

        string folder = Path.Combine(Application.dataPath, "../Output/MetaActionFeatures");
        Directory.CreateDirectory(folder);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        outputPath = Path.Combine(folder, "meta_features_" + runLabel + "_" + timestamp + ".csv");

        writer = new StreamWriter(outputPath, false, Encoding.UTF8);

        writer.WriteLine(
            "time,run_label,robot_x,robot_y,robot_z,goal_x,goal_y,goal_z,dist_to_goal,min_dist_to_ped_so_far,current_min_dist_to_ped,collision_count,num_pedestrians,pedestrian_positions"
        );

        Debug.Log("MetaActionFeatureLogger writing to: " + outputPath);
    }

    void Update()
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

    private void TryFindRobotBaseLink()
    {
        if (SEAN.SEAN.instance != null &&
            SEAN.SEAN.instance.robot != null &&
            SEAN.SEAN.instance.robot.base_link != null)
        {
            robotBaseLink = SEAN.SEAN.instance.robot.base_link.transform;
        }
    }

    private void FindPedestrians()
    {
        pedestrians.Clear();

        // Most pedestrian agents inherit from SEAN.Scenario.Agents.Base.
        SEAN.Scenario.Agents.Base[] agents = GameObject.FindObjectsOfType<SEAN.Scenario.Agents.Base>();

        foreach (SEAN.Scenario.Agents.Base agent in agents)
        {
            if (agent != null && agent.gameObject.activeInHierarchy)
            {
                pedestrians.Add(agent.transform);
            }
        }

        // Optional fallback: if you use tags later.
        if (!string.IsNullOrEmpty(pedestrianTag))
        {
            GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(pedestrianTag);
            foreach (GameObject obj in taggedObjects)
            {
                if (obj != null && obj.activeInHierarchy && !pedestrians.Contains(obj.transform))
                {
                    pedestrians.Add(obj.transform);
                }
            }
        }
    }

    private void LogFrame()
    {
        Vector3 robotPos = robotBaseLink.position;
        Vector3 goalPos = goalTransform != null ? goalTransform.position : fallbackGoalPosition;

        float distToGoal = Vector3.Distance(
            new Vector3(robotPos.x, 0f, robotPos.z),
            new Vector3(goalPos.x, 0f, goalPos.z)
        );

        float currentMinDist = float.PositiveInfinity;

        foreach (Transform ped in pedestrians)
        {
            if (ped == null || !ped.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 pedPos = ped.position;
            float dist = Vector3.Distance(
                new Vector3(robotPos.x, 0f, robotPos.z),
                new Vector3(pedPos.x, 0f, pedPos.z)
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
            if (currentMinDist < minDistanceToPedestrian)
            {
                minDistanceToPedestrian = currentMinDist;
            }

            bool collisionNow = currentMinDist <= collisionDistanceThreshold;
            if (collisionNow && !currentlyInCollision)
            {
                collisionCount += 1;
            }
            currentlyInCollision = collisionNow;
        }

        string pedPositions = BuildPedestrianPositionString();

        writer.WriteLine(
            Time.time.ToString("F3") + "," +
            Escape(runLabel) + "," +
            robotPos.x.ToString("F4") + "," +
            robotPos.y.ToString("F4") + "," +
            robotPos.z.ToString("F4") + "," +
            goalPos.x.ToString("F4") + "," +
            goalPos.y.ToString("F4") + "," +
            goalPos.z.ToString("F4") + "," +
            distToGoal.ToString("F4") + "," +
            SafeFloat(minDistanceToPedestrian) + "," +
            currentMinDist.ToString("F4") + "," +
            collisionCount + "," +
            pedestrians.Count + "," +
            Escape(pedPositions)
        );

        writer.Flush();
    }

    private string BuildPedestrianPositionString()
    {
        List<string> parts = new List<string>();

        for (int i = 0; i < pedestrians.Count; i++)
        {
            Transform ped = pedestrians[i];
            if (ped == null || !ped.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 p = ped.position;
            parts.Add(i + ":" + p.x.ToString("F3") + "|" + p.y.ToString("F3") + "|" + p.z.ToString("F3"));
        }

        return string.Join(";", parts);
    }

    private string SafeFloat(float value)
    {
        if (float.IsInfinity(value) || float.IsNaN(value))
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

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    void OnApplicationQuit()
    {
        CloseWriter();
    }

    void OnDisable()
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
            Debug.Log("MetaActionFeatureLogger saved: " + outputPath);
        }
    }
}

