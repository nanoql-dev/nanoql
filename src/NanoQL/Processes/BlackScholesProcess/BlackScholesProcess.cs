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
using NanoQL.Time;

namespace NanoQL.Processes;

/// <summary>
/// The Black-Scholes (1973) stochastic process, d ln S(t) = (r(t) − σ(t, S)²/2) dt + σ dW_t: the generalized process
/// with no dividend yield. The interface is expressed in S; the internal calculations work on ln S.
/// </summary>
/// <remarks>
/// QuantLib builds its dividend curve as <c>FlatForward(0, NullCalendar(), 0.0, Actual365Fixed())</c>, whose
/// reference date is the evaluation date, read wherever the curve is (the analytic engine measures the dividend rho's
/// time from it). <see cref="Create"/> builds that curve under the context it is given, so the process takes the
/// context where QuantLib's constructor takes none.
/// </remarks>
public sealed class BlackScholesProcess : GeneralizedBlackScholesProcess
{
    private BlackScholesProcess(
        double x0,
        YieldTermStructure dividendTS,
        YieldTermStructure riskFreeTS,
        BlackVolTermStructure blackVolTS,
        StochasticProcess1DDiscretization? d,
        bool forceDiscretization)
        : base(x0, dividendTS, riskFreeTS, blackVolTS, d, forceDiscretization)
    {
    }

    /// <summary>
    /// A Black-Scholes process, its zero dividend curve a flat zero rate on Actual/365 (Fixed) from the context's
    /// evaluation date.
    /// </summary>
    /// <param name="x0">The underlying's value.</param>
    /// <param name="riskFreeTS">The risk-free curve.</param>
    /// <param name="blackVolTS">The Black volatility.</param>
    /// <param name="context">The evaluation context, which dates the dividend curve.</param>
    /// <param name="d">The discretization, or <see langword="null"/> for QuantLib's default, Euler's.</param>
    /// <param name="forceDiscretization">
    /// Whether to use the discretization even where the local volatility is strike-independent.
    /// </param>
    /// <returns>The process.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/>, the curve or the volatility is <see langword="null"/>.
    /// </exception>
    /// <exception cref="NanoQLException">The Black volatility can't be read at time 0 and the spot.</exception>
    public static BlackScholesProcess Create(
        double x0,
        YieldTermStructure riskFreeTS,
        BlackVolTermStructure blackVolTS,
        EvaluationContext context,
        StochasticProcess1DDiscretization? d = null,
        bool forceDiscretization = false)
    {
        ArgumentNullException.ThrowIfNull(context);

        // no dividend yield
        FlatForward dividendTS = FlatForward.Create(0, new NullCalendar(), 0.0, new Actual365Fixed(), context);
        return new BlackScholesProcess(x0, dividendTS, riskFreeTS, blackVolTS, d, forceDiscretization);
    }
}
