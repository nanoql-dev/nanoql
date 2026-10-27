// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's constant-maturity swap-rate predictor-corrector evolver vectors,
/// <c>tests/vectors/cxx/models/marketmodels/evolvers/lognormalcmswapratepc.json</c>:
/// <see cref="LogNormalCmSwapRatePc"/>'s whole evolutions spanning 2 and 3 forwards, on the Mersenne-twister and
/// Sobol generators, under the terminal and money-market measures, from the first and a later step, with the model's
/// and a set initial state: QuantLib's set on its own state type, nanoql's on an LMM state of the same swap rates (the
/// S-mm plan's MR3 (v)); each step's weight, current step, forwards, discount ratios and coterminal swap rates; and
/// the refused numeraires.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LogNormalCmSwapRatePcCxxVectorTests
{
    [Fact]
    public void LogNormalCmSwapRatePcCxxVector() =>
        LogNormalFwdRateEulerCxxVectorTests.CheckEvolutions(
            "cxx/models/marketmodels/evolvers/lognormalcmswapratepc",
            (c, m, f, numeraires, initial) => new LogNormalCmSwapRatePc(c.Int("spanningForwards"), m, f, numeraires, initial));
}
