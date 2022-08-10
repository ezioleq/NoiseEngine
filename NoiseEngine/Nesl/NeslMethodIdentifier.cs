﻿using NoiseEngine.Collections;
using System.Collections.Generic;

namespace NoiseEngine.Nesl;

internal readonly record struct NeslMethodIdentifier(string Name, ComparableReadOnlyList<NeslType> ParameterTypes);
