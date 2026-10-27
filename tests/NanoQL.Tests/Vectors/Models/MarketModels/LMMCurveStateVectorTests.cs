// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The LMMCurveState vectors, <c>tests/vectors/models/marketmodels/curvestates/lmmcurvestate.json</c>:
/// <see cref="LMMCurveState"/>'s reads on states set on forwards and on discount ratios from several first valid
/// indexes, <see cref="CurveState.SwapRate"/>, and the refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LMMCurveStateVectorTests
{
    [Fact]
    public void LMMCurveStateVector()
    {
        var file = VectorFile.Load("models/marketmodels/curvestates/lmmcurvestate");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] times = c.Doubles("rateTimes");
            switch (c.String("call"))
            {
                case "reads":
                {
                    double[] values = c.Doubles("values");
                    int first = c.Int("first");
                    int[] spans = c.Ints("spans");
                    var cs = new LMMCurveState(times);
                    if (c.String("kind") == "forwards")
                    {
                        cs.SetOnForwardRates(values, first);
                    }
                    else
                    {
                        cs.SetOnDiscountRatios(values, first);
                    }

                    int n = cs.NumberOfRates;
                    IEnumerable<int> rates = Enumerable.Range(first, n - first), bonds = Enumerable.Range(first, n - first + 1);
                    check.Output(c, "discountRatios", bonds.Select(i => bonds.Select(j => cs.DiscountRatio(i, j)).ToArray()).ToArray());
                    check.Output(c, "forwardRates", rates.Select(cs.ForwardRate).ToArray());
                    check.Output(c, "coterminalSwapRates", rates.Select(cs.CoterminalSwapRate).ToArray());
                    check.Output(
                        c, "coterminalSwapAnnuities", bonds.Select(k => rates.Select(i => cs.CoterminalSwapAnnuity(k, i)).ToArray()).ToArray());
                    check.Output(c, "cmSwapRates", spans.Select(s => rates.Select(i => cs.CmSwapRate(i, s)).ToArray()).ToArray());
                    check.Output(c, "cmSwapAnnuities", spans.Select(s => rates.Select(i => cs.CmSwapAnnuity(n, i, s)).ToArray()).ToArray());
                    check.Output(c, "forwardRatesVector", cs.ForwardRates()[first..].ToArray());
                    check.Output(c, "coterminalSwapRatesVector", cs.CoterminalSwapRates()[first..].ToArray());
                    check.Output(c, "cmSwapRatesVectors", spans.Select(s => cs.CmSwapRates(s)[first..].ToArray()).ToArray());
                    check.Output(
                        c, "swapRates", rates.Select(b => Enumerable.Range(b + 1, n - b).Select(e => cs.SwapRate(b, e)).ToArray()).ToArray());
                    check.Output(c, "numberOfRates", n);
                    check.Output(c, "rateTimes", cs.RateTimes);
                    check.Output(c, "rateTaus", cs.RateTaus);
                    break;
                }

                case "below":
                {
                    var cs = new LMMCurveState(times);
                    cs.SetOnForwardRates(c.Doubles("values"), 1);
                    int n = cs.NumberOfRates;
                    check.Output(c, "forwardRate", Lazy(() => cs.ForwardRate(0)));
                    check.Output(c, "discountRatio", Lazy(() => cs.DiscountRatio(0, n)));
                    check.Output(c, "coterminalSwapAnnuity", Lazy(() => cs.CoterminalSwapAnnuity(0, 1)));
                    check.Output(c, "cmSwapRate", Lazy(() => cs.CmSwapRate(0, 1)));
                    break;
                }

                case "unset":
                {
                    var cs = new LMMCurveState(times);
                    check.Output(c, "forwardRate", Lazy(() => cs.ForwardRate(0)));
                    check.Output(c, "forwardRates", Lazy(() => cs.ForwardRates().ToArray()));
                    break;
                }

                case "badSets":
                {
                    double[] fwds = c.Doubles("values"), ratios = c.Doubles("ratios");
                    int n = fwds.Length;
                    check.Output(c, "shortForwards", Lazy(() => Set(times, fwds[..^1], 0, false)));
                    check.Output(c, "forwardsPastIndex", Lazy(() => Set(times, fwds, n, false)));
                    check.Output(c, "shortRatios", Lazy(() => Set(times, ratios[..^1], 0, true)));
                    check.Output(c, "ratiosPastIndex", Lazy(() => Set(times, ratios, n, true)));
                    break;
                }

                default:
                    check.Output(c, "construction", Lazy(() => new LMMCurveState(times)));
                    break;
            }
        }

        check.Complete();
    }

    private static LMMCurveState Set(double[] times, double[] values, int first, bool onRatios)
    {
        var cs = new LMMCurveState(times);
        if (onRatios)
        {
            cs.SetOnDiscountRatios(values, first);
        }
        else
        {
            cs.SetOnForwardRates(values, first);
        }

        return cs;
    }
}
