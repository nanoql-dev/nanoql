// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's curve-state conversion vectors, <c>tests/vectors/cxx/models/marketmodels/curvestate.json</c>:
/// <see cref="CurveStateFunctions"/>' forwards, coterminal and constant-maturity swaps from discount ratios, and their
/// size checks.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CurveStateCxxVectorTests
{
    private const double Mark = -7.0;

    [Fact]
    public void CurveStateCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/curvestate");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] ds = c.Doubles("ds"), taus = c.Doubles("taus");
            int n = taus.Length;
            switch (c.String("call"))
            {
                case "forwards":
                {
                    double[] fwds = Marked(n);
                    CurveStateFunctions.ForwardsFromDiscountRatios(c.Int("first"), ds, taus, fwds);
                    check.Output(c, "fwds", fwds);
                    break;
                }

                case "coterminal":
                {
                    double[] rates = Marked(n), annuities = Marked(n);
                    CurveStateFunctions.CoterminalFromDiscountRatios(c.Int("first"), ds, taus, rates, annuities);
                    check.Output(c, "rates", rates);
                    check.Output(c, "annuities", annuities);
                    break;
                }

                case "constantMaturity":
                {
                    double[] rates = Marked(n), annuities = Marked(n);
                    CurveStateFunctions.ConstantMaturityFromDiscountRatios(c.Int("span"), c.Int("first"), ds, taus, rates, annuities);
                    check.Output(c, "rates", rates);
                    check.Output(c, "annuities", annuities);
                    break;
                }

                default:
                    check.Output(c, "forwardsTaus", Lazy(() => Run(() => CurveStateFunctions.ForwardsFromDiscountRatios(0, ds, taus[..^1], Marked(n)))));
                    check.Output(c, "forwardsDs", Lazy(() => Run(() => CurveStateFunctions.ForwardsFromDiscountRatios(0, ds[..^1], taus, Marked(n)))));
                    check.Output(c, "coterminalTaus", Lazy(() => Run(() => CurveStateFunctions.CoterminalFromDiscountRatios(0, ds, taus[..^1], Marked(n), Marked(n)))));
                    check.Output(c, "coterminalAnnuities", Lazy(() => Run(() => CurveStateFunctions.CoterminalFromDiscountRatios(0, ds, taus, Marked(n), Marked(n + 1)))));
                    check.Output(c, "coterminalDs", Lazy(() => Run(() => CurveStateFunctions.CoterminalFromDiscountRatios(0, [.. ds, 1.0], taus, Marked(n), Marked(n)))));
                    check.Output(c, "constantMaturityTaus", Lazy(() => Run(() => CurveStateFunctions.ConstantMaturityFromDiscountRatios(1, 0, ds, taus[..^1], Marked(n), Marked(n)))));
                    check.Output(c, "constantMaturityAnnuities", Lazy(() => Run(() => CurveStateFunctions.ConstantMaturityFromDiscountRatios(1, 0, ds, taus, Marked(n), Marked(n + 1)))));
                    check.Output(c, "constantMaturityDs", Lazy(() => Run(() => CurveStateFunctions.ConstantMaturityFromDiscountRatios(1, 0, ds[..^1], taus, Marked(n), Marked(n)))));
                    break;
            }
        }

        check.Complete();
    }

    private static double[] Marked(int n)
    {
        double[] x = new double[n];
        Array.Fill(x, Mark);
        return x;
    }

    private static object? Run(Action a)
    {
        a();
        return null;
    }
}
