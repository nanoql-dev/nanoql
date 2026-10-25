// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Reflection;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The hybrid Heston-Hull-White process's step terms at small reversion (the S-hes plan's HR6 and HR7 (xv)). QuantLib's
/// m₂, m₄, m₅, v₁ and v₁₂ (<c>hybridhestonhullwhiteprocess.cpp:127-165</c>) combine terms of order one into results of
/// order (a·dt)ᵖ: at a·dt 1e-6 its m₄ is −1.15e-5 for 4.33e-7, and at 5e-13·0.2 its v₁ is −2.5e16, a negative variance
/// the step refuses. nanoql evaluates QuantLib's expressions in double-double below a·dt = 1e-1, in quad-double below
/// 5e-7 and below 1e-12 by the cubic through four quad-double values, the step's length t − s exact there (D3-2 of the
/// S-hes run record), QuantLib's double and bits from the cut up; the cut and floors measured against the expressions in
/// 250 digits (<c>scratchpad\cxxprobe\src\hhwstep250.cpp</c>, <c>scratchpad\c3\step\cuts.py</c>).
/// </summary>
public class HybridHestonHullWhiteStepTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly DayCounter A360 = new Actual360();

    [Fact]
    public void TheStepTermsHoldTheExactValuesAtAndBelowTheCut()
    {
        // s 0.1, dt 0.2 (t − s one ulp off dt), T 1.3, σ 0.01, η 0.2, ρ −0.9; a·dt 0.1, 1e-2, 1e-6 and 1e-13; the
        // 250-digit values, QuantLib's double in the comments
        (double A, string[] Exact)[] points =
        [
            (0.5, ["0.000304423029946029758839991349487", "3.86681010323380115346656573299e-07", "-1.67285790237163004773661250527e-06", "0.00793058874790852712901162765887", "-0.000340774111669333091284749482306"]),
            (0.049999999999999996, ["0.000385278540144984442177621052349", "4.28368436253826667145322630687e-07", "-2.19610406851531008566650955529e-06", "0.00792850407716477009104917604771", "-0.000356225868861566274605257723956"]),
            (4.9999999999999996e-06, ["0.000395998908002013121137781800308", "4.33332833333686464915852384689e-07", "-2.2666594666812610047533918395e-06", "0.00792826669046666316740878742774", "-0.000357999822000058938762193818443"]), // QuantLib: m4 -1.15e-5, m5 1.09e-4
            (4.9999999999999999e-13, ["0.000395999999999890923928645143879", "4.33333333333283548439044357956e-07", "-2.26666666666594769362296459053e-06", "0.0079282666666666714540742732638", "-0.000357999999999982305443431764354"]), // QuantLib: m2 -1.0e6, v1 -2.5e16
        ];
        double s = 0.1, dt = 0.2;
        double t = s + dt;
        foreach ((double a, string[] exact) in points)
        {
            for (int k = 0; k < 5; ++k)
            {
                double expected = double.Parse(exact[k], CultureInfo.InvariantCulture);
                double term = Term(k, a, 0.01, 0.2, -0.9, s, t, dt, 1.3);
                Assert.True(Math.Abs(term - expected) <= 1e-12 * Math.Abs(expected), $"a {a}, term {k}: {term} for {exact[k]}");
            }
        }
    }

    [Fact]
    public void TheTermsAreContinuousAcrossTheCut()
    {
        double dt = 0.25;
        double below = 0.1 * (1 - 1e-12) / dt, above = 0.1 * (1 + 1e-12) / dt;
        for (int k = 0; k < 5; ++k)
        {
            double lo = Term(k, below, 0.01, 0.2, -0.9, 0.0, dt, dt, dt);
            double hi = Term(k, above, 0.01, 0.2, -0.9, 0.0, dt, dt, dt);
            Assert.True(Math.Abs(hi - lo) <= 1e-11 * Math.Abs(hi), $"term {k}: {lo} below, {hi} above");
        }
    }

    [Fact]
    public void QuantLibsStepStandsAtTheCut()
    {
        // a·dt 0.1: QuantLib's double, its evolve on one clock (scratchpad\cxxprobe\src\c3oracle.cpp)
        YieldTermStructure r = new FlatForward(Today, 0.05, A360);
        var process = new HybridHestonHullWhiteProcess(
            new HestonProcess(r, new FlatForward(Today, 0.02 * 360.0 / 365.0, A360), 100.0, 0.04, 1.5, 0.04, 0.4, -0.6),
            new HullWhiteForwardProcess(r, 0.1, 0.01).WithForwardMeasureTime(2.0),
            -0.4);
        Vector y = process.Evolve(0.0, new Vector([100.0, 0.04, 0.0]), 1.0, new Vector([0.3, -1.2, 0.7]));
        double[] quantLib = [102.34565708190577, -0.051200000000000009, 0.012706357731174563];
        for (int i = 0; i < 3; ++i)
        {
            Assert.True(Math.Abs(y[i] - quantLib[i]) <= 4e-15 * Math.Abs(quantLib[i]), $"state {i}: {y[i]} for {quantLib[i]}");
        }
    }

    // the process's private Term in its dispatch, by the term's index (m2, m4, m5, v1, v12)
    private static double Term(int k, double a, double sigma, double eta, double rho, double s, double t, double dt, double tMeasure)
    {
        Type termType = typeof(HybridHestonHullWhiteProcess).GetNestedType("StepTerm", BindingFlags.NonPublic)!;
        MethodInfo term = typeof(HybridHestonHullWhiteProcess).GetMethod("Term", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (double)term.Invoke(null, [Enum.ToObject(termType, k), a, sigma, eta, rho, s, t, dt, tMeasure])!;
    }
}
