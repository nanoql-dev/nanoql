// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/xabrinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib's XABR <c>Model</c> concept: the seven members XABR's coefficient holder and fit call on a model's
/// specification (SABR's, ZABR's and the others), as static abstract members of a stateless struct.
/// </summary>
/// <typeparam name="TInstance">The model instance the specification builds.</typeparam>
/// <remarks>
/// QuantLib calls each on a value-initialized temporary (<c>Model().f(…)</c>), and no specification holds state.
/// <c>Inverse</c> and <c>Direct</c> map the model's parameters to the optimizer's unconstrained ones and back.
/// </remarks>
internal interface IXabrSpecs<TInstance>
    where TInstance : IXabrModelInstance
{
    /// <summary>Gets the number of the model's parameters.</summary>
    static abstract int Dimension { get; }

    /// <summary>Fills the parameters not given with the model's defaults.</summary>
    /// <param name="parameters">The parameters, <see langword="null"/> where not given; filled in place.</param>
    /// <param name="paramIsFixed">Whether each parameter is fixed.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="expiryTime">The time to expiry.</param>
    /// <param name="addParams">The model's additional parameters.</param>
    static abstract void DefaultValues(
        double?[] parameters, bool[] paramIsFixed, double forward, double expiryTime, ReadOnlySpan<double> addParams);

    /// <summary>Overwrites the free parameters with a guess from a point of the unit cube.</summary>
    /// <param name="values">The parameters; the free ones are overwritten in place.</param>
    /// <param name="paramIsFixed">Whether each parameter is fixed.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="expiryTime">The time to expiry.</param>
    /// <param name="r">The point, one coordinate per free parameter, consumed in the model's order.</param>
    /// <param name="addParams">The model's additional parameters.</param>
    static abstract void Guess(
        Vector values,
        ReadOnlySpan<bool> paramIsFixed,
        double forward,
        double expiryTime,
        ReadOnlySpan<double> r,
        ReadOnlySpan<double> addParams);

    /// <summary>The optimizer's parameters for the model's.</summary>
    /// <param name="y">The model's parameters.</param>
    /// <param name="paramIsFixed">Whether each parameter is fixed.</param>
    /// <param name="parameters">The fit's current parameters.</param>
    /// <param name="forward">The forward.</param>
    /// <returns>A new vector.</returns>
    static abstract Vector Inverse(Vector y, ReadOnlySpan<bool> paramIsFixed, ReadOnlySpan<double> parameters, double forward);

    /// <summary>The model's parameters for the optimizer's.</summary>
    /// <param name="x">The optimizer's parameters.</param>
    /// <param name="paramIsFixed">Whether each parameter is fixed.</param>
    /// <param name="parameters">The fit's current parameters.</param>
    /// <param name="forward">The forward.</param>
    /// <returns>A new vector.</returns>
    static abstract Vector Direct(Vector x, ReadOnlySpan<bool> paramIsFixed, ReadOnlySpan<double> parameters, double forward);

    /// <summary>A strike's vega weight, before normalization.</summary>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The strike's standard deviation.</param>
    /// <param name="addParams">The model's additional parameters.</param>
    /// <returns>The weight.</returns>
    static abstract double Weight(double strike, double forward, double stdDev, ReadOnlySpan<double> addParams);

    /// <summary>The model instance for a set of parameters.</summary>
    /// <param name="t">The time to expiry.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="parameters">The fit's own parameter array, which an instance may keep and read, as SABR's does, or
    /// copy, as ZABR's does.</param>
    /// <param name="addParams">The model's additional parameters.</param>
    /// <returns>The instance, after the model's checks of the parameters.</returns>
    static abstract TInstance Instance(double t, double forward, double[] parameters, ReadOnlySpan<double> addParams);
}
