// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's escrowed inner-value vectors,
/// <c>tests/vectors/cxx/methods/finitedifferences/utilities/fdmescrowedloginnervaluecalculator.json</c>:
/// <see cref="FdmEscrowedLogInnerValueCalculator"/>'s inner value and
/// <see cref="FdmEscrowedLogInnerValueCalculator.AvgInnerValue"/> at every point of a uniform log mesher, at four
/// times, over three dividend schedules.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmEscrowedLogInnerValueCalculatorCxxVectorTests
{
    [Fact]
    public void FdmEscrowedLogInnerValueCalculatorCxxVector()
    {
        var file = VectorFile.Load("cxx/methods/finitedifferences/utilities/fdmescrowedloginnervaluecalculator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Date today = context.EvaluationDate;
            JsonElement spec = c.Input("calculator");
            double Get(string name) => spec.GetProperty(name).GetDouble();
            DayCounter dc = new Actual365Fixed();
            ImmutableArray<Dividend> dividends =
            [
                .. spec.GetProperty("dividends").EnumerateArray()
                    .Select(d => (Dividend)new FixedDividend(d[1].GetDouble(), new Date(d[0].GetInt32()))),
            ];
            var adjustment = new EscrowedDividendAdjustment(
                dividends,
                new FlatForward(today, Get("riskFreeRate"), dc),
                new FlatForward(today, Get("dividendYield"), dc),
                d => dc.YearFraction(today, d),
                Get("maturity"),
                context);
            var mesher = new FdmMesherComposite(new Uniform1dMesher(Get("xMin"), Get("xMax"), spec.GetProperty("size").GetInt32()));
            var calculator = new FdmEscrowedLogInnerValueCalculator(
                adjustment, new PlainVanillaPayoff((OptionType)spec.GetProperty("optionType").GetInt32(), Get("strike")), mesher, 0);
            double[] times = c.Doubles("times");
            check.Output(c, "innerValue", times.Select(t => (object?)AtEachPoint(mesher, it => calculator.InnerValue(it, t))).ToArray());
            check.Output(c, "avgInnerValue", times.Select(t => (object?)AtEachPoint(mesher, it => calculator.AvgInnerValue(it, t))).ToArray());
        }

        check.Complete();
    }

    private static double[] AtEachPoint(FdmMesher mesher, Func<FdmLinearOpIterator, double> f)
    {
        FdmLinearOpLayout layout = mesher.Layout;
        double[] values = new double[layout.Size];
        FdmLinearOpIterator iter = layout.Begin();
        for (int i = 0; i < layout.Size; ++i, iter.Increment())
        {
            values[i] = f(iter);
        }

        return values;
    }
}
