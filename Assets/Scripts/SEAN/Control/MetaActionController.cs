using UnityEngine;

public class MetaActionController : MonoBehaviour
{
    public enum MetaAction
    {
        Stop,
        Straight,
        SlowDown,
        Left,
        Right,
        ForwardLeft,
        ForwardRight
    }

    [Header("Current Action")]
    public MetaAction currentAction = MetaAction.Straight;

    [Header("Motion Settings")]
    public float forwardSpeed = 0.3f;
    public float slowSpeed = 0.15f;
    public float turnSpeedDegrees = 45f;
    public bool moveOnPlay = true;

    private Transform robotBaseLink;

    private void Start()
    {
        TryFindRobotBaseLink();
    }

    private void FixedUpdate()
    {
        if (!moveOnPlay)
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

        ApplyMetaAction();
    }

    public void SetAction(MetaAction action)
    {
        currentAction = action;
    }

    public void StopRobot()
    {
        currentAction = MetaAction.Stop;
    }

    private void TryFindRobotBaseLink()
    {
        if (SEAN.SEAN.instance != null &&
            SEAN.SEAN.instance.robot != null &&
            SEAN.SEAN.instance.robot.base_link != null)
        {
            robotBaseLink = SEAN.SEAN.instance.robot.base_link.transform;

            Debug.Log(
                "[MetaActionController] Found robot base_link: " +
                robotBaseLink.name
            );
        }
    }

    private void ApplyMetaAction()
    {
        float forward = 0f;
        float turn = 0f;

        switch (currentAction)
        {
            case MetaAction.Stop:
                forward = 0f;
                turn = 0f;
                break;

            case MetaAction.Straight:
                forward = forwardSpeed;
                turn = 0f;
                break;

            case MetaAction.SlowDown:
                forward = slowSpeed;
                turn = 0f;
                break;

            case MetaAction.Left:
                forward = 0f;
                turn = -turnSpeedDegrees;
                break;

            case MetaAction.Right:
                forward = 0f;
                turn = turnSpeedDegrees;
                break;

            case MetaAction.ForwardLeft:
                forward = forwardSpeed;
                turn = -turnSpeedDegrees;
                break;

            case MetaAction.ForwardRight:
                forward = forwardSpeed;
                turn = turnSpeedDegrees;
                break;
        }

        if (Mathf.Abs(turn) > 0f)
        {
            robotBaseLink.Rotate(
                Vector3.up,
                turn * Time.fixedDeltaTime,
                Space.World
            );
        }

        if (Mathf.Abs(forward) > 0f)
        {
            Vector3 direction = robotBaseLink.forward;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                direction.Normalize();

                robotBaseLink.position +=
                    direction * forward * Time.fixedDeltaTime;
            }
        }
    }
}

