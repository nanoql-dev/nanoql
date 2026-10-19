// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/parameter.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>The standard constant parameter: a(t) = a.</summary>
public sealed class ConstantParameter : Parameter
{
    /// <summary>Initializes a new instance of the <see cref="ConstantParameter"/> class, its value 0, unchecked.</summary>
    /// <param name="constraint">The constraint on the value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="constraint"/> is null.</exception>
    /// <remarks>QuantLib leaves the value uninitialized.</remarks>
    public ConstantParameter(Constraint constraint)
        : base(1, new ConstantImpl(), constraint)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ConstantParameter"/> class with its value.</summary>
    /// <param name="value">The value.</param>
    /// <param name="constraint">The constraint on the value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="constraint"/> is null.</exception>
    /// <exception cref="ArgumentException">The value doesn't satisfy the constraint.</exception>
    public ConstantParameter(double value, Constraint constraint)
        : this(value, constraint, nameof(value))
    {
    }

    // For a model's constructor, whose own argument the refusal names.
    internal ConstantParameter(double value, Constraint constraint, string paramName)
        : base([Checked(value, constraint, paramName)], new ConstantImpl(), constraint)
    {
    }

    private static double Checked(double value, Constraint constraint, string paramName)
    {
        ArgumentNullException.ThrowIfNull(constraint);
        if (!constraint.Test(new Vector(1, value)))
        {
            throw new ArgumentException($"{value.ToString(CultureInfo.InvariantCulture)}: invalid value", paramName);
        }

        return value;
    }

    private sealed class ConstantImpl : Impl
    {
        public override double Value(ImmutableArray<double> parameters, double t) => parameters[0];
    }
}
