﻿using NoiseEngine.Tests.Environments;
using NoiseEngine.Tests.Fixtures;
using System.Threading;

namespace NoiseEngine.Tests;

public class WindowTest : ApplicationTestEnvironment {

    public WindowTest(ApplicationFixture fixture) : base(fixture) {
    }

    [FactRequire(TestRequirements.Gui)]
    public void Create() {
        using Window window = new Window();
    }

}
