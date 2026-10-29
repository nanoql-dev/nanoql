// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The energy engines' wheel vectors, <c>tests/vectors/experimental/finitedifferences/fdsimpleextoujumpswingengine.json</c>:
/// <see cref="FdSimpleExtOUJumpSwingEngine"/>'s swing values on Kluge's spot, rights obliged and not, with and without a
/// seasonal shape; and <see cref="FdOrnsteinUhlenbeckVanillaEngine"/>'s European and American values, deltas, gammas
/// and thetas on an Ornstein-Uhlenbeck process, with a dividend.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdSimpleExtOUJumpSwingEngineVectorTests
{
    private static readonly Date Today = new(18, Month.December, 2011);
    private static readonly DayCounter Dc = new ActualActual(ActualActualConvention.ISDA);

    [Fact]
    public void FdSimpleExtOUJumpSwingEngineVector()
    {
        var file = VectorFile.Load("experimental/finitedifferences/fdsimpleextoujumpswingengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Input("kind").GetString() == "swing")
            {
                JsonElement s = c.Input("swing");
                JsonElement p = s.GetProperty("process"), j = s.GetProperty("jumps");
                double b = p.GetProperty("level").GetProperty("b").GetDouble();
                var process = new ExtOUWithJumpsProcess(
                    new ExtendedOrnsteinUhlenbeckProcess(p.GetProperty("speed").GetDouble(), p.GetProperty("sigma").GetDouble(), p.GetProperty("x0").GetDouble(), _ => b),
                    j.GetProperty("y0").GetDouble(),
                    j.GetProperty("beta").GetDouble(),
                    j.GetProperty("jumpIntensity").GetDouble(),
                    j.GetProperty("eta").GetDouble());
                List<Date> dates = [Today + new Period(1, TimeUnit.Months)];
                while (dates[^1] < Today + new Period(6, TimeUnit.Months))
                {
                    dates.Add(dates[^1] + new Period(1, TimeUnit.Months));
                }

                var option = new VanillaSwingOption(
                    new VanillaForwardPayoff((OptionType)s.GetProperty("type").GetInt32(), s.GetProperty("strike").GetDouble()),
                    new SwingExercise(dates),
                    s.GetProperty("minRights").GetInt32(),
                    s.GetProperty("maxRights").GetInt32());
                int[] grids = [.. s.GetProperty("grids").EnumerateArray().Select(g => g.GetInt32())];
                (double Time, double Value)[] shape = [.. s.GetProperty("shape").EnumerateArray().Select(e => (e[0].GetDouble(), e[1].GetDouble()))];
                check.Output(c, "npv", () => FdSimpleExtOUJumpSwingEngine.Calculate(
                    option, context, process, new FlatForward(Today, 0.1, Dc), grids[0], grids[1], grids[2], shape).NPV);
                continue;
            }

            JsonElement o = c.Input("ou");
            var ou = new OrnsteinUhlenbeckProcess(
                o.GetProperty("speed").GetDouble(), o.GetProperty("vol").GetDouble(), o.GetProperty("x0").GetDouble(), o.GetProperty("level").GetDouble());
            Date maturity = Today + new Period(1, TimeUnit.Years);
            Exercise exercise = o.GetProperty("exercise").GetString() == "European" ? new EuropeanExercise(maturity) : new AmericanExercise(Today, maturity);
            var vanilla = new VanillaOption(new PlainVanillaPayoff((OptionType)o.GetProperty("type").GetInt32(), o.GetProperty("strike").GetDouble()), exercise);
            ImmutableArray<Dividend> dividends = o.GetProperty("dividend").ValueKind == JsonValueKind.Null
                ? []
                : [new FixedDividend(o.GetProperty("dividend")[1].GetDouble(), Today + o.GetProperty("dividend")[0].GetInt32())];
            int[] g2 = [.. o.GetProperty("grids").EnumerateArray().Select(g => g.GetInt32())];
            OneAssetOptionResults r = FdOrnsteinUhlenbeckVanillaEngine.Calculate(vanilla, context, ou, new FlatForward(Today, 0.05, Dc), dividends, g2[0], g2[1]);
            check.Output(c, "npv", r.NPV);
            check.Output(c, "delta", r.Delta);
            check.Output(c, "gamma", r.Gamma);
            check.Output(c, "theta", r.Theta);
        }

        check.Complete();
    }
}
