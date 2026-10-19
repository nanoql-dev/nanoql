// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/parameter.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>The base class for model arguments: a function of time defined by an array of parameters.</summary>
/// <remarks>
/// <para>
/// Immutable: <see cref="SetParam"/> returns a copy, so a model whose parameters change is a new model. Its parameters
/// start at 0, where QuantLib leaves an uninitialized array.
/// </para>
/// <para>
/// An extension point, as in QuantLib: the constant, null, piecewise-constant and term-structure fitting parameters
/// derive from it, as later ports' models' parameters will, each with an <see cref="Impl"/> that computes its value.
/// QuantLib's public default constructor, which serves only containers, has no counterpart.
/// </para>
/// </remarks>
public abstract class Parameter
{
    private readonly Impl impl;

    // Written only on a fresh copy (SetParam).
    private ImmutableArray<double> parameters;

    /// <summary>Initializes a new instance of the <see cref="Parameter"/> class, its parameters at 0.</summary>
    /// <param name="size">The number of parameters.</param>
    /// <param name="impl">The implementation that computes the parameter's value.</param>
    /// <param name="constraint">The constraint on the parameters.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="impl"/> or <paramref name="constraint"/> is null.</exception>
    protected internal Parameter(int size, Impl impl, Constraint constraint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        ArgumentNullException.ThrowIfNull(impl);
        ArgumentNullException.ThrowIfNull(constraint);
        this.impl = impl;
        parameters = [.. new double[size]];
        Constraint = constraint;
    }

    /// <summary>Initializes a new instance of the <see cref="Parameter"/> class with its parameters.</summary>
    /// <param name="parameters">The parameters, copied.</param>
    /// <param name="impl">The implementation that computes the parameter's value.</param>
    /// <param name="constraint">The constraint on the parameters, which they aren't tested against here.</param>
    /// <exception cref="ArgumentNullException"><paramref name="impl"/> or <paramref name="constraint"/> is null.</exception>
    protected internal Parameter(ReadOnlySpan<double> parameters, Impl impl, Constraint constraint)
    {
        ArgumentNullException.ThrowIfNull(impl);
        ArgumentNullException.ThrowIfNull(constraint);
        this.impl = impl;
        this.parameters = [.. parameters];
        Constraint = constraint;
    }

    /// <summary>Gets the number of parameters.</summary>
    public int Size => parameters.Length;

    /// <summary>Gets the constraint on the parameters.</summary>
    public Constraint Constraint { get; }

    /// <summary>The parameters.</summary>
    /// <returns>A new vector holding them.</returns>
    public Vector Params() => new(parameters.AsSpan());

    /// <summary>A copy of the parameter with one of its parameters set, unchecked against the constraint, as QuantLib's is.</summary>
    /// <param name="i">The parameter's index.</param>
    /// <param name="x">Its value.</param>
    /// <returns>The copy, of the receiver's type.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="i"/> is outside the parameters, where QuantLib writes past its array.
    /// </exception>
    public Parameter SetParam(int i, double x)
    {
        if ((uint)i >= (uint)parameters.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(i), i, $"parameter index {i} out of range [0, {parameters.Length})");
        }

        var copy = (Parameter)MemberwiseClone();
        copy.parameters = parameters.SetItem(i, x);
        return copy;
    }

    /// <summary>Tests the given parameters against the constraint.</summary>
    /// <param name="parameters">The parameters.</param>
    /// <returns><see langword="true"/> when they satisfy it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is null.</exception>
    public bool TestParams(Vector parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return Constraint.Test(parameters);
    }

    /// <summary>The parameter's value at a time: QuantLib's <c>operator()</c>.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The value.</returns>
    public double Evaluate(double t) => impl.Value(parameters, t);

    /// <summary>The implementation that computes the parameter's value.</summary>
    /// <returns>The implementation.</returns>
    protected internal Impl Implementation() => impl;

    /// <summary>The base class for a parameter's implementation: its value at a time from the parameters.</summary>
    protected internal abstract class Impl
    {
        /// <summary>The parameter's value at a time.</summary>
        /// <param name="parameters">The parameters.</param>
        /// <param name="t">The time.</param>
        /// <returns>The value.</returns>
        public abstract double Value(ImmutableArray<double> parameters, double t);
    }
}
