using UnityEngine;

public class StraightActionController : MonoBehaviour
{
    public float straightSpeed = 0.6f;
    public bool moveOnPlay = true;

    private Transform robotBaseLink;

    void Start()
    {
        if (SEAN.SEAN.instance != null &&
            SEAN.SEAN.instance.robot != null &&
            SEAN.SEAN.instance.robot.base_link != null)
        {
            robotBaseLink = SEAN.SEAN.instance.robot.base_link.transform;
            Debug.Log("StraightActionController found robot base_link: " + robotBaseLink.name);
        }
        else
        {
            Debug.LogError("StraightActionController could not find SEAN robot base_link.");
        }
    }

    void FixedUpdate()
    {
        if (!moveOnPlay || robotBaseLink == null)
        {
            return;
        }

        Vector3 forward = robotBaseLink.forward;
        forward.y = 0f;
        forward.Normalize();

        robotBaseLink.position += forward * straightSpeed * Time.fixedDeltaTime;
    }
}
