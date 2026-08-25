
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;


public class MetaActionDatasetRunner : MonoBehaviour
{
    private static MetaActionDatasetRunner activeRunner;


    [Serializable]
    public class ScenarioConfig
    {
        public string scenarioId;
        public string scenarioFamily;
        public string taskName;
        public int scenarioSeed;
    }


    private class TransformSnapshot
    {
        public Transform target;
        public Vector3 position;
        public Quaternion rotation;
        public bool activeSelf;
        public Rigidbody rigidbody;
    }


    [Header("Runner Control")]
    public bool runOnStart = false;
    public bool isRunning = false;


    [Header("Pilot Configuration")]
    public string runId = "pilot_50x7x2";
    public int numberOfScenarios = 50;
    public int rolloutsPerScenarioAction = 2;


    [Header("Timing")]
    public float rolloutDurationSeconds = 10.0f;
    public float taskInitializationWaitSeconds = 2.0f;
    public float pedestrianInitializationWaitSeconds = 2.0f;
    public float delayBetweenRolloutsSeconds = 1.0f;
    public float goalRepublishWaitSeconds = 0.25f;


    [Header("Task Retry")]
    public int maximumTaskStartAttempts = 8;
    public float taskRetryWaitSeconds = 1.0f;


    [Header("Scene References")]
    public MetaActionController metaActionController;
    public MetaActionFeatureLogger featureLogger;



    [Header("Automatic Video Recording")]
    public SEANAutoVideoRecorder videoRecorder;
    public bool recordVideos = true;
    private SEAN.SEAN sean;
    private bool ownsRunnerLock = false;


    private readonly List<ScenarioConfig> scenarios = new List<ScenarioConfig>();
    private readonly List<MetaActionController.MetaAction> actions =
        new List<MetaActionController.MetaAction>();
    private readonly List<TransformSnapshot> pedestrianSnapshots =
        new List<TransformSnapshot>();


    private string manifestPath;
    private int completedRollouts = 0;
    private int failedRollouts = 0;
    private int totalPlannedRollouts = 0;


    private Vector3 fixedRobotStartPosition;
    private Quaternion fixedRobotStartRotation;
    private Vector3 fixedRobotGoalPosition;
    private Quaternion fixedRobotGoalRotation;
    private bool fixedRobotStartActive;
    private bool fixedRobotGoalActive;
    private bool scenarioSnapshotReady = false;


    private void Awake()
    {
        if (activeRunner != null && activeRunner != this)
        {
            Debug.LogWarning("[MetaActionDatasetRunner] Duplicate runner disabled: " + gameObject.name);
            enabled = false;
            return;
        }


        activeRunner = this;
        ownsRunnerLock = true;
    }


    private void Start()
    {
        if (!ownsRunnerLock) return;


        sean = SEAN.SEAN.instance;
        BuildScenarioConfigs();
        BuildActions();


        if (featureLogger != null)
        {
            featureLogger.autoStartLogging = false;
            featureLogger.enableLogging = false;
        }


        if (runOnStart) StartDatasetRun();
    }


    public void StartDatasetRun()
    {
        if (!ownsRunnerLock || isRunning) return;


        sean = SEAN.SEAN.instance;
        BuildScenarioConfigs();
        BuildActions();
        StartCoroutine(RunPilotDataset());
    }


    private void BuildScenarioConfigs()
    {
        scenarios.Clear();


        int busyCount = 20;
        int joinCount = 15;
        int leaveCount = 15;
        int scenarioNumber = 1;


        for (int i = 0; i < busyCount; i++)
        {
            scenarios.Add(new ScenarioConfig
            {
                scenarioId = "scenario_" + scenarioNumber.ToString("D3") + "_busy_ab_nav",
                scenarioFamily = "busy_ab_nav",
                taskName = "BusyABNav",
                scenarioSeed = 100000 + i * 100
            });
            scenarioNumber++;
        }


        for (int i = 0; i < joinCount; i++)
        {
            scenarios.Add(new ScenarioConfig
            {
                scenarioId = "scenario_" + scenarioNumber.ToString("D3") + "_join_group",
                scenarioFamily = "join_group",
                taskName = "JoinGroup",
                scenarioSeed = 200000 + i * 100
            });
            scenarioNumber++;
        }


        for (int i = 0; i < leaveCount; i++)
        {
            scenarios.Add(new ScenarioConfig
            {
                scenarioId = "scenario_" + scenarioNumber.ToString("D3") + "_leave_group",
                scenarioFamily = "leave_group",
                taskName = "LeaveGroup",
                scenarioSeed = 300000 + i * 100
            });
            scenarioNumber++;
        }


        if (numberOfScenarios < scenarios.Count)
        {
            scenarios.RemoveRange(numberOfScenarios, scenarios.Count - numberOfScenarios);
        }
    }


    private void BuildActions()
    {
        actions.Clear();
        actions.Add(MetaActionController.MetaAction.Straight);
        actions.Add(MetaActionController.MetaAction.SlowDown);
        actions.Add(MetaActionController.MetaAction.Stop);
        actions.Add(MetaActionController.MetaAction.Left);
        actions.Add(MetaActionController.MetaAction.Right);
        actions.Add(MetaActionController.MetaAction.ForwardLeft);
        actions.Add(MetaActionController.MetaAction.ForwardRight);
    }


    private IEnumerator RunPilotDataset()
    {
        isRunning = true;
        completedRollouts = 0;
        failedRollouts = 0;
        totalPlannedRollouts =
            scenarios.Count * actions.Count * rolloutsPerScenarioAction;


        if (!ValidateReferences())
        {
            isRunning = false;
            yield break;
        }


        CreateManifest();


        for (int scenarioIndex = 0; scenarioIndex < scenarios.Count; scenarioIndex++)
        {
            ScenarioConfig scenario = scenarios[scenarioIndex];
            scenarioSnapshotReady = false;
            pedestrianSnapshots.Clear();


            if (!SetScenarioTask(scenario.taskName))
            {
                FailEntireScenario(scenario, "set_task_failed");
                continue;
            }


            yield return null;
            yield return new WaitForSeconds(taskInitializationWaitSeconds);


            if (sean.robotTask == null)
            {
                FailEntireScenario(scenario, "robot_task_null");
                continue;
            }


            Parameters.ResetToDefault();
            UnityEngine.Random.InitState(scenario.scenarioSeed);


            bool scenarioTaskStarted = false;
            yield return StartTaskWithRetries(
                scenario,
                result => scenarioTaskStarted = result
            );


            if (!scenarioTaskStarted)
            {
                FailEntireScenario(scenario, "scenario_initialization_failed");
                continue;
            }


            yield return null;
            yield return new WaitForSeconds(pedestrianInitializationWaitSeconds);


            if (!CaptureScenarioSnapshot())
            {
                FailEntireScenario(scenario, "scenario_snapshot_failed");
                continue;
            }


            featureLogger.useGoalAheadOfRobot = false;
            featureLogger.goalTransform = sean.robotTask.robotGoal.transform;


            SEAN.Tasks.Base scenarioTask = sean.robotTask;
            scenarioTask.enabled = false;


            for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
            {
                MetaActionController.MetaAction action = actions[actionIndex];


                for (int rolloutIndex = 0;
                     rolloutIndex < rolloutsPerScenarioAction;
                     rolloutIndex++)
                {
                    int rolloutSeed =
                        scenario.scenarioSeed + actionIndex * 10 + rolloutIndex + 1;


                    string runLabel =
                        runId + "_" +
                        scenario.scenarioId + "_" +
                        action + "_rollout_" +
                        rolloutIndex;


                    metaActionController.StopRobot();
                    featureLogger.EndRollout();
                    Parameters.ResetToDefault();


                    if (!RestoreScenarioSnapshot())
                    {
                        failedRollouts++;
                        AppendManifestRow(
                            scenario,
                            action,
                            rolloutIndex,
                            rolloutSeed,
                            "failed",
                            "scenario_restore_failed",
                            ""
                        );
                        LogProgress();
                        continue;
                    }


                    UnityEngine.Random.InitState(rolloutSeed);


                    /*
                     * Republish the fixed SEAN goal for every rollout so ROS
                     * starts a fresh navigation attempt after restoration.
                     */
                    sean.robotTask.RepublishCurrentGoal();


                    yield return new WaitForSeconds(
                        goalRepublishWaitSeconds
                    );


                    /*
                     * ROS may move the robot slightly while receiving the goal.
                     * Restore the exact snapshot immediately before logging.
                     */
                    if (!RestoreScenarioSnapshot())
                    {
                        failedRollouts++;


                        AppendManifestRow(
                            scenario,
                            action,
                            rolloutIndex,
                            rolloutSeed,
                            "failed",
                            "final_scenario_restore_failed",
                            ""
                        );


                        LogProgress();
                        continue;
                    }


                    if (recordVideos)


                    {


                        if (videoRecorder == null)


                        {


                            failedRollouts++;



                            AppendManifestRow(


                                scenario,


                                action,


                                rolloutIndex,


                                rolloutSeed,


                                "failed",


                                "video_recorder_not_assigned",


                                ""


                            );



                            LogProgress();


                            continue;


                        }



                        bool videoStarted =


                            videoRecorder.StartClip(


                                scenario.scenarioId,


                                action.ToString(),


                                rolloutIndex


                            );



                        if (!videoStarted)


                        {


                            failedRollouts++;



                            AppendManifestRow(


                                scenario,


                                action,


                                rolloutIndex,


                                rolloutSeed,


                                "failed",


                                "video_recording_start_failed",


                                ""


                            );



                            LogProgress();


                            continue;


                        }



                        yield return null;


                    }



                    featureLogger.BeginRollout(


                        scenario.scenarioId,


                        action.ToString(),


                        rolloutIndex,


                        rolloutSeed,


                        runLabel


                    );


                    string outputPath =
                        featureLogger.GetCurrentOutputPath();


                    metaActionController.SetAction(action);


                    yield return new WaitForSeconds(rolloutDurationSeconds);


                    metaActionController.StopRobot();



                    if (


                        recordVideos &&


                        videoRecorder != null


                    )


                    {


                        videoRecorder.StopClip();



                        /*


                         * Give Recorder one frame to finish writing


                         * the MP4 before starting the next rollout.


                         */


                        yield return null;


                    }



                    featureLogger.EndRollout();
completedRollouts++;
                    AppendManifestRow(
                        scenario,
                        action,
                        rolloutIndex,
                        rolloutSeed,
                        "completed",
                        "",
                        outputPath
                    );


                    LogProgress();
                    yield return new WaitForSeconds(delayBetweenRolloutsSeconds);
                }
            }


            if (scenarioTask != null) scenarioTask.enabled = true;


            scenarioSnapshotReady = false;
            pedestrianSnapshots.Clear();
        }


        metaActionController.StopRobot();
        featureLogger.EndRollout();
        Parameters.ResetToDefault();


        if (sean != null && sean.robotTask != null)
        {
            sean.robotTask.enabled = true;
        }


        isRunning = false;
    }


    private bool CaptureScenarioSnapshot()
    {
        if (
            sean == null ||
            sean.robotTask == null ||
            sean.robotTask.robotStart == null ||
            sean.robotTask.robotGoal == null ||
            sean.robot == null ||
            sean.robot.base_link == null
        )
        {
            scenarioSnapshotReady = false;
            return false;
        }


        Transform robotStart = sean.robotTask.robotStart.transform;
        Transform robotGoal = sean.robotTask.robotGoal.transform;


        fixedRobotStartPosition = robotStart.position;
        fixedRobotStartRotation = robotStart.rotation;
        fixedRobotGoalPosition = robotGoal.position;
        fixedRobotGoalRotation = robotGoal.rotation;
        fixedRobotStartActive = sean.robotTask.robotStart.activeSelf;
        fixedRobotGoalActive = sean.robotTask.robotGoal.activeSelf;


        pedestrianSnapshots.Clear();


        if (
            sean.pedestrianBehavior != null &&
            sean.pedestrianBehavior.agents != null
        )
        {
            foreach (
                SEAN.Scenario.Trajectory.TrackedAgent agent
                in sean.pedestrianBehavior.agents
            )
            {
                if (agent == null) continue;


                pedestrianSnapshots.Add(
                    new TransformSnapshot
                    {
                        target = agent.transform,
                        position = agent.transform.position,
                        rotation = agent.transform.rotation,
                        activeSelf = agent.gameObject.activeSelf,
                        rigidbody = agent.GetComponent<Rigidbody>()
                    }
                );
            }
        }


        scenarioSnapshotReady = true;
        return true;
    }


    private bool RestoreScenarioSnapshot()
    {
        if (
            !scenarioSnapshotReady ||
            sean == null ||
            sean.robotTask == null ||
            sean.robotTask.robotStart == null ||
            sean.robotTask.robotGoal == null ||
            sean.robot == null ||
            sean.robot.base_link == null
        )
        {
            return false;
        }


        sean.robotTask.robotStart.SetActive(fixedRobotStartActive);
        sean.robotTask.robotGoal.SetActive(fixedRobotGoalActive);


        Transform robotStart = sean.robotTask.robotStart.transform;
        robotStart.position = fixedRobotStartPosition;
        robotStart.rotation = fixedRobotStartRotation;


        Transform robotGoal = sean.robotTask.robotGoal.transform;
        robotGoal.position = fixedRobotGoalPosition;
        robotGoal.rotation = fixedRobotGoalRotation;


        Transform robotBase = sean.robot.base_link.transform;
        robotBase.position = fixedRobotStartPosition;
        robotBase.rotation = fixedRobotStartRotation;


        Rigidbody robotRigidbody = robotBase.GetComponent<Rigidbody>();
        if (robotRigidbody != null)
        {
            robotRigidbody.velocity = Vector3.zero;
            robotRigidbody.angularVelocity = Vector3.zero;
        }


        foreach (TransformSnapshot snapshot in pedestrianSnapshots)
        {
            if (snapshot.target == null) continue;


            snapshot.target.gameObject.SetActive(snapshot.activeSelf);
            snapshot.target.position = snapshot.position;
            snapshot.target.rotation = snapshot.rotation;


            if (snapshot.rigidbody != null)
            {
                snapshot.rigidbody.velocity = Vector3.zero;
                snapshot.rigidbody.angularVelocity = Vector3.zero;
            }
        }


        Physics.SyncTransforms();
        return true;
    }


    private void FailEntireScenario(
        ScenarioConfig scenario,
        string reason
    )
    {
        int failedForScenario =
            actions.Count * rolloutsPerScenarioAction;


        failedRollouts += failedForScenario;
        AppendScenarioFailureToManifest(scenario, reason);
        LogProgress();
    }


    private bool SetScenarioTask(string taskName)
    {
        try
        {
            if (sean.robotTask != null)
            {
                sean.robotTask.enabled = true;
            }


            sean.SetTask(taskName);


            if (sean.robotTask != null)
            {
                sean.robotTask.enabled = true;
            }


            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[MetaActionDatasetRunner] Could not set task " +
                taskName +
                ":\n" +
                exception
            );
            return false;
        }
    }


    private IEnumerator StartTaskWithRetries(
        ScenarioConfig scenario,
        Action<bool> resultCallback
    )
    {
        if (sean == null || sean.robotTask == null)
        {
            resultCallback(false);
            yield break;
        }


        sean.robotTask.enabled = true;


        for (
            int attempt = 1;
            attempt <= maximumTaskStartAttempts;
            attempt++
        )
        {
            UnityEngine.Random.InitState(scenario.scenarioSeed);


            ushort taskNumberBefore = sean.robotTask.number;


            try
            {
                sean.robotTask.StartNewTask();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[MetaActionDatasetRunner] Exception starting " +
                    scenario.taskName +
                    " | scenario=" +
                    scenario.scenarioId +
                    " | attempt=" +
                    attempt +
                    ":\n" +
                    exception
                );
            }


            yield return null;


            if (sean.robotTask.number > taskNumberBefore)
            {
                resultCallback(true);
                yield break;
            }


            yield return new WaitForSeconds(taskRetryWaitSeconds);
        }


        resultCallback(false);
    }


    private void CreateManifest()
    {
        string folder = Path.Combine(
            Application.dataPath,
            "../Output/MetaActionDataset"
        );


        Directory.CreateDirectory(folder);


        string timestamp =
            DateTime.Now.ToString("yyyyMMdd_HHmmss");


        manifestPath = Path.Combine(
            folder,
            runId + "_manifest_" + timestamp + ".csv"
        );


        using (
            StreamWriter writer =
                new StreamWriter(
                    manifestPath,
                    false,
                    Encoding.UTF8
                )
        )
        {
            writer.WriteLine(
                "run_id," +
                "scenario_id," +
                "scenario_family," +
                "task_name," +
                "scenario_seed," +
                "meta_action," +
                "rollout_id," +
                "rollout_seed," +
                "status," +
                "failure_reason," +
                "trajectory_file"
            );
        }
    }


    private void AppendManifestRow(
        ScenarioConfig scenario,
        MetaActionController.MetaAction action,
        int rolloutIndex,
        int rolloutSeed,
        string status,
        string failureReason,
        string trajectoryFile
    )
    {
        using (
            StreamWriter writer =
                new StreamWriter(
                    manifestPath,
                    true,
                    Encoding.UTF8
                )
        )
        {
            writer.WriteLine(
                Escape(runId) + "," +
                Escape(scenario.scenarioId) + "," +
                Escape(scenario.scenarioFamily) + "," +
                Escape(scenario.taskName) + "," +
                scenario.scenarioSeed + "," +
                Escape(action.ToString()) + "," +
                rolloutIndex + "," +
                rolloutSeed + "," +
                Escape(status) + "," +
                Escape(failureReason) + "," +
                Escape(trajectoryFile)
            );
        }
    }


    private void AppendScenarioFailureToManifest(
        ScenarioConfig scenario,
        string reason
    )
    {
        for (
            int actionIndex = 0;
            actionIndex < actions.Count;
            actionIndex++
        )
        {
            for (
                int rolloutIndex = 0;
                rolloutIndex < rolloutsPerScenarioAction;
                rolloutIndex++
            )
            {
                int rolloutSeed =
                    scenario.scenarioSeed +
                    actionIndex * 10 +
                    rolloutIndex +
                    1;


                AppendManifestRow(
                    scenario,
                    actions[actionIndex],
                    rolloutIndex,
                    rolloutSeed,
                    "failed",
                    reason,
                    ""
                );
            }
        }
    }


    private void LogProgress()
    {
        int processed = completedRollouts + failedRollouts;


        float percent =
            totalPlannedRollouts > 0
                ? 100.0f * processed / totalPlannedRollouts
                : 0.0f;


        Debug.Log(
            "[MetaActionDatasetRunner] Progress: " +
            processed +
            "/" +
            totalPlannedRollouts +
            " (" +
            percent.ToString("F1") +
            "%) | completed=" +
            completedRollouts +
            " | failed=" +
            failedRollouts
        );
    }


    private bool ValidateReferences()
    {
        if (sean == null)
        {
            Debug.LogError("[MetaActionDatasetRunner] SEAN instance is null.");
            return false;
        }


        if (metaActionController == null)
        {
            Debug.LogError(
                "[MetaActionDatasetRunner] MetaActionController is not assigned."
            );
            return false;
        }


        if (featureLogger == null)
        {
            Debug.LogError(
                "[MetaActionDatasetRunner] MetaActionFeatureLogger is not assigned."
            );
            return false;
        }


        return true;
    }


    private void PrintAvailableTasks()
    {
        try
        {
            List<SEAN.Tasks.Base> availableTasks = sean.robotTasks;
            string taskNames = "";


            for (int i = 0; i < availableTasks.Count; i++)
            {
                if (i > 0) taskNames += ", ";
                taskNames += availableTasks[i].name;
            }


            Debug.Log(
                "[MetaActionDatasetRunner] Available tasks: " +
                taskNames
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[MetaActionDatasetRunner] Could not enumerate tasks:\n" +
                exception
            );
        }
    }


    private string Escape(string value)
    {
        if (value == null) return "";


        return "\"" +
               value.Replace("\"", "\"\"") +
               "\"";
    }


    private void OnDestroy()
    {
        if (sean != null && sean.robotTask != null)
        {
            sean.robotTask.enabled = true;
        }


        if (ownsRunnerLock && activeRunner == this)
        {
            activeRunner = null;
        }
    }
}






