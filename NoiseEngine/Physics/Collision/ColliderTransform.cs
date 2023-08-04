﻿using NoiseEngine.Mathematics;

namespace NoiseEngine.Physics.Collision;

internal readonly record struct ColliderTransform(
    Vector3<float> Position,
    Quaternion<float> Rotation,
    Vector3<float> Scale,
    Vector3<float> LinearVelocity,
    float InverseMass,
    // -(1f + Restitution)
    float RestitutionPlusOneNegative
);
