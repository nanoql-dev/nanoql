// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/parameter.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>A parameter which is always zero: a(t) = 0, with no parameters.</summary>
public sealed class NullParameter : Parameter
{
    /// <summary>Initializes a new instance of the <see cref="NullParameter"/> class.</summary>
    public NullParameter()
        : base(0, new NullImpl(), new NoConstraint())
    {
    }

    private sealed class NullImpl : Impl
    {
        public override double Value(ImmutableArray<double> parameters, double t) => 0.0;
    }
}
