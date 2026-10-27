// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.LMMDriftCalculatorVectorTests;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's normal LIBOR drift vectors,
/// <c>tests/vectors/cxx/models/marketmodels/driftcomputation/lmmnormaldriftcalculator.json</c>:
/// <see cref="LMMNormalDriftCalculator"/>'s drifts by each form, twice on one calculator, and its refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LMMNormalDriftCalculatorCxxVectorTests
{
    [Fact]
    public void LMMNormalDriftCalculatorCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/driftcomputation/lmmnormaldriftcalculator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Matrix pseudo = MatrixOf(c.Input("pseudo"));
            double[] taus = c.Doubles("taus");
            int n = taus.Length;
            if (c.String("call") == "compute")
            {
                double[] fwds = c.Doubles("forwards");
                int numeraire = c.Int("numeraire"), alive = c.Int("alive");
                foreach ((string form, Action<LMMNormalDriftCalculator, double[]> compute) in new (string, Action<LMMNormalDriftCalculator, double[]>)[]
                {
                    ("compute", (k, x) => k.Compute(fwds, x)),
                    ("computePlain", (k, x) => k.ComputePlain(fwds, x)),
                    ("computeReduced", (k, x) => k.ComputeReduced(fwds, x)),
                })
                {
                    var calculator = new LMMNormalDriftCalculator(pseudo, taus, numeraire, alive);
                    check.Output(c, form, Drifts(n, x => compute(calculator, x)));
                    check.Output(c, form + "Again", Drifts(n, x => compute(calculator, x)));
                }

                continue;
            }

            check.Output(c, "noRates", Lazy(() => new LMMNormalDriftCalculator(pseudo, [], n, 0)));
            check.Output(c, "pseudoRows", Lazy(() => new LMMNormalDriftCalculator(Shorter(pseudo), taus, n, 0)));
            check.Output(c, "aliveOut", Lazy(() => new LMMNormalDriftCalculator(pseudo, taus, n, n)));
            check.Output(c, "numerairePast", Lazy(() => new LMMNormalDriftCalculator(pseudo, taus, n + 1, 0)));
            check.Output(c, "numeraireBelowAlive", Lazy(() => new LMMNormalDriftCalculator(pseudo, taus, 1, 2)));
        }

        check.Complete();
    }
}
