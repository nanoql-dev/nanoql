// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.LMMDriftCalculatorVectorTests;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's coterminal swap drift vectors,
/// <c>tests/vectors/cxx/models/marketmodels/driftcomputation/smmdriftcalculator.json</c>:
/// <see cref="SMMDriftCalculator"/>'s drifts of a coterminal swap curve state, twice on one calculator, and its
/// refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SMMDriftCalculatorCxxVectorTests
{
    [Fact]
    public void SMMDriftCalculatorCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/driftcomputation/smmdriftcalculator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Matrix pseudo = MatrixOf(c.Input("pseudo"));
            double[] times = c.Doubles("rateTimes"), rates = c.Doubles("swapRates");
            double[] taus = [.. UtilitiesFunctions.CheckIncreasingTimesAndCalculateTaus(times)];
            int n = taus.Length;
            var cs = new CoterminalSwapCurveState(times);
            cs.SetOnCoterminalSwapRates(rates);
            if (c.String("call") == "compute")
            {
                var calculator = new SMMDriftCalculator(pseudo, c.Doubles("displacements"), taus, c.Int("numeraire"), c.Int("alive"));
                check.Output(c, "drifts", Drifts(n, x => calculator.Compute(cs, x)));
                check.Output(c, "again", Drifts(n, x => calculator.Compute(cs, x)));
                continue;
            }

            double[] z = new double[n];
            check.Output(c, "displacementsSize", Lazy(() => new SMMDriftCalculator(pseudo, z[..^1], taus, n, 0)));
            check.Output(c, "pseudoRows", Lazy(() => new SMMDriftCalculator(Shorter(pseudo), z, taus, n, 0)));
            check.Output(c, "aliveOut", Lazy(() => new SMMDriftCalculator(pseudo, z, taus, n, n)));
            check.Output(c, "numerairePast", Lazy(() => new SMMDriftCalculator(pseudo, z, taus, n + 1, 0)));
            check.Output(c, "numeraireBelowAlive", Lazy(() => new SMMDriftCalculator(pseudo, z, taus, 1, 2)));
        }

        check.Complete();
    }
}
