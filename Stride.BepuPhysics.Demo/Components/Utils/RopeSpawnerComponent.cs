﻿using System;
using System.Collections.Generic;
using System.Linq;
using Stride.BepuPhysics.Constraints;
using Stride.Core.Mathematics;
using Stride.Engine;

#warning This need rework/Rename and could be part of the API

namespace Stride.BepuPhysics.Demo.Components.Utils
{
    //[DataContract("SpawnerComponent", Inherited = true)]
    [ComponentCategory("BepuDemo - Utils")]
    public class RopeSpawnerComponent : StartupScript
    {
        public int SimulationIndex { get; set; } = 0;
        public Prefab? RopePart { get; set; } //The rope part must be long in Z
        public float RopePartSize { get; set; } = 1.0f; //the z size of the rope part


        private BodyComponent? A { get; set; }
        public Entity? AEntity { get; set; }
        public Vector3 APos { get; set; } = new(0.5f, 0, 0);


		private BodyComponent? B { get; set; }
        public Entity? BEntity { get; set; }
        public Vector3 BPos { get; set; } = new(-0.5f, 0, 0);


        public override void Start()
        {
            A = AEntity?.Get<BodyComponent>();
            B = BEntity?.Get<BodyComponent>();

            if (RopePart == null || A == null || B == null)
                return;

            var start = A.Entity.Transform.GetWorldPos() + APos;
            var end = B.Entity.Transform.GetWorldPos() + BPos;

            var seg = end - start;
            var dir = end - start;
            dir.Normalize();

            var len = seg.Length() / RopePartSize;
            var bodiesContainers = new List<BodyComponent>();

            for (var i = 0; i < len; i++)
            {
                var entity = RopePart.Instantiate().First();
                entity.Transform.Position = start + dir * RopePartSize * i;
                entity.Transform.Rotation = Quaternion.LookRotation(dir, Vector3.UnitY);
                var body = entity.Get<BodyComponent>();
                body.SimulationIndex = SimulationIndex;
                
                bodiesContainers.Add(body);
                entity.SetParent(Entity);
            }

            for (int i = 1; i < bodiesContainers.Count; i++)
            {
                var bds = new[] { bodiesContainers[i - 1], bodiesContainers[i] };
                var bs = new BallSocketConstraintComponent();
                var sl = new SwingLimitConstraintComponent();

                bs.A = bodiesContainers[i - 1];
                bs.B = bodiesContainers[i];
                bs.LocalOffsetA = Vector3.UnitZ * RopePartSize / 2f;
                bs.LocalOffsetB = -bs.LocalOffsetA;
                bs.SpringFrequency = 120;
                bs.SpringDampingRatio = 1;

                sl.A = bodiesContainers[i - 1];
                sl.B = bodiesContainers[i];
                sl.AxisLocalA = Vector3.UnitZ;
                sl.AxisLocalB = Vector3.UnitZ;
                sl.SpringFrequency = 120;
                sl.SpringDampingRatio = 1;
                sl.MaximumSwingAngle = MathF.PI * 0.05f;

                Entity.Add(bs);
                Entity.Add(sl);
            }

            var bs1 = new BallSocketConstraintComponent();
            bs1.A = A;
            bs1.B = bodiesContainers.First();
            bs1.LocalOffsetA = APos;
            bs1.LocalOffsetB = -(Vector3.UnitZ * RopePartSize) / 2f;

            var bs2 = new BallSocketConstraintComponent();
            bs2.A = B;
            bs2.B = bodiesContainers.Last();
            bs2.LocalOffsetA = BPos;
            bs2.LocalOffsetB = Vector3.UnitZ * RopePartSize / 2f;

            Entity.Add(bs1);
            Entity.Add(bs2);
        }
    }
}
