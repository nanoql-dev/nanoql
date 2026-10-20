// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006, 2007, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/blackscholesprocess.hpp ql/processes/blackscholesprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;

namespace NanoQL.Processes;

/// <summary>
/// The Garman-Kohlhagen (1983) process for an exchange rate, d ln S(t) = (r(t) − r_f(t) − σ(t, S)²/2) dt + σ dW_t:
/// the generalized process with the foreign risk-free curve as the dividend curve. QuantLib's spelling of the class
/// name is kept.
/// </summary>
public sealed class GarmanKohlagenProcess : GeneralizedBlackScholesProcess
{
    /// <summary>Initializes a new instance of the <see cref="GarmanKohlagenProcess"/> class.</summary>
    /// <param name="x0">The exchange rate.</param>
    /// <param name="foreignRiskFreeTS">The foreign risk-free curve.</param>
    /// <param name="domesticRiskFreeTS">The domestic risk-free curve.</param>
    /// <param name="blackVolTS">The Black volatility.</param>
    /// <param name="d">The discretization, or <see langword="null"/> for QuantLib's default, Euler's.</param>
    /// <param name="forceDiscretization">
    /// Whether to use the discretization even where the local volatility is strike-independent.
    /// </param>
    /// <exception cref="ArgumentNullException">A curve or the volatility is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The Black volatility can't be read at time 0 and the spot.</exception>
    public GarmanKohlagenProcess(
        double x0,
        YieldTermStructure foreignRiskFreeTS,
        YieldTermStructure domesticRiskFreeTS,
        BlackVolTermStructure blackVolTS,
        StochasticProcess1DDiscretization? d = null,
        bool forceDiscretization = false)
        : base(x0, foreignRiskFreeTS, domesticRiskFreeTS, blackVolTS, d, forceDiscretization)
    {
    }
}
