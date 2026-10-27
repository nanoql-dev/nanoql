// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's coterminal swap-rate predictor-corrector evolver vectors,
/// <c>tests/vectors/cxx/models/marketmodels/evolvers/lognormalcotswapratepc.json</c>:
/// <see cref="LogNormalCotSwapRatePc"/>'s whole evolutions on the Mersenne-twister and Sobol generators, under the
/// terminal and money-market measures, from the first and a later step, with the model's and a set initial state:
/// QuantLib's set on its own state type, nanoql's on an LMM state of the same coterminal swap rates (the S-mm plan's
/// MR3 (v)); each step's weight, current step, forwards, discount ratios and coterminal swap rates; and the refused
/// numeraires.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LogNormalCotSwapRatePcCxxVectorTests
{
    [Fact]
    public void LogNormalCotSwapRatePcCxxVector() =>
        LogNormalFwdRateEulerCxxVectorTests.CheckEvolutions(
            "cxx/models/marketmodels/evolvers/lognormalcotswapratepc",
            (_, m, f, numeraires, initial) => new LogNormalCotSwapRatePc(m, f, numeraires, initial));
}
