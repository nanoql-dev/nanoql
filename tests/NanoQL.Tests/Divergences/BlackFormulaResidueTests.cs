// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Black's formula's rounding residue. The price is never negative on the inputs the formula accepts, but at tiny
/// standard deviations its arithmetic can land about 1e-16 to 1e-15 below zero, which QuantLib's check refuses
/// ("negative value", <c>blackformula.cpp</c>): on the wheel, a call struck at 96.9191326288682 on a forward of
/// 96.91912730254181 with a standard deviation of 1e-8 reads "negative value (-1.45416e-15) for 1e-08 stdDev, Call
/// option, 96.9191 strike , 96.9191 forward", one of the Gaussian copula spread engine's density strikes. nanoql returns
/// 0 for a negative result within 16·ε·D·max(F, K), the forward and the strike displaced. The worst residue,
/// 5.875·ε·D·max(F, K), comes where the formula's two arguments straddle the normal distribution's switch to its
/// asymptotic series, below an erf result of 1e-8, whose truncation N carries there. QuantLib's own precedent: its
/// implied-deviation helper clamps the same residue at 0 ("numerical inaccuracies can yield a negative answer",
/// <c>blackformula.cpp</c>). QuantLib's check: its bits wherever its result isn't negative, a positive residue included.
/// </summary>
public class BlackFormulaResidueTests
{
    [Fact]
    public void QuantLibsRefusedResiduesAreZero()
    {
        Assert.Equal(0.0, BlackFormula(OptionType.Call, 96.9191326288682, 96.91912730254181, 1e-8, 1.0));

        // the worst residue measured, 5.875 epsilon of the forward, where the arguments straddle the switch; on the
        // wheel "negative value (-4.82629e-16) for 1e-08 stdDev, Put option, 0.37 strike , 0.37 forward"
        Assert.Equal(0.0, BlackFormula(OptionType.Put, 0.36999997923559597, 0.37, 1e-8, 1.0));
    }

    [Fact]
    public void AScaleThatOverflowsKeepsQuantLibsRefusal()
    {
        // the worst residue's put at a forward of 3.7e29: at a discount of 1 the residue, about −4.8e14, is 0; at a
        // discount of 1e300 the result overflows to −∞ with the bound's scale, and QuantLib's check refuses it
        Assert.Equal(0.0, BlackFormula(OptionType.Put, 0.36999997923559597e30, 0.37e30, 1e-8, 1.0));
        var e = Assert.Throws<NanoQLException>(() => BlackFormula(OptionType.Put, 0.36999997923559597e30, 0.37e30, 1e-8, 1e300));
        Assert.StartsWith("negative value (-Infinity) for 1E-08 stdDev, Put option, ", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void QuantLibsBitsWhereItsResultIsntNegative()
    {
        // the wheel's values, QuantLib-Python 1.44's blackFormula
        Assert.Equal(7.567289082635513, BlackFormula(OptionType.Call, 100.0, 100.0, 0.2, 0.95));
        Assert.Equal(0.0048499999999999975, BlackFormula(OptionType.Put, 0.03, 0.025, 0.004, 0.97, 0.01));
        Assert.Equal(1.8285977635126432e-17, BlackFormula(OptionType.Call, 0.370000020764404, 0.37, 1e-8, 1.0));
        Assert.Equal(1.4760864197604917e-09, BlackFormula(OptionType.Put, 0.37, 0.37, 1e-8, 1.0));
        Assert.Equal(7.498393406191184e-09, BlackFormula(OptionType.Put, 1.0, 1.0000001, 1e-7, 0.9));
    }

    [Fact]
    public void NoResidueRefusesAcrossTheSwitchOrNearTheMoney()
    {
        // the switch: the largest z whose erf result is at most 1e-8, QuantLib's test for its series
        var erf = new ErrorFunction();
        double lo = -6.0, hi = -5.0;
        for (int i = 0; i < 200; i++)
        {
            double mid = 0.5 * (lo + hi);
            if (0.5 * (1.0 + erf.Evaluate(mid / Math.Sqrt(2.0))) <= 1e-8)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        double[] forwards = [1e-3, 0.37, 1.82, 1e4];
        foreach (double sd in new[] { 1e-16, 1e-14, 1e-12, 1e-10, 1e-8, 1e-6 })
        {
            foreach (double f in forwards)
            {
                foreach (double discount in new[] { 1.0, 0.37 })
                {
                    for (int j = 1; j < 100; j++)
                    {
                        // d1 = ln(F/K)/sd + sd/2 = z + theta sd, so d1 and d2 = d1 - sd straddle the switch at z, and
                        // the mirrored strike puts -d1 and -d2 across it
                        double lnFk = sd * (lo + (j / 100.0 * sd) - (sd / 2.0));
                        foreach (double k in new[] { f * Math.Exp(-lnFk), f * Math.Exp(lnFk) })
                        {
                            foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
                            {
                                Assert.True(BlackFormula(type, k, f, sd, discount) >= 0.0, $"{type} K {k} F {f} sd {sd}");
                            }
                        }
                    }
                }
            }
        }

        // near the money, K = F exp(-d sd), and displaced
        foreach (double f in new[] { 1e-3, 1.0, 96.91912730254181, 1e4, 1e8 })
        {
            foreach (double sd in new[] { 1e-14, 1e-12, 1e-10, 1e-9, 1e-8, 1e-6, 1e-3 })
            {
                for (double d = -7.0; d <= 7.0; d += 0.25)
                {
                    double k = f * Math.Exp(-d * sd);
                    foreach (OptionType type in new[] { OptionType.Call, OptionType.Put })
                    {
                        Assert.True(BlackFormula(type, k, f, sd, 1.0) >= 0.0, $"{type} K {k} F {f} sd {sd}");
                        Assert.True(BlackFormula(type, k - 0.03, f - 0.03, sd, 1.0, 0.03) >= 0.0, $"{type} K {k} F {f} sd {sd} displaced");
                    }
                }
            }
        }
    }
}
