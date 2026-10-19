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

using NanoQL.TermStructures;

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib's <c>Model::type</c> in XABR's template: the model instance whose volatility an XABR interpolation returns.
/// The one member QuantLib's template calls on it.
/// </summary>
internal interface IXabrModelInstance
{
    /// <summary>The model's volatility at a strike.</summary>
    /// <param name="x">The strike.</param>
    /// <param name="volatilityType">The volatility type the interpolation was built with.</param>
    /// <returns>The volatility.</returns>
    double Volatility(double x, VolatilityType volatilityType);
}
