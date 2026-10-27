// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's iterative predictor-corrector evolver vectors,
/// <c>tests/vectors/cxx/models/marketmodels/evolvers/lognormalfwdrateipc.json</c>: <see cref="LogNormalFwdRateIpc"/>'s
/// whole evolutions on the Mersenne-twister and Sobol generators, under the terminal measure, from the first and a
/// later step, with the model's and a set initial state: each step's weight, current step, forwards, discount ratios
/// and coterminal swap rates; and the refused numeraires, the money-market measure among them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LogNormalFwdRateIpcCxxVectorTests
{
    [Fact]
    public void LogNormalFwdRateIpcCxxVector() =>
        LogNormalFwdRateEulerCxxVectorTests.CheckEvolutions(
            "cxx/models/marketmodels/evolvers/lognormalfwdrateipc",
            (_, m, f, numeraires, initial) => new LogNormalFwdRateIpc(m, f, numeraires, initial));
}
