﻿using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;
using DemoContentLoader;
using DemoRenderer;
using DemoRenderer.UI;
using DemoUtilities;
using OpenTK.Input;
using Quaternion = BepuUtilities.Quaternion;

namespace Demos.Demos.Tanks
{
    public class TankDemo : Demo
    {
        TankController playerController;

        static Key Forward = Key.W;
        static Key Backward = Key.S;
        static Key Right = Key.D;
        static Key Left = Key.A;
        static Key Zoom = Key.LShift;
        static Key Brake = Key.Space;
        static Key BrakeAlternate = Key.BackSpace; //I have a weird keyboard.
        static Key ToggleTank = Key.C;
        public override void Initialize(ContentArchive content, Camera camera)
        {
            camera.Position = new Vector3(0, 5, 10);
            camera.Yaw = 0;
            camera.Pitch = 0;

            var properties = new BodyProperty<TankBodyProperties>();
            Simulation = Simulation.Create(BufferPool, new TankCallbacks() { Properties = properties }, new DemoPoseIntegratorCallbacks(new Vector3(0, -10, 0)));

            var builder = new CompoundBuilder(BufferPool, Simulation.Shapes, 2);
            builder.Add(new Box(1.85f, 0.7f, 4.73f), RigidPose.Identity, 10);
            builder.Add(new Box(1.85f, 0.6f, 2.5f), new RigidPose(new Vector3(0, 0.65f, -0.35f)), 0.5f);
            builder.BuildDynamicCompound(out var children, out var bodyInertia, out _);
            builder.Dispose();
            var bodyShape = new Compound(children);
            var bodyShapeIndex = Simulation.Shapes.Add(bodyShape);
            var wheelShape = new Cylinder(0.4f, .18f);
            wheelShape.ComputeInertia(0.25f, out var wheelInertia);
            var wheelShapeIndex = Simulation.Shapes.Add(wheelShape);

            var tankDescription = new TankDescription
            {
                Body = TankPartDescription.Create(10, new Box(4f, 1, 5), RigidPose.Identity, 0.5f, Simulation.Shapes),
                Turret = TankPartDescription.Create(1, new Box(1.5f, 0.7f, 2f), new RigidPose(new Vector3(0, 0.85f, 0.4f)), 0.5f, Simulation.Shapes),
                Barrel = TankPartDescription.Create(0.5f, new Box(0.2f, 0.2f, 3f), new RigidPose(new Vector3(0, 0.85f, 0.4f - 1f - 1.5f)), 0.5f, Simulation.Shapes),
                TurretAnchor = new Vector3(0f, 0.5f, 0.4f),
                BarrelAnchor = new Vector3(0, 0.5f + 0.35f, 0.4f - 1f),
                TurretBasis = Quaternion.Identity,
                TurretServo = new ServoSettings(1f, 0f, 40f),
                TurretSpring = new SpringSettings(10f, 1f),
                BarrelServo = new ServoSettings(1f, 0f, 40f),
                BarrelSpring = new SpringSettings(10f, 1f),
                LeftTreadOffset = new Vector3(-1.9f, 0f, 0),
                RightTreadOffset = new Vector3(1.9f, 0f, 0),
                SuspensionLength = 1f,
                SuspensionSettings = new SpringSettings(2.5f, 0.8f),
                WheelShape = wheelShapeIndex,
                WheelInertia = wheelInertia,
                WheelFriction = 2f,
                TreadSpacing = 1f,
                WheelCountPerTread = 5,
                WheelOrientation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI * -0.5f),
            };

            playerController = new TankController(Tank.Create(Simulation, properties, BufferPool, new RigidPose(new Vector3(0, 10, 0), Quaternion.Identity), tankDescription), 20, 5, 2, 1, 3.5f);


            const int planeWidth = 257;
            const float terrainScale = 3;
            const float inverseTerrainScale = 1f / terrainScale;
            var terrainPosition = new Vector2(1 - planeWidth, 1 - planeWidth) * terrainScale * 0.5f;
            var random = new Random(5);

            //Add some building-ish landmarks.
            Vector3 landmarkMin = new Vector3(planeWidth * terrainScale * -0.45f, 0, planeWidth * terrainScale * -0.45f);
            Vector3 landmarkSpan = new Vector3(planeWidth * terrainScale * 0.9f, 0, planeWidth * terrainScale * 0.9f);
            for (int j = 0; j < 125; ++j)
            {
                var buildingShape = new Box(10 + (float)random.NextDouble() * 10, 20 + (float)random.NextDouble() * 20, 10 + (float)random.NextDouble() * 10);
                var position = landmarkMin + landmarkSpan * new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
                Simulation.Statics.Add(new StaticDescription(
                    new Vector3(0, buildingShape.HalfHeight - 4f + GetHeightForPosition(position.X, position.Z, planeWidth, inverseTerrainScale, terrainPosition), 0) + position,
                    Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)random.NextDouble() * MathF.PI),
                    new CollidableDescription(Simulation.Shapes.Add(buildingShape), 0.1f)));
            }

            for (int i = 0; i < 100; ++i)
            {
                Simulation.Statics.Add(new StaticDescription(new Vector3(0, 0, i * 1), Quaternion.CreateFromAxisAngle(new Vector3(0, 0, 1), MathF.PI * 0.5f),
                    new CollidableDescription(Simulation.Shapes.Add(new Capsule((float)random.NextDouble() * 0.4f + 0.2f, 40)), 0.1f)));
            }

            DemoMeshHelper.CreateDeformedPlane(planeWidth, planeWidth,
                (int vX, int vY) =>
                    {
                        var position2D = new Vector2(vX, vY) * terrainScale + terrainPosition;
                        return new Vector3(position2D.X, GetHeightForPosition(position2D.X, position2D.Y, planeWidth, inverseTerrainScale, terrainPosition), position2D.Y);
                    }, new Vector3(1, 1, 1), BufferPool, out var planeMesh);
            Simulation.Statics.Add(new StaticDescription(new Vector3(0, 0, 0),
                new CollidableDescription(Simulation.Shapes.Add(planeMesh), 0.1f)));


        }

        float GetHeightForPosition(float x, float y, int planeWidth, float inverseTerrainScale, in Vector2 terrainPosition)
        {
            var normalizedX = (x - terrainPosition.X) * inverseTerrainScale;
            var normalizedY = (y - terrainPosition.Y) * inverseTerrainScale;
            var octave0 = (MathF.Sin((normalizedX + 5f) * 0.05f) + MathF.Sin((normalizedY * inverseTerrainScale + 11) * 0.05f)) * 3.8f;
            var octave1 = (MathF.Sin((normalizedX + 17) * 0.15f) + MathF.Sin((normalizedY * inverseTerrainScale + 19) * 0.15f)) * 1.9f;
            var octave2 = (MathF.Sin((normalizedX + 37) * 0.35f) + MathF.Sin((normalizedY * inverseTerrainScale + 93) * 0.35f)) * 0.7f;
            var octave3 = (MathF.Sin((normalizedX + 53) * 0.65f) + MathF.Sin((normalizedY * inverseTerrainScale + 47) * 0.65f)) * 0.5f;
            var octave4 = (MathF.Sin((normalizedX + 67) * 1.50f) + MathF.Sin((normalizedY * inverseTerrainScale + 13) * 1.5f)) * 0.325f;
            var distanceToEdge = planeWidth / 2 - Math.Max(Math.Abs(normalizedX - planeWidth / 2), Math.Abs(normalizedY - planeWidth / 2));
            //Flatten an area in the middle.
            var offsetX = planeWidth * 0.5f - normalizedX;
            var offsetY = planeWidth * 0.5f - normalizedY;
            var distanceToCenterSquared = offsetX * offsetX + offsetY * offsetY;
            const float centerCircleSize = 30f;
            const float fadeoutBoundary = 50f;
            var outsideWeight = MathF.Min(1f, MathF.Max(0, distanceToCenterSquared - centerCircleSize * centerCircleSize) / (fadeoutBoundary * fadeoutBoundary - centerCircleSize * centerCircleSize));
            var edgeRamp = 25f / (distanceToEdge + 1);
            return outsideWeight * (octave0 + octave1 + octave2 + octave3 + octave4 + edgeRamp);
        }

        bool playerControlActive = true;
        public override void Update(Window window, Camera camera, Input input, float dt)
        {
            if (input.WasPushed(ToggleTank))
                playerControlActive = !playerControlActive;
            if (playerControlActive)
            {
                float leftTargetSpeedFraction = 0;
                float rightTargetSpeedFraction = 0;
                var left = input.IsDown(Left);
                var right = input.IsDown(Right);
                var forward = input.IsDown(Forward);
                var backward = input.IsDown(Backward);
                if (forward)
                {
                    if ((left && right) || (!left && !right))
                    {
                        leftTargetSpeedFraction = 1f;
                        rightTargetSpeedFraction = 1f;
                    }
                    //Note turns require a bit of help from the opposing track to overcome friction.
                    else if (left)
                    {
                        leftTargetSpeedFraction = 0.5f;
                        rightTargetSpeedFraction = 1f;
                    }
                    else if (right)
                    {
                        leftTargetSpeedFraction = 1f;
                        rightTargetSpeedFraction = 0.5f;
                    }
                }
                else if (backward)
                {
                    if ((left && right) || (!left && !right))
                    {
                        leftTargetSpeedFraction = -1f;
                        rightTargetSpeedFraction = -1f;
                    }
                    else if (left)
                    {
                        leftTargetSpeedFraction = -0.5f;
                        rightTargetSpeedFraction = -1f;
                    }
                    else if (right)
                    {
                        leftTargetSpeedFraction = -1f;
                        rightTargetSpeedFraction = -0.5f;
                    }
                }
                else
                {
                    //Not trying to move. Turn?
                    if (left && !right)
                    {
                        leftTargetSpeedFraction = -1f;
                        rightTargetSpeedFraction = 1f;
                    }
                    else if (right && !left)
                    {
                        leftTargetSpeedFraction = 1f;
                        rightTargetSpeedFraction = -1f;
                    }
                }

                var zoom = input.IsDown(Zoom);
                var brake = input.IsDown(Brake) || input.IsDown(BrakeAlternate);
                playerController.Update(Simulation, leftTargetSpeedFraction, rightTargetSpeedFraction, zoom, brake, brake, camera.Forward);
            }

            base.Update(window, camera, input, dt);
        }

        void RenderControl(ref Vector2 position, float textHeight, string controlName, string controlValue, TextBuilder text, TextBatcher textBatcher, Font font)
        {
            text.Clear().Append(controlName).Append(": ").Append(controlValue);
            textBatcher.Write(text, position, textHeight, new Vector3(1), font);
            position.Y += textHeight * 1.1f;
        }

        public override void Render(Renderer renderer, Camera camera, Input input, TextBuilder text, Font font)
        {
            if (playerControlActive)
            {
                var tankBody = new BodyReference(playerController.Tank.Body, Simulation.Bodies);
                Quaternion.TransformUnitY(tankBody.Pose.Orientation, out var tankUp);
                Quaternion.TransformUnitZ(tankBody.Pose.Orientation, out var tankBackward);
                var backwardDirection = camera.Backward;
                backwardDirection.Y = MathF.Max(backwardDirection.Y, -0.2f);
                camera.Position = tankBody.Pose.Position + tankUp * 2f + tankBackward * 0.4f + backwardDirection * 8;
            }

            var textHeight = 16;
            var position = new Vector2(32, renderer.Surface.Resolution.Y - 128);
            RenderControl(ref position, textHeight, nameof(Forward), Forward.ToString(), text, renderer.TextBatcher, font);
            RenderControl(ref position, textHeight, nameof(Backward), Backward.ToString(), text, renderer.TextBatcher, font);
            RenderControl(ref position, textHeight, nameof(Right), Right.ToString(), text, renderer.TextBatcher, font);
            RenderControl(ref position, textHeight, nameof(Left), Left.ToString(), text, renderer.TextBatcher, font);
            RenderControl(ref position, textHeight, nameof(Zoom), Zoom.ToString(), text, renderer.TextBatcher, font);
            RenderControl(ref position, textHeight, nameof(Brake), Brake.ToString(), text, renderer.TextBatcher, font);
            RenderControl(ref position, textHeight, nameof(ToggleTank), ToggleTank.ToString(), text, renderer.TextBatcher, font);
            base.Render(renderer, camera, input, text, font);
        }
    }
}