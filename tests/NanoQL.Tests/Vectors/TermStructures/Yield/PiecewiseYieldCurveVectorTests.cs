// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The piecewise yield curve vectors, <c>tests/vectors/termstructures/yield/piecewiseyieldcurve.json</c>:
/// <see cref="PiecewiseYieldCurve{TTraits, TInterpolator, TBootstrap}"/> on <see cref="IterativeBootstrap"/>, with the
/// four traits and their interpolators, on deposit and swap helpers, built in each case's context
/// (<see cref="BootstrapInputs"/>): whether each curve builds, its nodes, maximum date, discounts and the helpers'
/// quote errors. Two kinds of cases replace QuantLib's testObservability: curves rebuilt after a bumped quote, read at
/// the unbumped pillar's time, and curves built in a context later than the one their helpers were built in, which
/// re-date the helpers as QuantLib's helpers re-date themselves. A second test builds the linear curves on the
/// fixture's helpers again on a factory of one's own.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PiecewiseYieldCurveVectorTests
{
    [Fact]
    public void PiecewiseYieldCurveVector()
    {
        var file = VectorFile.Load("termstructures/yield/piecewiseyieldcurve");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            switch (c.String("kind"))
            {
                case "curves":
                    BootstrapInputs.CheckCurves(
                        check, c, RateHelperInputs.HelpersOf(c.Input("helpers"), context), context);
                    break;
                case "moved":
                    EvaluationContext earlier = context with { EvaluationDate = c.DateInput("helpersEvaluationDate") };
                    BootstrapInputs.CheckCurves(
                        check, c, RateHelperInputs.HelpersOf(c.Input("helpers"), earlier), context);
                    break;
                case "bumped":
                    CheckBumped(check, c, context);
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    [Fact]
    public void PiecewiseYieldCurveVectorOnOwnInterpolator()
    {
        // A factory of one's own that keeps IInterpolator's default CheckPoints, which refuses nothing, takes
        // QuantLib's results wherever no construction check would refuse: the linear zero, forward and simple-zero
        // curves on the fixture's helpers, on the three evaluation dates and with jumps (local interpolations, which no
        // point count refuses), built on BootstrapInputs.OwnLinear, compare with the same outputs as on Linear.
        var file = VectorFile.Load("termstructures/yield/piecewiseyieldcurve");
        var check = new VectorAssert(file);
        VectorCase[] fixture =
        [
            .. file.Cases.Where(c =>
                c.String("kind") == "curves" && c.Name.EndsWith(" fixture") && !c.Name.Contains("cubic")),
        ];
        Assert.Equal(4, fixture.Length);
        foreach (VectorCase c in fixture)
        {
            EvaluationContext context = c.Context();
            BootstrapInputs.CheckCurves(
                check, c, RateHelperInputs.HelpersOf(c.Input("helpers"), context), context, ownLinear: true);
        }

        Assert.Empty(check.Mismatches);
        Assert.Equal(fixture.Sum(c => c.Expected.EnumerateObject().Sum(o => Count(o.Value))), check.Compared);
    }

    private static int Count(JsonElement expected) =>
        expected.ValueKind == JsonValueKind.Array ? expected.EnumerateArray().Sum(Count) : 1;

    private static void CheckBumped(VectorAssert check, VectorCase c, EvaluationContext context)
    {
        BootstrapHelper<YieldTermStructure>[] helpers = RateHelperInputs.HelpersOf(c.Input("helpers"), context);
        BootstrapInputs.Built? curve = null;
        check.Output(c, "built", () =>
        {
            curve = BootstrapInputs.CurveOf(c.Input("curve"), helpers, context);
            return true;
        });
        check.Output(c, "dates", curve?.Dates.Cast<object?>().ToArray() ?? []);
        check.Output(c, "data", curve?.Data.Cast<object?>().ToArray() ?? []);
        check.Output(c, "discountAtTestTime", () => curve!.Curve.Discount(c.Double("testTime"), true));
    }
}
