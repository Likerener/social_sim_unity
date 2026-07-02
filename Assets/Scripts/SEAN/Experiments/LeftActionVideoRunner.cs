using System;
using System.Collections;
using UnityEngine;

public class LeftActionVideoRunner : MonoBehaviour
{
    [Header("Control")]
    public bool runOnStart = true;
    public bool isRunning = false;

    [Header("Scenario")]
    public string scenarioId = "video_busy_ab_nav_left";
    public string taskName = "BusyABNav";
    public int scenarioSeed = 10000;

    [Header("Timing")]
    public float taskInitializationWaitSeconds = 2.0f;
    public float pedestrianInitializationWaitSeconds = 2.0f;
    public float videoDurationSeconds = 15.0f;

    [Header("References")]
    public MetaActionController metaActionController;
    public MetaActionFeatureLogger featureLogger;

    private SEAN.SEAN sean;

    private void Start()
    {
        if (runOnStart)
        {
            StartVideoRun();
        }
    }

    public void StartVideoRun()
    {
        if (isRunning)
        {
            return;
        }

        StartCoroutine(RunLeftActionVideo());
    }

    private IEnumerator RunLeftActionVideo()
    {
        isRunning = true;

        sean = SEAN.SEAN.instance;

        if (sean == null)
        {
            Debug.LogError(
                "[LeftActionVideoRunner] SEAN instance is null."
            );

            isRunning = false;
            yield break;
        }

        if (metaActionController == null)
        {
            Debug.LogError(
                "[LeftActionVideoRunner] MetaActionController is not assigned."
            );

            isRunning = false;
            yield break;
        }

        if (featureLogger == null)
        {
            Debug.LogError(
                "[LeftActionVideoRunner] MetaActionFeatureLogger is not assigned."
            );

            isRunning = false;
            yield break;
        }

        featureLogger.autoStartLogging = false;
        featureLogger.enableLogging = false;

        metaActionController.StopRobot();

        UnityEngine.Random.InitState(scenarioSeed);
        Parameters.ResetToDefault();

        try
        {
            sean.SetTask(taskName);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[LeftActionVideoRunner] Could not select task " +
                taskName +
                ":\n" +
                exception
            );

            isRunning = false;
            yield break;
        }

        yield return null;

        yield return new WaitForSeconds(
            taskInitializationWaitSeconds
        );

        if (sean.robotTask == null)
        {
            Debug.LogError(
                "[LeftActionVideoRunner] robotTask is null."
            );

            isRunning = false;
            yield break;
        }

        UnityEngine.Random.InitState(scenarioSeed);

        ushort taskNumberBefore =
            sean.robotTask.number;

        try
        {
            sean.robotTask.StartNewTask();
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[LeftActionVideoRunner] Could not start task:\n" +
                exception
            );

            isRunning = false;
            yield break;
        }

        yield return null;

        if (
            sean.robotTask.number <= taskNumberBefore &&
            !sean.robotTask.isRunning
        )
        {
            Debug.LogError(
                "[LeftActionVideoRunner] Task did not start."
            );

            isRunning = false;
            yield break;
        }

        yield return new WaitForSeconds(
            pedestrianInitializationWaitSeconds
        );

        featureLogger.BeginRollout(
            scenarioId,
            MetaActionController.MetaAction.Left.ToString(),
            0,
            scenarioSeed,
            scenarioId
        );

        metaActionController.SetAction(
            MetaActionController.MetaAction.Left
        );

        Debug.Log(
            "[LeftActionVideoRunner] LEFT action started. Begin recording now."
        );

        yield return new WaitForSeconds(
            videoDurationSeconds
        );

        metaActionController.StopRobot();
        featureLogger.EndRollout();

        Parameters.ResetToDefault();

        isRunning = false;

        Debug.Log(
            "[LeftActionVideoRunner] LEFT action video run complete."
        );
    }
}

