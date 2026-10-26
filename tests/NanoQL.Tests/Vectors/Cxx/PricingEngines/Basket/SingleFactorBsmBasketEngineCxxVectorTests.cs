// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Reflection;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's single-factor basket engine vectors,
/// <c>tests/vectors/cxx/pricingengines/basket/singlefactorbsmbasketengine.json</c>:
/// <see cref="SingleFactorBsmBasketEngine"/>'s NPV and root's d on positive and negative weights, spots and
/// volatilities and every volatility 0, and its internal root solver's values, its roots by four strategies with their
/// call counts, and its two refusals, read through reflection (QuantLib's <c>detail</c> namespace).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SingleFactorBsmBasketEngineCxxVectorTests
{
    private static readonly Type SolverType =
        typeof(ChoiBasketEngine).Assembly.GetType("NanoQL.PricingEngines.SumExponentialsRootSolver", true)!;

    [Fact]
    public void SingleFactorBsmBasketEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/basket/singlefactorbsmbasketengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Name.StartsWith("engine", StringComparison.Ordinal))
            {
                var today = new Date(c.Int("today"));
                DayCounter dc = new Actual365Fixed();
                var rTS = new FlatForward(today, c.Double("r"), dc);
                double[] spots = c.Doubles("spots"), divs = c.Doubles("dividends"), vols = c.Doubles("vols");
                ImmutableArray<GeneralizedBlackScholesProcess> processes =
                [
                    .. spots.Select((s, i) => (GeneralizedBlackScholesProcess)new BlackScholesMertonProcess(
                        s, new FlatForward(today, divs[i], dc), rTS, new BlackConstantVol(today, new NullCalendar(), vols[i], dc))),
                ];
                double[] payoff = c.Doubles("payoff");
                var option = new BasketOption(
                    new AverageBasketPayoff(new PlainVanillaPayoff((OptionType)(int)payoff[0], payoff[1]), c.Doubles("weights")),
                    new EuropeanExercise(new Date(c.Int("maturity"))));
                MultiAssetOptionResults r = SingleFactorBsmBasketEngine.Calculate(option, c.Context(), processes);
                Kept(c, check, "NPV", r.NPV);
                Kept(c, check, "d", r.AdditionalResults.TryGetValue("d", out object? d) ? d : null);
                continue;
            }

            double[] a = c.Doubles("a"), sig = c.Doubles("sig");
            double k = c.Double("K");
            if (c.Name.EndsWith("(refused)", StringComparison.Ordinal))
            {
                check.Output(c, "BrentRoot", () => GetRoot(a, sig, k, c.Double("xTol"), 2).Root);
                continue;
            }

            // the root solver's outputs S7R6 keeps (the S-exo plan's ED12), each strategy's root and counts
            object solver = Solver(a, sig, k);
            double x = c.Double("x");
            check.Output(c, "value", Invoke(solver, "Evaluate", x));
            check.Output(c, "derivative", Invoke(solver, "Derivative", x));
            check.Output(c, "secondDerivative", Invoke(solver, "SecondDerivative", x));
            foreach ((string name, int strategy) in new[] { ("Ridder", 0), ("Newton", 1), ("Brent", 2), ("Halley", 3) })
            {
                Func<(double Root, int F, int D, int S)> root = () => GetRoot(a, sig, k, c.Double("xTol"), strategy);
                Kept(c, check, name + "Root", () => root().Root);
                Kept(c, check, name + "FCtr", () => root().F);
                Kept(c, check, name + "DerivativeCtr", () => root().D);
                Kept(c, check, name + "SecondDerivativeCtr", () => root().S);
            }
        }

        check.Complete();
    }

    // An output the generator's stability filter kept (S7R6): it compares only what the file holds.
    private static void Kept(VectorCase c, VectorAssert check, string name, object? actual)
    {
        if (c.Expected.TryGetProperty(name, out _))
        {
            check.Output(c, name, actual);
        }
    }

    private static void Kept(VectorCase c, VectorAssert check, string name, Func<object?> actual)
    {
        if (c.Expected.TryGetProperty(name, out _))
        {
            check.Output(c, name, actual);
        }
    }

    private static object Solver(double[] a, double[] sig, double k) =>
        SolverType.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic, [typeof(double[]), typeof(double[]), typeof(double)])!
            .Invoke([a, sig, k]);

    private static double Invoke(object solver, string method, double x) =>
        (double)SolverType.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(solver, [x])!;

    private static (double Root, int F, int D, int S) GetRoot(double[] a, double[] sig, double k, double xTol, int strategy)
    {
        Type strategyType = SolverType.GetNestedType("Strategy", BindingFlags.NonPublic)!;
        object result;
        try
        {
            result = SolverType.GetMethod("GetRoot", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(Solver(a, sig, k), [xTol, Enum.ToObject(strategyType, strategy)])!;
        }
        catch (TargetInvocationException e)
        {
            throw e.InnerException!;
        }

        Type r = result.GetType();
        return (
            (double)r.GetProperty("Root")!.GetValue(result)!,
            (int)r.GetProperty("FCtr")!.GetValue(result)!,
            (int)r.GetProperty("DerivativeCtr")!.GetValue(result)!,
            (int)r.GetProperty("SecondDerivativeCtr")!.GetValue(result)!);
    }
}
