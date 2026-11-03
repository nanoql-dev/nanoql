// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The fitted bond curve vectors, <c>tests/vectors/termstructures/yield/fittedbonddiscountcurve.json</c>: Nelson-Siegel
/// and Svensson evaluators on a fixed reference date and on settlement days; and fits of ten fixed-rate bonds by six
/// methods, their outputs kept where the wheel's refits on prices one ulp apart leave them stable: the curve's maximum
/// date and number of bonds, the method's weights, solution, iterations and cost, and discounts.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FittedBondDiscountCurveVectorTests
{
    [Fact]
    public void FittedBondDiscountCurveVector()
    {
        var file = VectorFile.Load("termstructures/yield/fittedbonddiscountcurve");
        var check = new VectorAssert(file);
        var a365 = new Actual365Fixed();
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            FittedBondDiscountCurveFittingMethod method = FittingInputs.MethodOf(c.Input("method"), context);
            if (c.String("kind") == "evaluator")
            {
                double[] parameters = c.Doubles("parameters");
                Date maxDate = c.DateInput("maxDate");
                FittedBondDiscountCurve curve = c.String("form") == "referenceDate"
                    ? new FittedBondDiscountCurve(context.EvaluationDate, method, parameters, maxDate, a365)
                    : FittedBondDiscountCurve.Create(
                        c.Int("settlementDays"), new TARGET(), method, parameters, maxDate, a365, context);
                check.Output(c, "referenceDate", curve.ReferenceDate.SerialNumber);
                check.Output(c, "maxDate", curve.MaxDate.SerialNumber);
                check.Output(c, "maxTime", curve.MaxTime);
                check.Output(c, "numberOfBonds", curve.NumberOfBonds);
                check.Output(c, "solution", curve.FitResults.Solution.Cast<object?>().ToArray());
                check.Output(c, "numberOfIterations", curve.FitResults.NumberOfIterations);
                check.Output(c, "minimumCostValue", curve.FitResults.MinimumCostValue);
                double[] times = c.Doubles("times");
                check.Output(c, "discount", times.Select(t => (Func<object?>)(() => curve.Discount(t))).ToArray());
            }
            else
            {
                var helpers = c.Input("bonds").EnumerateArray().Select(b => HelperOf(b, context)).ToList();
                var curve = new FittedBondDiscountCurve(
                    context.EvaluationDate, helpers, a365, method, context, 1e-10, 10000, c.Doubles("guess"), 1.0);
                FittedBondDiscountCurveFittingMethod results = curve.FitResults;
                Optional(check, c, "maxDate", curve.MaxDate.SerialNumber);
                Optional(check, c, "numberOfBonds", curve.NumberOfBonds);
                Optional(check, c, "numberOfIterations", results.NumberOfIterations);
                Optional(check, c, "minimumCostValue", results.MinimumCostValue);
                check.Output(c, "weights", c.Ints("weightIndices").Select(i => (object?)results.Weights[i]).ToArray());
                check.Output(c, "solution", c.Ints("solutionIndices").Select(i => (object?)results.Solution[i]).ToArray());
                check.Output(c, "discount", YieldCurveInputs.Each(c.DateInputs("dates"), d => curve.Discount(d)));
            }
        }

        check.Complete();
    }

    // A fit's output the stability filter kept.
    private static void Optional(VectorAssert check, VectorCase c, string output, object? actual)
    {
        if (c.Expected.TryGetProperty(output, out _))
        {
            check.Output(c, output, actual);
        }
    }

    // A bond of the generator's: semiannual on Actual/Actual (ISDA), two settlement days, its helper at its clean price.
    private static BondHelper HelperOf(JsonElement b, EvaluationContext context)
    {
        var schedule = new Schedule(
            new Date(b.GetProperty("issueDate").GetInt32()),
            new Date(b.GetProperty("maturityDate").GetInt32()),
            new Period(6, TimeUnit.Months),
            new TARGET(),
            BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.Backward,
            false,
            context);
        var bond = new FixedRateBond(
            2, 100.0, schedule, [b.GetProperty("coupon").GetDouble()], new ActualActual(ActualActualConvention.ISDA));
        return new BondHelper(b.GetProperty("price").GetDouble(), bond, context);
    }
}
