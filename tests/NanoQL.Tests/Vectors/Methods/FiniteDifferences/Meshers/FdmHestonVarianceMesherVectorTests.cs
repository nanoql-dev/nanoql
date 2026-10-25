// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmHestonVarianceMesher vectors,
/// <c>tests/vectors/methods/finitedifferences/meshers/fdmhestonvariancemesher.json</c>: both variance meshers'
/// locations, dplus, dminus and volatility estimate on three Heston parameter sets at three sizes and maturities, other
/// averaging steps and epsilons, and the local volatility mesher under two leverage functions.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmHestonVarianceMesherVectorTests
{
    [Fact]
    public void FdmHestonVarianceMesherVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/meshers/fdmhestonvariancemesher");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            HestonProcess process = HestonProcessVectorTests.HestonProcessOf(c.Input("process"));
            int size = c.Int("size");
            double maturity = c.Double("maturity");
            int tAvgSteps = c.Int("tAvgSteps");
            double epsilon = c.Double("epsilon");
            (Fdm1dMesher mesher, double volaEstimate) = c.Inputs.TryGetProperty("leverage", out JsonElement leverage)
                ? Local(new FdmHestonLocalVolatilityVarianceMesher(
                    size, process, FdHestonVanillaEngineVectorTests.LeverageOf(leverage), maturity, tAvgSteps, epsilon))
                : Plain(new FdmHestonVarianceMesher(size, process, maturity, tAvgSteps, epsilon));
            (double[] locations, double?[] dplus, double?[] dminus) = Read(mesher);
            check.Output(c, "locations", locations);
            check.Output(c, "dplus", dplus);
            check.Output(c, "dminus", dminus);
            check.Output(c, "volaEstimate", volaEstimate);
        }

        check.Complete();
    }

    private static (Fdm1dMesher, double) Plain(FdmHestonVarianceMesher m) => (m, m.VolaEstimate);

    private static (Fdm1dMesher, double) Local(FdmHestonLocalVolatilityVarianceMesher m) => (m, m.VolaEstimate);
}
