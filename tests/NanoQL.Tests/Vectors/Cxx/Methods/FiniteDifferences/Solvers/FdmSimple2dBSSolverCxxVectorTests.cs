// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's two-dimensional Black-Scholes solver vectors,
/// <c>tests/vectors/cxx/methods/finitedifferences/solvers/fdmsimple2dbssolver.json</c>:
/// <see cref="FdmSimple2dBSSolver"/>'s value, delta, gamma and theta at spots and rights' coordinates on the problem the
/// swing engine builds, for forward and plain-vanilla payoffs, exercise times, rights and grids.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmSimple2dBSSolverCxxVectorTests
{
    [Fact]
    public void FdmSimple2dBSSolverCxxVector()
    {
        var file = VectorFile.Load("cxx/methods/finitedifferences/solvers/fdmsimple2dbssolver");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var today = new Date(c.Int("today"));
            DayCounter dc = new Actual365Fixed();
            var process = new BlackScholesMertonProcess(
                c.Double("spot"),
                new FlatForward(today, c.Double("q"), dc),
                new FlatForward(today, c.Double("r"), dc),
                new BlackConstantVol(today, new NullCalendar(), c.Double("vol"), dc));
            JsonElement p = c.Input("payoff");
            var type = (OptionType)p[1].GetInt32();
            StrikedTypePayoff payoff = p[0].GetString() == "Forward"
                ? new VanillaForwardPayoff(type, p[2].GetDouble())
                : new PlainVanillaPayoff(type, p[2].GetDouble());
            double[] times = c.Doubles("exerciseTimes");
            int maximum = c.Int("maxExercises");
            double maturity = times[^1];
            var mesher = new FdmMesherComposite(
                new FdmBlackScholesMesher(c.Int("xGrid"), process, maturity, payoff.Strike, context: c.Context()),
                new Uniform1dMesher(0, maximum, maximum + 1));
            var conditions = new FdmStepConditionComposite(
                [times], [new FdmSimpleSwingCondition(times, mesher, new FdmLogInnerValue(payoff, mesher, 0), 1, c.Int("minExercises"))]);
            var desc = new FdmSolverDesc(mesher, [], conditions, new FdmZeroInnerValue(), maturity, c.Int("tGrid"), 0);
            var solver = new FdmSimple2dBSSolver(process, payoff.Strike, desc);
            (double S, double A)[] points = [.. c.Input("points").EnumerateArray().Select(x => (x[0].GetDouble(), x[1].GetDouble()))];
            double eps = c.Double("eps");
            check.Output(c, "valueAt", points.Select(x => (object?)solver.ValueAt(x.S, x.A)).ToArray());
            check.Output(c, "deltaAt", points.Select(x => (object?)solver.DeltaAt(x.S, x.A, eps)).ToArray());
            check.Output(c, "gammaAt", points.Select(x => (object?)solver.GammaAt(x.S, x.A, eps)).ToArray());
            check.Output(c, "thetaAt", points.Select(x => (object?)solver.ThetaAt(x.S, x.A)).ToArray());
        }

        check.Complete();
    }
}
