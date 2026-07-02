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

    [Header("Runner Control")]
    public bool runOnStart = false;

    // Runtime status only. Do not check manually.
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

    [Header("Task Retry")]
    public int maximumTaskStartAttempts = 8;
    public float taskRetryWaitSeconds = 1.0f;

    [Header("Scene References")]
    public MetaActionController metaActionController;
    public MetaActionFeatureLogger featureLogger;

    private SEAN.SEAN sean;
    private bool ownsRunnerLock = false;

    private readonly List<ScenarioConfig> scenarios =
        new List<ScenarioConfig>();

    private readonly List<MetaActionController.MetaAction> actions =
        new List<MetaActionController.MetaAction>();

    private string manifestPath;
    private int completedRollouts = 0;
    private int failedRollouts = 0;
    private int totalPlannedRollouts = 0;

    private void Awake()
    {
        if (activeRunner != null && activeRunner != this)
        {
            Debug.LogWarning(
                "[MetaActionDatasetRunner] Duplicate runner disabled: " +
                gameObject.name
            );

            enabled = false;
            return;
        }

        activeRunner = this;
        ownsRunnerLock = true;

        Debug.Log(
            "[MetaActionDatasetRunner] Active runner: " +
            gameObject.name +
            " | instance ID=" +
            GetInstanceID()
        );
    }

    private void Start()
    {
        if (!ownsRunnerLock)
        {
            return;
        }

        sean = SEAN.SEAN.instance;

        BuildScenarioConfigs();
        BuildActions();

        if (featureLogger != null)
        {
            featureLogger.autoStartLogging = false;
            featureLogger.enableLogging = false;
        }

        if (runOnStart)
        {
            StartDatasetRun();
        }
    }

    public void StartDatasetRun()
    {
        if (!ownsRunnerLock)
        {
            Debug.LogWarning(
                "[MetaActionDatasetRunner] This is not the active runner."
            );
            return;
        }

        if (isRunning)
        {
            Debug.LogWarning(
                "[MetaActionDatasetRunner] Runner is already running."
            );
            return;
        }

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
            scenarios.Add(
                new ScenarioConfig
                {
                    scenarioId =
                        "scenario_" +
                        scenarioNumber.ToString("D3") +
                        "_busy_ab_nav",

                    scenarioFamily = "busy_ab_nav",
                    taskName = "BusyABNav",
                    scenarioSeed = 100000 + i * 100
                }
            );

            scenarioNumber++;
        }

        for (int i = 0; i < joinCount; i++)
        {
            scenarios.Add(
                new ScenarioConfig
                {
                    scenarioId =
                        "scenario_" +
                        scenarioNumber.ToString("D3") +
                        "_join_group",

                    scenarioFamily = "join_group",
                    taskName = "JoinGroup",
                    scenarioSeed = 200000 + i * 100
                }
            );

            scenarioNumber++;
        }

        for (int i = 0; i < leaveCount; i++)
        {
            scenarios.Add(
                new ScenarioConfig
                {
                    scenarioId =
                        "scenario_" +
                        scenarioNumber.ToString("D3") +
                        "_leave_group",

                    scenarioFamily = "leave_group",
                    taskName = "LeaveGroup",
                    scenarioSeed = 300000 + i * 100
                }
            );

            scenarioNumber++;
        }

        if (numberOfScenarios < scenarios.Count)
        {
            scenarios.RemoveRange(
                numberOfScenarios,
                scenarios.Count - numberOfScenarios
            );
        }

        Debug.Log(
            "[MetaActionDatasetRunner] Built " +
            scenarios.Count +
            " scenario configurations."
        );
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
            scenarios.Count *
            actions.Count *
            rolloutsPerScenarioAction;

        if (!ValidateReferences())
        {
            isRunning = false;
            yield break;
        }

        CreateManifest();

        Debug.Log(
            "[MetaActionDatasetRunner] Starting pilot dataset: " +
            scenarios.Count +
            " scenarios x " +
            actions.Count +
            " actions x " +
            rolloutsPerScenarioAction +
            " rollouts = " +
            totalPlannedRollouts +
            " total rollouts."
        );

        PrintAvailableTasks();

        for (
            int scenarioIndex = 0;
            scenarioIndex < scenarios.Count;
            scenarioIndex++
        )
        {
            ScenarioConfig scenario = scenarios[scenarioIndex];

            Debug.Log(
                "[MetaActionDatasetRunner] Scenario " +
                (scenarioIndex + 1) +
                "/" +
                scenarios.Count +
                ": " +
                scenario.scenarioId
            );

            bool taskSet = SetScenarioTask(
                scenario.taskName
            );

            if (!taskSet)
            {
                int failedForScenario =
                    actions.Count *
                    rolloutsPerScenarioAction;

                failedRollouts += failedForScenario;

                AppendScenarioFailureToManifest(
                    scenario,
                    "set_task_failed"
                );

                continue;
            }

            yield return null;

            yield return new WaitForSeconds(
                taskInitializationWaitSeconds
            );

            if (sean.robotTask == null)
            {
                int failedForScenario =
                    actions.Count *
                    rolloutsPerScenarioAction;

                failedRollouts += failedForScenario;

                AppendScenarioFailureToManifest(
                    scenario,
                    "robot_task_null"
                );

                continue;
            }

            yield return new WaitForSeconds(
                pedestrianInitializationWaitSeconds
            );

            for (
                int actionIndex = 0;
                actionIndex < actions.Count;
                actionIndex++
            )
            {
                MetaActionController.MetaAction action =
                    actions[actionIndex];

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

                    string runLabel =
                        runId + "_" +
                        scenario.scenarioId + "_" +
                        action + "_" +
                        "rollout_" +
                        rolloutIndex;

                    metaActionController.StopRobot();
                    featureLogger.EndRollout();

                    Parameters.ResetToDefault();

                    /*
                     * Use scenarioSeed to reproduce the same
                     * scenario start/goal configuration for all
                     * actions and rollouts within this scenario.
                     */
                    UnityEngine.Random.InitState(
                        scenario.scenarioSeed
                    );

                    bool taskStarted = false;

                    yield return StartTaskWithRetries(
                        scenario,
                        result => taskStarted = result
                    );

                    if (!taskStarted)
                    {
                        failedRollouts++;

                        AppendManifestRow(
                            scenario,
                            action,
                            rolloutIndex,
                            rolloutSeed,
                            "failed",
                            "task_start_failed",
                            ""
                        );

                        LogProgress();
                        continue;
                    }

                    yield return null;

                    yield return new WaitForSeconds(
                        pedestrianInitializationWaitSeconds
                    );

                    /*
                     * Change to rolloutSeed after the scenario
                     * start/goal has been initialized. This seed
                     * is intended to produce stochastic outcome
                     * differences during the rollout.
                     */
                    UnityEngine.Random.InitState(
                        rolloutSeed
                    );

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

                    Debug.Log(
                        "[MetaActionDatasetRunner] Running " +
                        (completedRollouts + failedRollouts + 1) +
                        "/" +
                        totalPlannedRollouts +
                        ": " +
                        runLabel +
                        " | scenario seed=" +
                        scenario.scenarioSeed +
                        " | rollout seed=" +
                        rolloutSeed
                    );

                    yield return new WaitForSeconds(
                        rolloutDurationSeconds
                    );

                    metaActionController.StopRobot();
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

                    yield return new WaitForSeconds(
                        delayBetweenRolloutsSeconds
                    );
                }
            }
        }

        metaActionController.StopRobot();
        featureLogger.EndRollout();
        Parameters.ResetToDefault();

        isRunning = false;

        Debug.Log(
            "[MetaActionDatasetRunner] Pilot complete. " +
            "Completed=" +
            completedRollouts +
            ", Failed=" +
            failedRollouts +
            ", Planned=" +
            totalPlannedRollouts +
            ", Manifest=" +
            manifestPath
        );
    }

    private bool SetScenarioTask(string taskName)
    {
        try
        {
            sean.SetTask(taskName);

            Debug.Log(
                "[MetaActionDatasetRunner] Selected task: " +
                taskName
            );

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

        for (
            int attempt = 1;
            attempt <= maximumTaskStartAttempts;
            attempt++
        )
        {
            UnityEngine.Random.InitState(
                scenario.scenarioSeed
            );

            ushort taskNumberBefore =
                sean.robotTask.number;

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

            /*
             * Base.OnNewTask increments number only when
             * NewTask() succeeds.
             */
            if (sean.robotTask.number > taskNumberBefore)
            {
                resultCallback(true);
                yield break;
            }

            Debug.LogWarning(
                "[MetaActionDatasetRunner] Task start retry: " +
                scenario.scenarioId +
                " | attempt " +
                attempt +
                "/" +
                maximumTaskStartAttempts
            );

            yield return new WaitForSeconds(
                taskRetryWaitSeconds
            );
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
            runId +
            "_manifest_" +
            timestamp +
            ".csv"
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

        Debug.Log(
            "[MetaActionDatasetRunner] Manifest: " +
            manifestPath
        );
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
        int processed =
            completedRollouts +
            failedRollouts;

        float percent =
            totalPlannedRollouts > 0
                ? 100.0f * processed /
                  totalPlannedRollouts
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
            Debug.LogError(
                "[MetaActionDatasetRunner] SEAN instance is null."
            );
            return false;
        }

        if (metaActionController == null)
        {
            Debug.LogError(
                "[MetaActionDatasetRunner] " +
                "MetaActionController is not assigned."
            );
            return false;
        }

        if (featureLogger == null)
        {
            Debug.LogError(
                "[MetaActionDatasetRunner] " +
                "MetaActionFeatureLogger is not assigned."
            );
            return false;
        }

        return true;
    }

    private void PrintAvailableTasks()
    {
        try
        {
            List<SEAN.Tasks.Base> availableTasks =
                sean.robotTasks;

            string taskNames = "";

            for (
                int i = 0;
                i < availableTasks.Count;
                i++
            )
            {
                if (i > 0)
                {
                    taskNames += ", ";
                }

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
                "[MetaActionDatasetRunner] " +
                "Could not enumerate tasks:\n" +
                exception
            );
        }
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

    private void OnDestroy()
    {
        if (ownsRunnerLock && activeRunner == this)
        {
            activeRunner = null;
        }
    }
}

