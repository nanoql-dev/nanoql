// SPDX-FileCopyrightText: Copyright (C) 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/quotes.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Not ported: testObservable (observer notifications, which values have no use for, R1); testObservableHandle (relinkable handles' notifications, which values have no use for, R1)

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Quotes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class QuoteTests
{
    // The evaluation date QuantLib's test runner sets globally (playbook section 9.2).
    private static readonly EvaluationContext RunnerContext = new() { EvaluationDate = new Date(16, Month.September, 2015) };

    private static double Add10(double x) => x + 10;

    private static double Mul10(double x) => x * 10;

    private static double Sub10(double x) => x - 10;

    private static double Add(double x, double y) => x + y;

    private static double Mul(double x, double y) => x * y;

    private static double Sub(double x, double y) => x - y;

    private static double AddAll(Vector x)
    {
        double sum = 0.0;
        for (int i = 0; i < x.Size; i++)
        {
            sum += x[i];
        }

        return sum;
    }

    private static double MulAll(Vector x)
    {
        double product = 1.0;
        for (int i = 0; i < x.Size; i++)
        {
            product *= x[i];
        }

        return product;
    }

    // Adapted: the quote's element is a value, so each value set on QuantLib's SimpleQuote is a new quote.
    [Fact]
    public void TestDerived()
    {
        // Testing derived quotes...
        Func<double, double>[] funcs = [Add10, Mul10, Sub10];
        double[] values = [12, 23, 34];

        foreach (Func<double, double> func in funcs)
        {
            foreach (double value in values)
            {
                var derived = new DerivedQuote(value, func);
                double x = derived.Value, y = func(value);
                Assert.False(
                    Math.Abs(x - y) > 1.0e-10, $"derived quote yields {x}\nfunction result is {y}");
            }
        }
    }

    // Adapted: the quote's elements are values, so each pair set on QuantLib's SimpleQuotes is a new quote.
    [Fact]
    public void TestComposite()
    {
        // Testing composite quotes...
        Func<double, double, double>[] funcs = [Add, Mul, Sub];
        double[] values = [12, 23, 34];

        foreach (Func<double, double, double> func in funcs)
        {
            foreach (double value in values)
            {
                var composite = new CompositeQuote(value, value + 1, func);
                double x = composite.Value, y = func(value, value + 1);
                Assert.False(
                    Math.Abs(x - y) > 1.0e-10, $"composite quote yields {x}\nfunction result is {y}");
            }
        }
    }

    // Adapted: the quote's elements are values, so each set of values on QuantLib's SimpleQuotes is a new quote.
    [Fact]
    public void TestMultiComposite()
    {
        // Testing multi composite quotes...
        Func<Vector, double>[] funcs = [AddAll, MulAll, ArrayFunctions.Norm2];

        foreach (Func<Vector, double> func in funcs)
        {
            var mes = new List<double>();
            for (int i = 0; i < 3; i++)
            {
                mes.Add(i + 1);
                for (int j = 0; j <= i; j++)
                {
                    mes[j] = (j * 10) + 1;
                    var composite = new MultiCompositeQuote(mes, func);
                    var args = new Vector(mes.Count);
                    for (int k = 0; k < mes.Count; k++)
                    {
                        args[k] = mes[k];
                    }

                    double x = composite.Value, y = func(args);
                    Assert.False(
                        Math.Abs(x - y) > 1.0e-10, $"composite quote yields {x}\nfunction result is {y}");
                    for (int k = 0; k < mes.Count; k++)
                    {
                        Assert.Equal(mes[k], composite.InputValue(k));
                    }
                }
            }
        }
    }

    // Adapted: the observer checks (lines 193-198, 227-238) become new quotes on the moved forward and price, the
    // forward value rechecked against the index as QuantLib's is (lines 200-205), and the implied standard deviation
    // against the formula from the quote's guess.
    [Fact]
    public void TestForwardValueQuoteAndImpliedStdevQuote()
    {
        // Testing forward-value and implied-standard-deviation quotes...
        double forwardRate = .05;
        DayCounter dc = new ActualActual(ActualActualConvention.ISDA);
        Calendar calendar = new TARGET();
        EvaluationContext context = RunnerContext;
        Date evaluationDate = context.EvaluationDate;
        var euriborTenor = new Period(1, TimeUnit.Years);
        Date fixingDate = calendar.Advance(evaluationDate, euriborTenor);

        Euribor Euribor(double rate) => new(euriborTenor, new FlatForward(evaluationDate, rate, dc));

        Euribor euribor = Euribor(forwardRate);
        var forwardValueQuote = new ForwardValueQuote(euribor, fixingDate, context);
        double forwardValue = forwardValueQuote.Value;
        double expectedForwardValue = euribor.Fixing(fixingDate, context, true);

        // we test if the forward value given by the quote is consistent
        // with the one directly given by the index
        Assert.False(
            Math.Abs(forwardValue - expectedForwardValue) > 1.0e-15,
            $"Foward Value Quote quote yields {forwardValue}\nexpected result is {expectedForwardValue}");

        // and we retest if the values are still matching on a moved forward
        double forward = 0.04;
        euribor = Euribor(forward);
        forwardValue = new ForwardValueQuote(euribor, fixingDate, context).Value;
        expectedForwardValue = euribor.Fixing(fixingDate, context, true);
        Assert.False(
            Math.Abs(forwardValue - expectedForwardValue) > 1.0e-15,
            $"Foward Value Quote quote yields {forwardValue}\nexpected result is {expectedForwardValue}");

        // we test the ImpliedStdevQuote class
        double price = 0.02;
        double strike = 0.04;
        double guess = .15;
        double accuracy = 1.0e-6;
        OptionType optionType = OptionType.Call;
        var impliedStdevQuote = new ImpliedStdDevQuote(optionType, forward, price, strike, guess, accuracy);
        double impliedStdev = impliedStdevQuote.Value;
        double expectedImpliedStdev =
            BlackFormulaFunctions.BlackFormulaImpliedStdDev(optionType, strike, forward, price, 1.0, 0.0, guess, 1.0e-6);
        Assert.False(
            Math.Abs(impliedStdev - expectedImpliedStdev) > 1.0e-15,
            $"\nimpliedStdevQuote yields :{impliedStdev}\nexpected result is       :{expectedImpliedStdev}");

        // and on the moved forward
        forward = 0.05;
        impliedStdev = new ImpliedStdDevQuote(optionType, forward, price, strike, guess, accuracy).Value;
        expectedImpliedStdev =
            BlackFormulaFunctions.BlackFormulaImpliedStdDev(optionType, strike, forward, price, 1.0, 0.0, guess, 1.0e-6);
        Assert.Equal(expectedImpliedStdev, impliedStdev);

        // and on the moved price, above the call's largest value, where the formula has no root and the quote is 0
        price = 0.11;
        Assert.ThrowsAny<NanoQLException>(
            () => BlackFormulaFunctions.BlackFormulaImpliedStdDev(optionType, strike, forward, price, 1.0, 0.0, guess, 1.0e-6));
        Assert.Equal(0.0, new ImpliedStdDevQuote(optionType, forward, price, strike, guess, accuracy).Value);
    }
}
