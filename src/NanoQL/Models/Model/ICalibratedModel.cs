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

/// <summary>
/// A model that can be calibrated: what a calibration and the calibration helpers read of it, which QuantLib's
/// <c>CalibratedModel</c> declares.
/// </summary>
/// <remarks>
/// A model is immutable: its parameters are fixed when it is built, and <see cref="WithParams"/> returns a new model.
/// <see cref="CalibratedModel"/> implements the contract with QuantLib's machinery; a model whose class derives from
/// another base implements it over its own. The extension <c>SetParams</c> returns the new model as the receiver's
/// type.
/// </remarks>
public interface ICalibratedModel
{
    /// <summary>Gets the model's arguments, the parameters calibration works on, in order.</summary>
    ImmutableArray<Parameter> Arguments { get; }

    /// <summary>Gets the constraint the model imposes on its parameters: each argument's own, on its slice.</summary>
    Constraint Constraint { get; }

    /// <summary>
    /// Gets the parameters a calibration fixes when its caller fixes none: one flag per parameter, or empty, the
    /// default, for all free.
    /// </summary>
    /// <remarks>
    /// QuantLib's models that override <c>calibrate</c> to fix parameters by default implement it: the
    /// Markov-functional model fixes its first volatility, since its prices don't depend on the volatilities' level.
    /// Every other model keeps the empty default, QuantLib's.
    /// </remarks>
    ImmutableArray<bool> DefaultFixParameters => [];

    /// <summary>The parameters calibration works on: every argument's parameters, in order.</summary>
    /// <returns>A new vector holding them.</returns>
    Vector Params();

    /// <summary>A new model, of the receiver's type, with the given parameters: QuantLib's <c>setParams</c>.</summary>
    /// <param name="parameters">The parameters, as many as <see cref="Params"/> gives.</param>
    /// <returns>The new model; the receiver doesn't change.</returns>
    ICalibratedModel WithParams(Vector parameters);
}
