// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The LMMDriftCalculator vectors, <c>tests/vectors/models/marketmodels/driftcomputation/lmmdriftcalculator.json</c>:
/// <see cref="LMMDriftCalculator"/>'s drifts by each form on forwards and on a curve state, full-factor and reduced,
/// displaced and not, at several numeraires and first alive rates; and the constructor's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LMMDriftCalculatorVectorTests
{
    [Fact]
    public void LMMDriftCalculatorVector()
    {
        var file = VectorFile.Load("models/marketmodels/driftcomputation/lmmdriftcalculator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Matrix pseudo = MatrixOf(c.Input("pseudo"));
            double[] times = c.Doubles("rateTimes"), taus = c.Doubles("taus");
            int n = taus.Length;
            if (c.String("call") == "compute")
            {
                double[] d = c.Doubles("displacements"), fwds = c.Doubles("forwards");
                var calculator = new LMMDriftCalculator(pseudo, d, taus, c.Int("numeraire"), c.Int("alive"));
                check.Output(c, "compute", Drifts(n, x => calculator.Compute(fwds, x)));
                check.Output(c, "computePlain", Drifts(n, x => calculator.ComputePlain(fwds, x)));
                check.Output(c, "computeReduced", Drifts(n, x => calculator.ComputeReduced(fwds, x)));
                var cs = new LMMCurveState(times);
                cs.SetOnForwardRates(fwds);
                check.Output(c, "computeState", Drifts(n, x => calculator.Compute(cs, x)));
                check.Output(c, "computeAgain", Drifts(n, x => calculator.Compute(fwds, x)));
                continue;
            }

            double[] zeros = new double[n];
            check.Output(c, "noRates", Lazy(() => new LMMDriftCalculator(pseudo, zeros, [], n, 0)));
            check.Output(c, "displacementsSize", Lazy(() => new LMMDriftCalculator(pseudo, zeros[..^1], taus, n, 0)));
            check.Output(c, "pseudoRows", Lazy(() => new LMMDriftCalculator(Shorter(pseudo), zeros, taus, n, 0)));
            check.Output(c, "aliveOut", Lazy(() => new LMMDriftCalculator(pseudo, zeros, taus, n, n)));
            check.Output(c, "numerairePast", Lazy(() => new LMMDriftCalculator(pseudo, zeros, taus, n + 1, 0)));
            check.Output(c, "numeraireBelowAlive", Lazy(() => new LMMDriftCalculator(pseudo, zeros, taus, 1, 2)));
        }

        check.Complete();
    }

    // A pseudo-root without its last row.
    internal static Matrix Shorter(Matrix m)
    {
        var r = new Matrix(m.Rows - 1, m.Columns);
        for (int i = 0; i < r.Rows; ++i)
        {
            m.Row(i).CopyTo(r.Row(i));
        }

        return r;
    }

    // The drifts written over zeros.
    internal static double[] Drifts(int n, Action<double[]> compute)
    {
        double[] drifts = new double[n];
        compute(drifts);
        return drifts;
    }
}
