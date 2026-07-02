// Copyright (c) 2021, Members of Yale Interactive Machines Group, Yale University,
// Nathan Tsoi
// All rights reserved.
// This source code is licensed under the BSD-style license found in the
// LICENSE file in the root directory of this source tree.

using System.Collections.Generic;
using UnityEngine;
using SEAN.Display.VolumetricLine;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;

namespace SEAN.Display
{
    public class PlanVisualizer : MonoBehaviour
    {
        private SEAN sean;

        public string Topic;
        public Color LineColor;
        public float waitSec = 0.25f;
        public float pThresh = 0.5f;

        private ulong stamp;
        private ulong prevStamp;

        private RosMessageTypes.Nav.MPath message;
        private bool started = false;

        [Header("Path Rendering")]
        public Material LightSaberMaterial;

        public int SampledPath = 25;

        private List<Vector3> pathPositions;
        private Vector3[] renderPathPositions;

        private VolumetricLineStripBehavior lineStripBehavior;

        private void Awake()
        {
            ROSConnection.instance.Subscribe<RosMessageTypes.Nav.MPath>(
                Topic,
                ReceiveMessage
            );
        }

        private void Start()
        {
            pathPositions = new List<Vector3>();
            sean = SEAN.instance;

            /*
             * Do not create VolumetricLineStripBehavior here.
             *
             * The component requires at least three vertices.
             * Creating it before a valid ROS path exists causes:
             *
             * "Add at least 3 vertices to the VolumetricLineStrip"
             *
             * It will instead be created after a valid path arrives.
             */
            lineStripBehavior =
                gameObject.GetComponent<VolumetricLineStripBehavior>();

            if (lineStripBehavior != null)
            {
                lineStripBehavior.enabled = false;
            }

            started = true;
            ProcessMessage();
        }

        private void ReceiveMessage(
            RosMessageTypes.Nav.MPath newMessage
        )
        {
            message = newMessage;
            ProcessMessage();
        }

        private void EnsureLineStripExists(Vector3[] initialVertices)
        {
            if (
                initialVertices == null ||
                initialVertices.Length < 3
            )
            {
                return;
            }

            if (lineStripBehavior == null)
            {
                lineStripBehavior =
                    gameObject.AddComponent<
                        VolumetricLineStripBehavior
                    >();

                lineStripBehavior.TemplateMaterial =
                    LightSaberMaterial;

                lineStripBehavior.LightSaberFactor = 1f;
                lineStripBehavior.LineWidth = 0.2f;
                lineStripBehavior.LineColor = LineColor;
            }

            lineStripBehavior.UpdateLineVertices(
                initialVertices
            );

            lineStripBehavior.enabled = true;
        }

        private void EnableLineStrip(bool enable)
        {
            if (lineStripBehavior != null)
            {
                lineStripBehavior.enabled = enable;
            }
        }

        private void ProcessMessage()
        {
            if (!started || message == null)
            {
                return;
            }

            if (
                message.header == null ||
                message.header.stamp == null
            )
            {
                return;
            }

            stamp = message.header.stamp.secs;

            if (
                prevStamp != 0 &&
                stamp >= prevStamp &&
                stamp - prevStamp < waitSec
            )
            {
                return;
            }

            prevStamp = stamp;

            pathPositions.Clear();

            if (
                message.poses == null ||
                message.poses.Length < 3
            )
            {
                EnableLineStrip(false);
                return;
            }

            if (
                sean == null ||
                sean.robot == null
            )
            {
                sean = SEAN.instance;

                if (
                    sean == null ||
                    sean.robot == null
                )
                {
                    EnableLineStrip(false);
                    return;
                }
            }

            Vector3 lastAcceptedPoint = Vector3.zero;
            bool hasAcceptedPoint = false;

            for (
                int i = 0;
                i < message.poses.Length;
                i++
            )
            {
                if (
                    message.poses[i] == null ||
                    message.poses[i].pose == null
                )
                {
                    continue;
                }

                Vector3 point =
                    message.poses[i]
                        .pose
                        .position
                        .From<FLU>();

                point.y = sean.robot.position.y;

                if (!hasAcceptedPoint)
                {
                    pathPositions.Add(point);
                    lastAcceptedPoint = point;
                    hasAcceptedPoint = true;
                }
                else
                {
                    float distance =
                        Vector3.Distance(
                            lastAcceptedPoint,
                            point
                        );

                    if (distance > pThresh)
                    {
                        pathPositions.Add(point);
                        lastAcceptedPoint = point;
                    }
                }

                if (
                    pathPositions.Count >= SampledPath
                )
                {
                    break;
                }
            }

            /*
             * A volumetric line strip requires at least
             * three actual vertices.
             */
            if (pathPositions.Count < 3)
            {
                EnableLineStrip(false);
                return;
            }

            int desiredPointCount =
                Mathf.Max(3, SampledPath);

            while (
                pathPositions.Count <
                desiredPointCount
            )
            {
                pathPositions.Add(lastAcceptedPoint);
            }

            renderPathPositions =
                pathPositions.ToArray();

            EnsureLineStripExists(
                renderPathPositions
            );
        }

        private void OnDisable()
        {
            EnableLineStrip(false);
        }
    }
}

