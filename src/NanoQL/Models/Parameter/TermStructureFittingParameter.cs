// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/parameter.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Models;

/// <summary>A deterministic time-dependent parameter used for yield-curve fitting, with no parameters of its own.</summary>
/// <remarks>
/// <para>
/// Built on a curve, its implementation is a <see cref="NumericalImpl"/>, the values a tree fits as it is built: a
/// fitted short-rate tree fills them during its construction, step by step, and reads them afterwards, so each tree
/// owns its fitting and a built tree's fitting never changes.
/// </para>
/// <para>An extension point, as in QuantLib: Hull-White's analytical fitting parameter derives from it.</para>
/// </remarks>
public class TermStructureFittingParameter : Parameter
{
    /// <summary>Initializes a new instance of the <see cref="TermStructureFittingParameter"/> class with an implementation.</summary>
    /// <param name="impl">The implementation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="impl"/> is null.</exception>
    protected internal TermStructureFittingParameter(Impl impl)
        : base(0, impl, new NoConstraint())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TermStructureFittingParameter"/> class with numerical values to fit to a curve.</summary>
    /// <param name="term">The curve.</param>
    /// <exception cref="ArgumentNullException"><paramref name="term"/> is null.</exception>
    public TermStructureFittingParameter(YieldTermStructure term)
        : base(0, new NumericalImpl(term), new NoConstraint())
    {
    }

    /// <summary>The numerical implementation: the fitted values at the times a tree sets them.</summary>
    /// <remarks>
    /// Its values are written by the tree that fits them while the tree is built, through internal members, and read
    /// afterwards. A time is looked up exactly, as QuantLib's <c>std::find</c> does.
    /// </remarks>
    protected internal class NumericalImpl : Impl
    {
        private readonly List<double> times = [];
        private readonly List<double> values = [];

        /// <summary>Initializes a new instance of the <see cref="NumericalImpl"/> class, with no values.</summary>
        /// <param name="termStructure">The curve the values fit.</param>
        /// <exception cref="ArgumentNullException"><paramref name="termStructure"/> is null.</exception>
        public NumericalImpl(YieldTermStructure termStructure)
        {
            ArgumentNullException.ThrowIfNull(termStructure);
            TermStructure = termStructure;
        }

        /// <summary>Gets the curve the values fit.</summary>
        public YieldTermStructure TermStructure { get; }

        /// <summary>The fitted value at a time.</summary>
        /// <param name="parameters">The parameters, unused.</param>
        /// <param name="t">The time, one the tree set.</param>
        /// <returns>The value.</returns>
        /// <exception cref="NanoQLException">No value was set at <paramref name="t"/>.</exception>
        public override double Value(ImmutableArray<double> parameters, double t)
        {
            // std::find: the first time equal to t by ==, so a NaN is never found
            for (int i = 0; i < times.Count; i++)
            {
                if (times[i] == t)
                {
                    return values[i];
                }
            }

            throw new NanoQLException("fitting parameter not set!");
        }

        // A tree's construction appends a value at a time.
        internal void Set(double t, double x)
        {
            times.Add(t);
            values.Add(x);
        }

        // A tree's construction rewrites the last value.
        internal void Change(double x) => values[^1] = x;

        // A tree's construction clears the values.
        internal void Reset()
        {
            times.Clear();
            values.Clear();
        }
    }
}
