// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's normal predictor-corrector evolver vectors,
/// <c>tests/vectors/cxx/models/marketmodels/evolvers/normalfwdratepc.json</c>: <see cref="NormalFwdRatePc"/>'s whole
/// evolutions on models of normal volatilities, on the Mersenne-twister and Sobol generators, under the terminal and
/// money-market measures, from the first and a later step, with the model's initial state (a set one is in the
/// Divergences record): each step's weight, current step, forwards, discount ratios and coterminal swap rates; and
/// the refused numeraires.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class NormalFwdRatePcCxxVectorTests
{
    [Fact]
    public void NormalFwdRatePcCxxVector() =>
        LogNormalFwdRateEulerCxxVectorTests.CheckEvolutions(
            "cxx/models/marketmodels/evolvers/normalfwdratepc",
            (_, m, f, numeraires, initial) => new NormalFwdRatePc(m, f, numeraires, initial));
}
