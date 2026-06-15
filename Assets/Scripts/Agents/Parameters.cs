
public struct Parameters
{
    public static float DESIRED_SPEED = 0.6f;
    public static float T = 0.5f;
    public static float A = 2000f / 4;
    public static float B = 0.08f * 2;
    public static float K = 1.2E5f;
    public static float KAPPA = 2.4E5f;

    public static float WALL_A = 2000f * 4;
    public static float WALL_B = 0.08f * 3;
    public static float WALL_K = 1.2E5f;
    public static float WALL_KAPPA = 2.4E5f;

    public static float TAN_A = 2000f;
    public static float TAN_B = 0.08f;

    public static float MAX_VEL = 0.6f;//0.5f / 0.02f;
    public static float NEXT_NAV_MIN_DIST = 1.0f;
    public static float CLOSE_ENOUGH_MIN_DIST = 1.0f;
    public static float BACKWARD_DAMPENING = 20;
    public static float LATERAL_DAMPENING = 5;
    public static float ROBOT_REPULSION_DAMPENING_MIN = 0.5f;
    public static float ROBOT_REPULSION_DAMPENING_MAX = 1.0f;

    public static void ResetToDefault()
    {
        DESIRED_SPEED = 0.6f;
        T = 0.5f;
        A = 2000f / 4;
        B = 0.08f * 2;
        K = 1.2E5f;
        KAPPA = 2.4E5f;

        WALL_A = 2000f * 4;
        WALL_B = 0.08f * 3;
        WALL_K = 1.2E5f;
        WALL_KAPPA = 2.4E5f;

        TAN_A = 2000f;
        TAN_B = 0.08f;

        MAX_VEL = 0.6f;
        NEXT_NAV_MIN_DIST = 1.0f;
        CLOSE_ENOUGH_MIN_DIST = 1.0f;
        BACKWARD_DAMPENING = 20;
        LATERAL_DAMPENING = 5;
        ROBOT_REPULSION_DAMPENING_MIN = 0.5f;
        ROBOT_REPULSION_DAMPENING_MAX = 1.0f;
    }
}

