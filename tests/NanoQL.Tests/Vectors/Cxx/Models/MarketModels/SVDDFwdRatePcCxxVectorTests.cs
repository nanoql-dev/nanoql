// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's SVD-DD evolver vectors, <c>tests/vectors/cxx/models/marketmodels/evolvers/svddfwdratepc.json</c>:
/// <see cref="SVDDFwdRatePc"/>'s whole evolutions on a <see cref="SquareRootAndersen"/> whose first step's e^{−κΔt}
/// underflows to 0, QuantLib's own value, of one and two sub-steps, its variates from the first, the second and past
/// the forwards' factors, on the Mersenne-twister and Sobol generators, under the terminal and money-market measures,
/// with the model's and a set initial state: each step's weight, current step, forwards, discount ratios and
/// coterminal swap rates; and the refused numeraires.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SVDDFwdRatePcCxxVectorTests
{
    [Fact]
    public void SVDDFwdRatePcCxxVector() =>
        LogNormalFwdRateEulerCxxVectorTests.CheckEvolutions(
            "cxx/models/marketmodels/evolvers/svddfwdratepc",
            (c, m, f, numeraires, initial) => new SVDDFwdRatePc(
                m,
                f,
                SquareRootAndersenOf(c.Input("volProcess")),
                c.Int("firstVolatilityFactor"),
                c.Int("volatilityFactorStep"),
                numeraires,
                initial));
}
