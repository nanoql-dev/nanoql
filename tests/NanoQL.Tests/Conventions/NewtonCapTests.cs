// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.PricingEngines;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The cap on QuantLib's Newton loops in the complex chooser and holder-extensible engines: QuantLib's steps and
/// stopping rule, the value within 0.001 of zero, refused past 1,000 steps with "critical value not found after 1000
/// iterations". No engine input is known whose loop doesn't converge, so the engines' shared loop runs on test-side
/// functions: one without a root (x² + 1 from 0.5), refused; one meeting the rule after exactly 1,000 steps, returned,
/// and after 1,001, refused; and x² − 2 from 1, QuantLib's steps to within its rule of √2. A NaN value stops the loop,
/// as QuantLib's (x² + 1 from 1 steps to 0, then to infinity): no engine input is known to reach one.
/// </summary>
public class NewtonCapTests
{
    private static readonly MethodInfo Newton =
        typeof(AnalyticComplexChooserEngine).GetMethod("Newton", BindingFlags.Static | BindingFlags.NonPublic)!;

    [Fact]
    public void AFunctionWithoutARootIsRefused()
    {
        NanoQLException e = Assert.IsType<NanoQLException>(Inner(() => Solve(0.5, x => ((x * x) + 1.0, 2.0 * x))));
        Assert.Equal("critical value not found after 1000 iterations", e.Message);
    }

    [Fact]
    public void TheCapIsAThousandSteps()
    {
        // the value 1 until the given number of steps, then 0: the first evaluation and one per step
        Func<double, (double, double)> After(int steps)
        {
            int evaluations = 0;
            return _ => (++evaluations > steps ? 0.0 : 1.0, 1.0);
        }

        Assert.Equal(-1000.0, Solve(0.0, After(1000)));
        Assert.IsType<NanoQLException>(Inner(() => Solve(0.0, After(1001))));
    }

    [Fact]
    public void AConvergingFunctionStopsAtQuantLibsRule()
    {
        // 1, 1.5, 1.41667, 1.41422: the third step's value 6.0e-6, the second's 4.3e-3
        double root = Solve(1.0, x => ((x * x) - 2.0, 2.0 * x));
        Assert.Equal(577.0 / 408.0, root, 1e-15);
    }

    private static double Solve(double start, Func<double, (double, double)> f) => (double)Newton.Invoke(null, [start, f])!;

    private static Exception Inner(Action action)
    {
        TargetInvocationException e = Assert.Throws<TargetInvocationException>(action);
        return e.InnerException!;
    }
}
