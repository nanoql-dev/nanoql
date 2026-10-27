// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.LMMDriftCalculatorVectorTests;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's constant-maturity swap drift vectors,
/// <c>tests/vectors/cxx/models/marketmodels/driftcomputation/cmsmmdriftcalculator.json</c>:
/// <see cref="CMSMMDriftCalculator"/>'s drifts of a constant-maturity swap curve state at several spannings, twice on
/// one calculator, and its refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CMSMMDriftCalculatorCxxVectorTests
{
    [Fact]
    public void CMSMMDriftCalculatorCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/driftcomputation/cmsmmdriftcalculator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Matrix pseudo = MatrixOf(c.Input("pseudo"));
            double[] times = c.Doubles("rateTimes"), rates = c.Doubles("swapRates");
            double[] taus = [.. UtilitiesFunctions.CheckIncreasingTimesAndCalculateTaus(times)];
            int n = taus.Length;
            if (c.String("call") == "compute")
            {
                int span = c.Int("spanning");
                var cs = new CMSwapCurveState(times, span);
                cs.SetOnCMSwapRates(rates);
                var calculator = new CMSMMDriftCalculator(pseudo, c.Doubles("displacements"), taus, c.Int("numeraire"), c.Int("alive"), span);
                check.Output(c, "drifts", Drifts(n, x => calculator.Compute(cs, x)));
                check.Output(c, "again", Drifts(n, x => calculator.Compute(cs, x)));
                continue;
            }

            double[] z = new double[n];
            check.Output(c, "displacementsSize", Lazy(() => new CMSMMDriftCalculator(pseudo, z[..^1], taus, n, 0, 1)));
            check.Output(c, "pseudoRows", Lazy(() => new CMSMMDriftCalculator(Shorter(pseudo), z, taus, n, 0, 1)));
            check.Output(c, "aliveOut", Lazy(() => new CMSMMDriftCalculator(pseudo, z, taus, n, n, 1)));
            check.Output(c, "numerairePast", Lazy(() => new CMSMMDriftCalculator(pseudo, z, taus, n + 1, 0, 1)));
            check.Output(c, "numeraireBelowAlive", Lazy(() => new CMSMMDriftCalculator(pseudo, z, taus, 1, 2, 1)));
        }

        check.Complete();
    }
}
