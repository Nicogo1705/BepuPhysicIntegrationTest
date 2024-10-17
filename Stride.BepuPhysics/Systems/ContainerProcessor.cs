﻿using System.Diagnostics;
using System.Runtime.CompilerServices;
using Stride.BepuPhysics.Components;
using Stride.Core.Annotations;
using Stride.Engine;
using Stride.Games;
using Stride.Rendering;
using Stride.Core.Mathematics;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Stride.BepuPhysics.Definitions;

namespace Stride.BepuPhysics.Systems;

public class ContainerProcessor : EntityProcessor<ContainerComponent>
{
    internal readonly UnsortedO1List<StaticComponent, Matrix4x4> Statics = new();

    internal ShapeCacheSystem ShapeCache { get; private set; } = null!;
    public Dictionary<ContainerComponent, ContainerComponent>.Enumerator ComponentDataEnumerator => base.ComponentDatas.GetEnumerator();

    public BepuConfiguration BepuConfiguration { get; private set; } = null!;

    public Action<ContainerComponent>? OnPostAdd;
    public Action<ContainerComponent>? OnPreRemove;

    public ContainerProcessor()
    {
        Order = SystemsOrderHelper.ORDER_OF_CONTAINER_P;
    }

    protected override void OnSystemAdd()
    {
        ServicesHelper.LoadBepuServices(Services);
        BepuConfiguration = Services.GetService<BepuConfiguration>();
        ShapeCache = Services.GetService<IGame>().Services.GetService<ShapeCacheSystem>();
    }

    public override void Draw(RenderContext context) // While this is not related to drawing, we're doing this in draw as it runs after the TransformProcessor updates WorldMatrix
    {
        base.Draw(context);

#warning should be changed to dispatcher's ForBatch from master when it releases
        var span = Statics.UnsafeGetSpan();
        for (int i = 0; i < span.Length; i++)
        {
            var container = span[i].Key;
            ref Matrix4x4 numericMatrix = ref Unsafe.As<Matrix, Matrix4x4>(ref container.Entity.Transform.WorldMatrix); // Casting to numerics, stride's equality comparison is ... not great
            if (span[i].Value == numericMatrix)
                continue; // This static did not move

            span[i].Value = numericMatrix;

            if (container.StaticReference is { } sRef)
            {
                var description = sRef.GetDescription();
                container.Entity.Transform.WorldMatrix.Decompose(out _, out Quaternion rotation, out Vector3 translation);
                description.Pose.Position = (translation + container.CenterOfMass).ToNumericVector();
                description.Pose.Orientation = rotation.ToNumericQuaternion();
                sRef.ApplyDescription(description);
            }
        }
    }

    protected override void OnEntityComponentAdding(Entity entity, [NotNull] ContainerComponent component, [NotNull] ContainerComponent data)
    {
        Debug.Assert(BepuConfiguration is not null);

        component.Processor = this;

        var targetSimulation = BepuConfiguration.BepuSimulations[component.SimulationIndex];
        component.ReAttach(targetSimulation);

        if (component is ISimulationUpdate simulationUpdate)
            targetSimulation.Register(simulationUpdate);
    }

    protected override void OnEntityComponentRemoved(Entity entity, [NotNull] ContainerComponent component, [NotNull] ContainerComponent data)
    {
        if (component is ISimulationUpdate simulationUpdate)
            component.Simulation?.Unregister(simulationUpdate);

        component.Detach();

        component.Processor = null;
    }
}