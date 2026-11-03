// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's iterative Balland evolver vectors,
/// <c>tests/vectors/cxx/models/marketmodels/evolvers/lognormalfwdrateiballand.json</c>:
/// <see cref="LogNormalFwdRateiBalland"/>'s evolutions of one step from time 0, undisplaced, where QuantLib's
/// geometric mean is nanoql's, on the Mersenne-twister and Sobol generators, under the terminal measure: the
/// step's weight, current step, forwards, discount ratios and coterminal swap rates; and the refused numeraires,
/// the money-market measure among them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LogNormalFwdRateiBallandCxxVectorTests
{
    [Fact]
    public void LogNormalFwdRateiBallandCxxVector() =>
        LogNormalFwdRateEulerCxxVectorTests.CheckEvolutions(
            "cxx/models/marketmodels/evolvers/lognormalfwdrateiballand",
            (_, m, f, numeraires, initial) => new LogNormalFwdRateiBalland(m, f, numeraires, initial));
}
