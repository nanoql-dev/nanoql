// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2013, 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/model.hpp ql/models/model.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>The calibrated model class: <see cref="ICalibratedModel"/> over QuantLib's arguments and constraint.</summary>
/// <remarks>
/// <para>
/// Immutable once built. A derived constructor sets its arguments with <see cref="SetArgument"/> and derives what it
/// computes from them in <see cref="GenerateArguments"/>; <see cref="WithParams"/> copies the model, rewrites the copy's
/// arguments and runs <see cref="GenerateArguments"/> on the copy, so the receiver never changes. The arguments a
/// derived constructor hasn't set are null parameters, where QuantLib's are default-constructed parameters with no
/// value. <see cref="SetArgument"/> and <see cref="GenerateArguments"/>'s writes are for a model under construction or
/// a fresh copy only, a convention the base can't enforce.
/// </para>
/// <para>
/// An extension point, as in QuantLib: the short-rate models derive from it, as later ports' Heston models will. A
/// model with another base class implements <see cref="ICalibratedModel"/> over its own, through this class's internal
/// helpers.
/// </para>
/// </remarks>
public abstract class CalibratedModel : ICalibratedModel
{
    // Written by SetArgument while a derived constructor runs, and by WithParams on a fresh copy only.
    private ImmutableArray<Parameter> arguments;
    private Constraint constraint;

    /// <summary>Initializes a new instance of the <see cref="CalibratedModel"/> class.</summary>
    /// <param name="nArguments">The number of arguments, which a derived constructor sets.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nArguments"/> is negative.</exception>
    protected CalibratedModel(int nArguments)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(nArguments);
        var unset = ImmutableArray.CreateBuilder<Parameter>(nArguments);
        for (int i = 0; i < nArguments; i++)
        {
            unset.Add(new NullParameter());
        }

        arguments = unset.MoveToImmutable();
        constraint = new PrivateConstraint(arguments);
    }

    /// <inheritdoc/>
    public ImmutableArray<Parameter> Arguments => arguments;

    /// <inheritdoc/>
    public Constraint Constraint => constraint;

    /// <inheritdoc/>
    public Vector Params() => FlattenParams(arguments);

    /// <summary>A new model, of the receiver's type, with the given parameters: QuantLib's <c>setParams</c>.</summary>
    /// <param name="parameters">The parameters, as many as <see cref="Params"/> gives.</param>
    /// <returns>The new model; the receiver doesn't change.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is null.</exception>
    /// <exception cref="ArgumentException">The parameters are too few or too many.</exception>
    /// <remarks>
    /// As QuantLib's, the parameters aren't checked against the arguments' constraints. QuantLib's writes the parameters
    /// it has before refusing too few, and all of them before refusing too many; the refusals here come before any
    /// write.
    /// </remarks>
    public virtual ICalibratedModel WithParams(Vector parameters)
    {
        ImmutableArray<Parameter> newArguments = ArgumentsWithParams(arguments, parameters);
        var copy = (CalibratedModel)MemberwiseClone();
        copy.arguments = newArguments;
        copy.constraint = new PrivateConstraint(newArguments);
        copy.GenerateArguments();
        return copy;
    }

    // The flattening QuantLib's params() does, for any model's arguments.
    internal static Vector FlattenParams(ImmutableArray<Parameter> arguments)
    {
        int size = 0;
        foreach (Parameter argument in arguments)
        {
            size += argument.Size;
        }

        var parameters = new Vector(size);
        for (int i = 0, k = 0; i < arguments.Length; ++i)
        {
            Vector p = arguments[i].Params();
            for (int j = 0; j < p.Size; ++j, ++k)
            {
                parameters[k] = p[j];
            }
        }

        return parameters;
    }

    // QuantLib's setParams on a copy of the arguments: the refusals first, then each argument's parameters in order.
    internal static ImmutableArray<Parameter> ArgumentsWithParams(ImmutableArray<Parameter> arguments, Vector parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        int size = 0;
        foreach (Parameter argument in arguments)
        {
            size += argument.Size;
        }

        if (parameters.Size < size)
        {
            throw new ArgumentException("parameter array too small", nameof(parameters));
        }

        if (parameters.Size > size)
        {
            throw new ArgumentException("parameter array too big!", nameof(parameters));
        }

        var result = ImmutableArray.CreateBuilder<Parameter>(arguments.Length);
        int p = 0;
        foreach (Parameter argument in arguments)
        {
            Parameter updated = argument;
            for (int j = 0; j < argument.Size; ++j, ++p)
            {
                updated = updated.SetParam(j, parameters[p]);
            }

            result.Add(updated);
        }

        return result.MoveToImmutable();
    }

    /// <summary>
    /// Derives from the arguments whatever else the model computes, after its constructor sets them and on the copy
    /// <see cref="WithParams"/> returns; does nothing by default.
    /// </summary>
    protected virtual void GenerateArguments()
    {
    }

    /// <summary>Sets an argument, while a derived constructor runs: QuantLib's assignment to <c>arguments_[i]</c>.</summary>
    /// <param name="i">The argument's index.</param>
    /// <param name="argument">The argument.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> is outside the arguments.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="argument"/> is null.</exception>
    protected void SetArgument(int i, Parameter argument)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(i, arguments.Length);
        ArgumentNullException.ThrowIfNull(argument);
        arguments = arguments.SetItem(i, argument);
        constraint = new PrivateConstraint(arguments);
    }

    /// <summary>The constraint imposed on the arguments: each argument's own constraint, on its slice of the parameters.</summary>
    internal sealed class PrivateConstraint : Constraint
    {
        private readonly ImmutableArray<Parameter> arguments;

        public PrivateConstraint(ImmutableArray<Parameter> arguments) => this.arguments = arguments;

        public override bool Test(Vector p)
        {
            ArgumentNullException.ThrowIfNull(p);
            int k = 0;
            foreach (Parameter argument in arguments)
            {
                int size = argument.Size;
                var testParams = new Vector(size);
                for (int j = 0; j < size; j++, k++)
                {
                    testParams[j] = p[k];
                }

                if (!argument.TestParams(testParams))
                {
                    return false;
                }
            }

            return true;
        }

        protected override Vector UpperBoundCore(Vector parameters) =>
            Bound(parameters, (argument, partialParams) => argument.Constraint.UpperBound(partialParams));

        protected override Vector LowerBoundCore(Vector parameters) =>
            Bound(parameters, (argument, partialParams) => argument.Constraint.LowerBound(partialParams));

        private Vector Bound(Vector parameters, Func<Parameter, Vector, Vector> bound)
        {
            int k = 0, k2 = 0;
            int totalSize = 0;
            foreach (Parameter argument in arguments)
            {
                totalSize += argument.Size;
            }

            var result = new Vector(totalSize);
            foreach (Parameter argument in arguments)
            {
                int size = argument.Size;
                var partialParams = new Vector(size);
                for (int j = 0; j < size; j++, k++)
                {
                    partialParams[j] = parameters[k];
                }

                Vector tmpBound = bound(argument, partialParams);
                for (int j = 0; j < size; j++, k2++)
                {
                    result[k2] = tmpBound[j];
                }
            }

            return result;
        }
    }
}
