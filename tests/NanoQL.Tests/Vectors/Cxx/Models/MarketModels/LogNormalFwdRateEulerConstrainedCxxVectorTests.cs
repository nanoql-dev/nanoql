// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's constrained Euler evolver vectors,
/// <c>tests/vectors/cxx/models/marketmodels/evolvers/lognormalfwdrateeulerconstrained.json</c>:
/// <see cref="LogNormalFwdRateEulerConstrained"/>'s whole evolutions with each step's first alive rate constrained at
/// every step, or the last rate at even steps, on the Mersenne-twister and Sobol generators, under the terminal and
/// money-market measures, from the first and a later step, with the model's and a set initial state, on models of equal
/// displacements: each step's weight, current step, forwards, discount ratios and coterminal swap rates. QuantLib's
/// constraint type is set again before each step, so each step's covariances are its own pseudo-root's, as nanoql's
/// constraint type, set once, builds them (the S-mm plan's MR3 (vi)).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LogNormalFwdRateEulerConstrainedCxxVectorTests
{
    [Fact]
    public void LogNormalFwdRateEulerConstrainedCxxVector() =>
        LogNormalFwdRateEulerCxxVectorTests.CheckEvolutions(
            "cxx/models/marketmodels/evolvers/lognormalfwdrateeulerconstrained",
            (_, m, f, numeraires, initial) => new LogNormalFwdRateEulerConstrained(m, m, f, numeraires, initial),
            prepare: (c, e) =>
            {
                var constrained = (LogNormalFwdRateEulerConstrained)e;
                int[] start = Integers(c.Input("constrainedRates"));
                constrained.SetConstraintType(start, [.. start.Select(s => s + 1)]);
                bool[] active = [.. c.Input("isConstraintActive").EnumerateArray().Select(x => x.GetBoolean())];
                constrained.SetThisConstraint(Reals(c.Input("rateConstraints")), active);
            });
}
