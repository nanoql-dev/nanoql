// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's shout inner value vectors,
/// <c>tests/vectors/cxx/methods/finitedifferences/utilities/fdmshoutloginnervaluecalculator.json</c>:
/// <see cref="FdmShoutLogInnerValueCalculator"/>'s inner and average inner values at every point of the shout engine's
/// mesher, at times from today to maturity, for calls and puts without dividends and with a dividend paid before the
/// times read.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmShoutLogInnerValueCalculatorCxxVectorTests
{
    [Fact]
    public void FdmShoutLogInnerValueCalculatorCxxVector()
    {
        var file = VectorFile.Load("cxx/methods/finitedifferences/utilities/fdmshoutloginnervaluecalculator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var today = new Date(c.Int("today"));
            DayCounter dc = new Actual365Fixed();
            var process = new BlackScholesMertonProcess(
                c.Double("spot"),
                new FlatForward(today, c.Double("q"), dc),
                new FlatForward(today, c.Double("r"), dc),
                new BlackConstantVol(today, new NullCalendar(), c.Double("vol"), dc));
            ImmutableArray<Dividend> dividends = DividendFunctions.DividendVector(
                c.Input("dividends").EnumerateArray().Select(d => new Date(d[0].GetInt32())),
                c.Input("dividends").EnumerateArray().Select(d => d[1].GetDouble()));
            double maturity = c.Double("maturity");
            var payoff = new PlainVanillaPayoff((OptionType)c.Input("payoff")[0].GetInt32(), c.Input("payoff")[1].GetDouble());
            TermStructure clock = FdmBlackScholesOp.Clock(process);
            var adjustment = new EscrowedDividendAdjustment(
                dividends, process.RiskFreeRate, process.DividendYield, clock.TimeFromReference, maturity, context, clock);
            double divAdj = adjustment.DividendAdjustment(clock.TimeFromReference(today));
            var mesher = new FdmMesherComposite(new FdmBlackScholesMesher(
                c.Int("xGrid"), process, maturity, payoff.Strike, null, null, 0.0001, 1.5, (payoff.Strike, 0.1), [], null, divAdj, context));
            var calculator = new FdmShoutLogInnerValueCalculator(process.BlackVolatility, adjustment, maturity, payoff, mesher, 0);
            double[] times = c.Doubles("times");
            check.Output(c, "innerValue", times.Select(t => (object?)Values(mesher, i => calculator.InnerValue(i, t))).ToArray());
            check.Output(c, "avgInnerValue", times.Select(t => (object?)Values(mesher, i => calculator.AvgInnerValue(i, t))).ToArray());
        }

        check.Complete();
    }

    private static double[] Values(FdmMesher mesher, Func<FdmLinearOpIterator, double> f)
    {
        double[] values = new double[mesher.Layout.Size];
        FdmLinearOpIterator iter = mesher.Layout.Begin();
        for (int i = 0; i < values.Length; ++i, iter.Increment())
        {
            values[iter.Index] = f(iter);
        }

        return values;
    }
}
