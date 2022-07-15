﻿using NoiseEngine.Jobs;
using System.Threading;

namespace NoiseEngine.Tests.Jobs;

internal class TestSystemCounter : EntitySystem {

    private int entityCount;

    public int EntityCount { get; private set; }

    protected override void OnUpdate() {
        entityCount = 0;
    }

    protected override void OnUpdateEntity(Entity entity) {
        Interlocked.Increment(ref entityCount);
    }

    protected override void OnLateUpdate() {
        EntityCount = entityCount;
    }

}
