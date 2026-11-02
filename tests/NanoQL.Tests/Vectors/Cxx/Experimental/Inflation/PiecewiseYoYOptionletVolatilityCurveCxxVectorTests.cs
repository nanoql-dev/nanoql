// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's piecewise year-on-year optionlet volatility curve vectors,
/// <c>tests/vectors/cxx/experimental/inflation/piecewiseyoyoptionletvolatility.json</c>: the curves the interpolated
/// stripper bootstraps at each strike of QuantLib's euro price surfaces, their dates, base level, times, data, and
/// volatilities at their strikes through their own date route.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PiecewiseYoYOptionletVolatilityCurveCxxVectorTests
{
    [Fact]
    public void PiecewiseYoYOptionletVolatilityCurveCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/inflation/piecewiseyoyoptionletvolatility");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            (var nominal, var index) = InflationSurfaceInputs.EuroMarketOf(c.Input("market"));
            var surf = InflationSurfaceInputs.YoYSurfaceOf(c.Input("surface"), nominal, index, context);
            var k = KInterpolatedYoYInflationOptionletVolatilitySurface.Create(
                0,
                new TARGET(),
                BusinessDayConvention.ModifiedFollowing,
                new Actual365Fixed(),
                surf.ObservationLag,
                surf,
                (cf, ctx, v) => YoYInflationUnitDisplacedBlackCapFloorEngine.Calculate(cf, ctx, index, v, nominal),
                new InterpolatedYoYInflationOptionletStripper(),
                c.Input("slope").GetDouble(),
                context);
            var curves = k.Stripped.Curves
                .Select(x => (PiecewiseYoYOptionletVolatilityCurve<Linear, IterativeBootstrap, YoYInflationVolatilityTraits>)x)
                .ToArray();
            Date[] dates = c.DateInputs("dates");
            check.Output(c, "referenceDate", curves.Select(x => (object?)x.ReferenceDate).ToArray());
            check.Output(c, "baseDate", curves.Select(x => (object?)x.BaseDate()).ToArray());
            check.Output(c, "baseLevel", curves.Select(x => (object?)x.BaseLevel()).ToArray());
            check.Output(c, "maxDate", curves.Select(x => (object?)x.MaxDate).ToArray());
            check.Output(c, "times", curves.Select(x => (object?)x.Times()).ToArray());
            check.Output(c, "dates", curves.Select(x => (object?)x.Dates().Select(d => (object?)d).ToArray()).ToArray());
            check.Output(c, "data", curves.Select(x => (object?)x.Data()).ToArray());
            check.Output(c, "volatility", curves.Select((x, i) => (object?)dates.Select(d => (Func<object?>)(() =>
                x.Volatility(d, k.Stripped.Strikes[i], null, true))).ToArray()).ToArray());
        }

        check.Complete();
    }
}
