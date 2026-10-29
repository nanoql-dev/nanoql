// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The wheel's interpolated year-on-year optionlet stripper vectors,
/// <c>tests/vectors/experimental/inflation/interpolatedyoyoptionletstripper.json</c>: QuantLib's euro price surfaces
/// stripped with the unit-displaced Black engine through the k-interpolated surface, the stripped volatilities at each
/// strike at dates (its <see cref="KInterpolatedYoYOptionletVolatilitySurface{TInterpolator}.Dslice"/>), and the
/// surface's strikes and last date.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InterpolatedYoYOptionletStripperVectorTests
{
    [Fact]
    public void InterpolatedYoYOptionletStripperVector()
    {
        var file = VectorFile.Load("experimental/inflation/interpolatedyoyoptionletstripper");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            (var nominal, var index) = InflationSurfaceInputs.EuroMarketOf(c.Input("market"));
            var surf = InflationSurfaceInputs.YoYSurfaceOf(c.Input("surface"), nominal, index, context);
            var k = KInterpolatedYoYOptionletVolatilitySurface<Linear>.Create(
                0,
                new TARGET(),
                BusinessDayConvention.ModifiedFollowing,
                new Actual365Fixed(),
                surf.ObservationLag,
                surf,
                (cf, ctx, v) => YoYInflationUnitDisplacedBlackCapFloorEngine.Calculate(cf, ctx, index, v, nominal),
                new InterpolatedYoYOptionletStripper<Linear>(),
                c.Input("slope").GetDouble(),
                context);
            check.Output(c, "minStrike", k.MinStrike());
            check.Output(c, "maxStrike", k.MaxStrike());
            check.Output(c, "maxDate", k.MaxDate);
            check.Output(c, "dslice", c.DateInputs("dates").Select(d => (Func<object?>)(() =>
            {
                var slice = k.Dslice(d);
                return new object?[] { slice.Strikes, slice.Volatilities };
            })).ToArray());
        }

        check.Complete();
    }
}
