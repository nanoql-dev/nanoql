// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The iterative predictor-corrector evolver's vectors,
/// <c>tests/vectors/models/marketmodels/evolvers/lognormalfwdrateipc.json</c>:
/// <see cref="LogNormalFwdRateIpc"/>'s whole evolutions on AbcdVol models, on the Mersenne-twister and Sobol
/// generators, under the terminal measure, from the first and a later step, with the model's and a set initial state:
/// each step's weight, current step, forwards, discount ratios and coterminal swap rates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LogNormalFwdRateIpcVectorTests
{
    [Fact]
    public void LogNormalFwdRateIpcVector() =>
        LogNormalFwdRateEulerCxxVectorTests.CheckEvolutions(
            "models/marketmodels/evolvers/lognormalfwdrateipc",
            (_, m, f, numeraires, initial) => new LogNormalFwdRateIpc(m, f, numeraires, initial));
}
