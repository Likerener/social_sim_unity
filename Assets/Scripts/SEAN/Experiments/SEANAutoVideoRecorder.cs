
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
#endif

public class SEANAutoVideoRecorder : MonoBehaviour
{
    [Header("Output")]
    public string outputFolderName = "SEAN_Clean_Videos";

    [Header("Video")]
    public int outputWidth = 1280;
    public int outputHeight = 720;
    public float frameRate = 30.0f;

    [Header("Remove Upper-Left Camera")]
    public bool removeUpperLeftCamera = true;

    /*
     * Runtime state.
     */
    private bool recordingActive = false;

    /*
     * Remember the original state of every camera object we disable,
     * so it can be restored after the rollout finishes.
     */
    private class CameraState
    {
        public Camera camera;
        public GameObject gameObject;
        public bool originalGameObjectActive;
        public bool originalCameraEnabled;
    }

    private readonly List<CameraState> disabledCameraStates =
        new List<CameraState>();

#if UNITY_EDITOR
    private RecorderController recorderController;

    private RecorderControllerSettings
        controllerSettings;

    private MovieRecorderSettings
        movieSettings;
#endif


    /*
     * We check every frame because SEAN can create/re-enable
     * robot sensor cameras after StartClip() has already begun.
     */
    private void LateUpdate()
    {
        if (
            recordingActive &&
            removeUpperLeftCamera
        )
        {
            ForceDisableUpperLeftCamera();
        }
    }


    public bool StartClip(
        string scenarioId,
        string actionName,
        int rolloutId
    )
    {
#if UNITY_EDITOR
        /*
         * Make sure a previous clip is completely closed.
         */
        StopClip();

        recordingActive = true;

        /*
         * Disable the unwanted upper-left camera before
         * Recorder begins capturing Game View.
         */
        if (removeUpperLeftCamera)
        {
            Debug.Log(
                "[SEANAutoVideoRecorder] " +
                "Searching for upper-left cameras before recording."
            );

            DebugAllCameras();

            ForceDisableUpperLeftCamera();
        }

        string outputFolder =
            Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "..",
                    "Recordings",
                    outputFolderName
                )
            );

        Directory.CreateDirectory(
            outputFolder
        );

        string fileName =
            Sanitize(scenarioId) +
            "_" +
            Sanitize(actionName) +
            "_rollout_" +
            rolloutId;

        controllerSettings =
            ScriptableObject.CreateInstance<
                RecorderControllerSettings
            >();

        movieSettings =
            ScriptableObject.CreateInstance<
                MovieRecorderSettings
            >();

        movieSettings.name =
            "SEAN Automatic Movie Recorder";

        movieSettings.Enabled = true;

        movieSettings.OutputFormat =
            MovieRecorderSettings
                .VideoRecorderOutputFormat
                .MP4;

        /*
         * Record the actual Unity Game View.
         */
        movieSettings.ImageInputSettings =
            new GameViewInputSettings
            {
                OutputWidth =
                    outputWidth,

                OutputHeight =
                    outputHeight
            };

        /*
         * Disable audio.
         *
         * This avoids the FMOD / zero sample rate problem
         * previously encountered on these machines.
         */
        movieSettings
            .AudioInputSettings
            .PreserveAudio = false;

        movieSettings.OutputFile =
            Path.Combine(
                outputFolder,
                fileName
            );

        controllerSettings
            .AddRecorderSettings(
                movieSettings
            );

        controllerSettings
            .SetRecordModeToManual();

        controllerSettings.FrameRate =
            frameRate;

        controllerSettings.CapFrameRate =
            true;

        recorderController =
            new RecorderController(
                controllerSettings
            );

        RecorderOptions.VerboseMode =
            false;

        /*
         * Prepare and start this rollout's MP4.
         */
        recorderController
            .PrepareRecording();

        bool started =
            recorderController
                .StartRecording();

        if (!started)
        {
            Debug.LogError(
                "[SEANAutoVideoRecorder] " +
                "Failed to start recording: " +
                movieSettings.OutputFile
            );

            recordingActive =
                false;

            CleanupRecorder();

            RestoreDisabledCameras();

            return false;
        }

        Debug.Log(
            "[SEANAutoVideoRecorder] " +
            "Recording started: " +
            movieSettings.OutputFile +
            ".mp4"
        );

        return true;

#else

        Debug.LogError(
            "[SEANAutoVideoRecorder] " +
            "Unity Recorder is available only inside the Unity Editor."
        );

        return false;

#endif
    }


    public void StopClip()
    {
#if UNITY_EDITOR

        if (
            recorderController != null &&
            recorderController.IsRecording()
        )
        {
            recorderController
                .StopRecording();

            Debug.Log(
                "[SEANAutoVideoRecorder] " +
                "Recording stopped."
            );
        }

        CleanupRecorder();

#endif

        recordingActive =
            false;

        RestoreDisabledCameras();
    }


    /*
     * Finds the actual runtime camera responsible for the
     * upper-left inset and disables its entire GameObject.
     *
     * Known SEAN upper-left camera viewport:
     *
     * x      = 0.03
     * y      = 0.72
     * width  = 0.25
     * height = 0.25
     *
     * The right-side ThirdPersonCamera is:
     *
     * x = 0.715
     *
     * and therefore will NOT match.
     */
    private void ForceDisableUpperLeftCamera()
    {
        Camera[] cameras =
            FindObjectsOfType<Camera>(
                true
            );

        foreach (
            Camera cam
            in cameras
        )
        {
            if (cam == null)
            {
                continue;
            }

            /*
             * Never touch this recorder's own GameObject
             * in case a Camera gets attached later.
             */
            if (
                cam.gameObject ==
                gameObject
            )
            {
                continue;
            }

            Rect rect =
                cam.rect;

            bool matchesUpperLeftViewport =
                Mathf.Abs(
                    rect.x - 0.03f
                ) < 0.04f &&

                Mathf.Abs(
                    rect.y - 0.72f
                ) < 0.04f &&

                Mathf.Abs(
                    rect.width - 0.25f
                ) < 0.04f &&

                Mathf.Abs(
                    rect.height - 0.25f
                ) < 0.04f;

            bool namedOverheadCamera =
                cam.gameObject.name
                    .Equals(
                        "OverheadCamera",
                        System.StringComparison
                            .OrdinalIgnoreCase
                    );

            bool shouldDisable =
                namedOverheadCamera ||
                matchesUpperLeftViewport;

            if (!shouldDisable)
            {
                continue;
            }

            /*
             * If we have not seen this runtime camera before,
             * save its original state.
             */
            CameraState existingState =
                FindSavedState(
                    cam
                );

            if (
                existingState == null
            )
            {
                CameraState state =
                    new CameraState
                    {
                        camera =
                            cam,

                        gameObject =
                            cam.gameObject,

                        originalGameObjectActive =
                            cam.gameObject
                                .activeSelf,

                        originalCameraEnabled =
                            cam.enabled
                    };

                disabledCameraStates
                    .Add(
                        state
                    );

                Debug.Log(
                    "[SEANAutoVideoRecorder] " +
                    "FOUND upper-left camera: " +
                    GetCameraPath(cam) +
                    " | enabled=" +
                    cam.enabled +
                    " | active=" +
                    cam.gameObject
                        .activeInHierarchy +
                    " | rect=" +
                    FormatRect(rect) +
                    " | targetTexture=" +
                    (
                        cam.targetTexture == null
                            ? "None"
                            : cam.targetTexture.name
                    )
                );
            }

            /*
             * Disable the Camera component first.
             */
            if (cam.enabled)
            {
                cam.enabled =
                    false;
            }

            /*
             * More importantly, disable the entire camera GameObject.
             *
             * This also disables publisher/offscreen components
             * attached to the same OverheadCamera object.
             */
            if (
                cam.gameObject
                    .activeSelf
            )
            {
                Debug.Log(
                    "[SEANAutoVideoRecorder] " +
                    "DISABLING camera GameObject: " +
                    GetCameraPath(cam)
                );

                cam.gameObject
                    .SetActive(
                        false
                    );
            }
        }
    }


    /*
     * Returns an existing saved state for the supplied camera,
     * or null if this is a newly discovered runtime camera.
     */
    private CameraState FindSavedState(
        Camera cam
    )
    {
        foreach (
            CameraState state
            in disabledCameraStates
        )
        {
            if (
                state != null &&
                state.camera == cam
            )
            {
                return state;
            }
        }

        return null;
    }


    /*
     * Restore only the cameras that we changed.
     *
     * Cameras that were already inactive before recording
     * remain inactive.
     */
    private void RestoreDisabledCameras()
    {
        foreach (
            CameraState state
            in disabledCameraStates
        )
        {
            if (
                state == null ||
                state.gameObject == null
            )
            {
                continue;
            }

            state.gameObject
                .SetActive(
                    state
                        .originalGameObjectActive
                );

            if (
                state.camera != null
            )
            {
                state.camera.enabled =
                    state
                        .originalCameraEnabled;
            }

            Debug.Log(
                "[SEANAutoVideoRecorder] " +
                "RESTORED camera: " +
                (
                    state.camera == null
                        ? state.gameObject.name
                        : GetCameraPath(
                            state.camera
                        )
                )
            );
        }

        disabledCameraStates
            .Clear();
    }


    /*
     * Print all runtime cameras immediately before recording.
     *
     * This is intentionally kept in this version so if the
     * upper-left view somehow still exists, the Unity Console
     * will show exactly which cameras were present.
     */
    private void DebugAllCameras()
    {
        Camera[] cameras =
            FindObjectsOfType<Camera>(
                true
            );

        Debug.Log(
            "[SEANAutoVideoRecorder] " +
            "Runtime camera count: " +
            cameras.Length
        );

        foreach (
            Camera cam
            in cameras
        )
        {
            if (cam == null)
            {
                continue;
            }

            Debug.Log(
                "[CAMERA DEBUG] " +
                "path=" +
                GetCameraPath(cam) +
                " | enabled=" +
                cam.enabled +
                " | activeSelf=" +
                cam.gameObject.activeSelf +
                " | activeInHierarchy=" +
                cam.gameObject
                    .activeInHierarchy +
                " | rect=" +
                FormatRect(
                    cam.rect
                ) +
                " | depth=" +
                cam.depth +
                " | targetTexture=" +
                (
                    cam.targetTexture == null
                        ? "None"
                        : cam.targetTexture.name
                )
            );
        }
    }


    /*
     * Helpful full hierarchy path so duplicate camera names
     * can be distinguished in Console logs.
     */
    private string GetCameraPath(
        Camera cam
    )
    {
        if (cam == null)
        {
            return "null";
        }

        Transform current =
            cam.transform;

        string path =
            current.name;

        while (
            current.parent != null
        )
        {
            current =
                current.parent;

            path =
                current.name +
                "/" +
                path;
        }

        return path;
    }


    private string FormatRect(
        Rect rect
    )
    {
        return
            "x=" +
            rect.x.ToString("F3") +
            ", y=" +
            rect.y.ToString("F3") +
            ", w=" +
            rect.width.ToString("F3") +
            ", h=" +
            rect.height.ToString("F3");
    }


#if UNITY_EDITOR

    private void CleanupRecorder()
    {
        recorderController =
            null;

        if (
            movieSettings != null
        )
        {
            DestroyImmediate(
                movieSettings
            );

            movieSettings =
                null;
        }

        if (
            controllerSettings != null
        )
        {
            DestroyImmediate(
                controllerSettings
            );

            controllerSettings =
                null;
        }
    }

#endif


    private string Sanitize(
        string value
    )
    {
        if (
            string.IsNullOrEmpty(
                value
            )
        )
        {
            return "unknown";
        }

        foreach (
            char invalid
            in Path
                .GetInvalidFileNameChars()
        )
        {
            value =
                value.Replace(
                    invalid,
                    '_'
                );
        }

        return value.Replace(
            ' ',
            '_'
        );
    }


    private void OnDisable()
    {
        StopClip();
    }


    private void OnDestroy()
    {
        StopClip();
    }
}
