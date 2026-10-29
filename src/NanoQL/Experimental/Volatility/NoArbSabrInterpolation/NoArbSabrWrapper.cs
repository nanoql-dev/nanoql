// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/noarbsabrinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// QuantLib's <c>detail::NoArbSabrWrapper</c>, its typedef of <see cref="NoArbSabrSmileSection"/>: the section the
/// no-arbitrage SABR fit evaluates at each trial parameter set.
/// </summary>
[Experimental("NQL0001")]
internal sealed class NoArbSabrWrapper : IXabrModelInstance
{
    private readonly NoArbSabrSmileSection section;

    public NoArbSabrWrapper(double t, double forward, double[] parameters) =>
        section = new NoArbSabrSmileSection(t, forward, parameters);

    public double Volatility(double x, VolatilityType volatilityType) => section.Volatility(x, volatilityType);
}
