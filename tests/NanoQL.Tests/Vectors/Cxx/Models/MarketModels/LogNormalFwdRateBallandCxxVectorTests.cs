// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Balland evolver vectors,
/// <c>tests/vectors/cxx/models/marketmodels/evolvers/lognormalfwdrateballand.json</c>:
/// <see cref="LogNormalFwdRateBalland"/>'s evolutions of one step from time 0, undisplaced, where QuantLib's geometric
/// mean of the time-0 and end forwards is nanoql's of the step's start and end (the S-mm plan's MR3 (xix)), on the
/// Mersenne-twister and Sobol generators, under the terminal and money-market measures: the step's weight, current
/// step, forwards, discount ratios and coterminal swap rates; and the refused numeraires.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LogNormalFwdRateBallandCxxVectorTests
{
    [Fact]
    public void LogNormalFwdRateBallandCxxVector() =>
        LogNormalFwdRateEulerCxxVectorTests.CheckEvolutions(
            "cxx/models/marketmodels/evolvers/lognormalfwdrateballand",
            (_, m, f, numeraires, initial) => new LogNormalFwdRateBalland(m, f, numeraires, initial));
}
