﻿namespace NoiseEngine.Rendering.Buffers.CommandBuffers;

internal enum CommandBufferCommand : ushort {
    CopyBuffer = 0,
    CopyBufferToTexture = 1,
    CopyTextureToBuffer = 2,
    Dispatch = 3,
    AttachCameraWindow = 4,
    AttachCameraTexture = 5,
    DetachCamera = 6,
    DrawMesh = 7,

    AttachShader = 10000
}
