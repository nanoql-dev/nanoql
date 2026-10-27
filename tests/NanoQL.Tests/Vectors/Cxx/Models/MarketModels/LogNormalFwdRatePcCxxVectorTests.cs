// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's predictor-corrector evolver vectors,
/// <c>tests/vectors/cxx/models/marketmodels/evolvers/lognormalfwdratepc.json</c>: <see cref="LogNormalFwdRatePc"/>'s
/// whole evolutions on the Mersenne-twister and Sobol generators, under the terminal and money-market measures, from
/// the first and a later step, with the model's and a set initial state: each step's weight, current step, forwards,
/// discount ratios and coterminal swap rates; and the refused numeraires.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LogNormalFwdRatePcCxxVectorTests
{
    [Fact]
    public void LogNormalFwdRatePcCxxVector() =>
        LogNormalFwdRateEulerCxxVectorTests.CheckEvolutions(
            "cxx/models/marketmodels/evolvers/lognormalfwdratepc",
            (m, f, numeraires, initial) => new LogNormalFwdRatePc(m, f, numeraires, initial));
}
