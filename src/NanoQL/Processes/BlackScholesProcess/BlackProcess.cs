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
/// Black's (1976) process for a forward or futures contract, d ln S(t) = −σ(t, S)²/2 dt + σ dW_t: the generalized
/// process with its risk-free curve as the dividend curve too.
/// </summary>
public sealed class BlackProcess : GeneralizedBlackScholesProcess
{
    /// <summary>Initializes a new instance of the <see cref="BlackProcess"/> class.</summary>
    /// <param name="x0">The underlying's value.</param>
    /// <param name="riskFreeTS">The risk-free curve, which is the dividend curve too.</param>
    /// <param name="blackVolTS">The Black volatility.</param>
    /// <param name="d">The discretization, or <see langword="null"/> for QuantLib's default, Euler's.</param>
    /// <param name="forceDiscretization">
    /// Whether to use the discretization even where the local volatility is strike-independent.
    /// </param>
    /// <exception cref="ArgumentNullException">The curve or the volatility is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The Black volatility can't be read at time 0 and the spot.</exception>
    public BlackProcess(
        double x0,
        YieldTermStructure riskFreeTS,
        BlackVolTermStructure blackVolTS,
        StochasticProcess1DDiscretization? d = null,
        bool forceDiscretization = false)
        : base(x0, riskFreeTS, riskFreeTS, blackVolTS, d, forceDiscretization)
    {
    }
}
