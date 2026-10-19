// SPDX-FileCopyrightText: Copyright (C) 2025 Paolo D'Elia
// SPDX-FileCopyrightText: Copyright (C) 2026 Yassine Idyiahia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/interpolatedsmilesection.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Replaced: testHandlesUpdatePropagates (Conventions/InterpolatedSmileSectionQuoteTests.cs)

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class InterpolatedSmileSectionTests
{
    // Adapted: the section's public volatilityImpl and varianceImpl (lines 71 and 75) are protected here, as C# can't
    // widen an override, and the base's public Volatility and Variance return them unchanged.
    [Fact]
    public void TestInterpolationAndVariance()
    {
        // Testing basic behavior of linearly interpolated smile section...

        // basic scenario: sorted strikes, constructor taking stdDevs (total std devs)
        double expiry = 0.25; // 3 months
        Real sqrtT = Math.Sqrt(expiry);
        Rate[] strikes = [90.0, 100.0, 110];

        // total std deviations (i.e., sigma * sqrt(T))
        Real[] stdDevs = [0.20 * sqrtT, 0.15 * sqrtT, 0.18 * sqrtT];
        Real atmLevel = 95.0;

        var section = new InterpolatedSmileSection<Linear>(expiry, strikes, stdDevs, atmLevel);

        // pick an interior strike 95 between 90 and 100
        Real strike = 95.0;

        Real v90 = stdDevs[0] / sqrtT;
        Real v100 = stdDevs[1] / sqrtT;
        Real expectedVol = LinearInterp(strike, 90.0, v90, 100.0, v100);

        Real tol = 1e-12;
        CheckClose(section.Volatility(strike), expectedVol, tol);

        // variance should be vol^2 * T
        Real expectedVar = expectedVol * expectedVol * expiry;
        CheckClose(section.Variance(strike), expectedVar, tol);
    }

    // Adapted: volatilityImpl (lines 101 and 109) is the base's public Volatility, as in testInterpolationAndVariance.
    [Fact]
    public void TestExtrapolationWhenAllowed()
    {
        // Testing extrapolation behavior of linearly interpolated smile section...

        // test extrapolation behavior when flatStrikeExtrapolation=false
        double expiry = 0.25;
        Real sqrtT = Math.Sqrt(expiry);
        Rate[] strikes = [90.0, 100.0, 110];
        Real[] stdDevs = [0.20 * sqrtT, 0.15 * sqrtT, 0.18 * sqrtT];
        Real atmLevel = 95.0;

        var section = new InterpolatedSmileSection<Linear>(expiry, strikes, stdDevs, atmLevel);

        // compute expected extrapolated vol at strike 80 (below min strike 90)
        Real v90 = stdDevs[0] / sqrtT;
        Real v100 = stdDevs[1] / sqrtT;
        Real strikeLow = 80.0;
        Real expectedLow = LinearInterp(strikeLow, 90.0, v90, 100.0, v100);

        Real tol = 1e-12;
        CheckClose(section.Volatility(strikeLow), expectedLow, tol);

        // extrapolate above max strike, e.g., 120 using last segment 110-100
        Real v110 = stdDevs[2] / sqrtT;
        Real strikeHigh = 120.0;

        // use last two points (110, v110) and (100, v100) for extrapolation;
        // linearInterp works for extrapolation as well
        Real expectedHigh = LinearInterp(strikeHigh, 110.0, v110, 100.0, v100);
        CheckClose(section.Volatility(strikeHigh), expectedHigh, tol);
    }

    // Adapted: the quote handles (lines 160-169) are their values, the quote form being the value form, so the changed
    // quote (line 190) is a section built from the new value; volatilityImpl (lines 181, 187 and 192) is the base's
    // public Volatility.
    [Fact]
    public void TestFlatStrikeExtrapolation()
    {
        // Testing flat strike extrapolation in interpolated smile section...

        double expiry = 0.25;
        Real sqrtT = Math.Sqrt(expiry);
        Rate[] strikes = [90.0, 100.0, 110.0];

        Real q0 = 0.20 * sqrtT, q1 = 0.15 * sqrtT, q2 = 0.18 * sqrtT;
        Real atm = 95.0;

        var section = new InterpolatedSmileSection<Linear>(
            expiry, strikes, [q0, q1, q2], atm, default, new Actual365Fixed(), VolatilityType.ShiftedLognormal, 0.0, true);

        // Check with strike lower than minStrike()
        Real v90 = q0 / sqrtT;
        Real strikeLow = 85.0;

        Real tol = 1e-12;
        CheckClose(section.Volatility(strikeLow), v90, tol);

        // Check with strike higher than maxStrike()
        Real v110 = q2 / sqrtT;
        Real strikeHigh = 120.0;

        CheckClose(section.Volatility(strikeHigh), v110, tol);

        // Change minStrike vol quote and check if out-of-bounds vol is the same
        q0 = 0.21 * sqrtT;
        section = new InterpolatedSmileSection<Linear>(
            expiry, strikes, [q0, q1, q2], atm, default, new Actual365Fixed(), VolatilityType.ShiftedLognormal, 0.0, true);
        Real v90_after = q0 / sqrtT;
        CheckClose(section.Volatility(strikeLow), v90_after, tol);
    }

    [Fact]
    public void TestErrorThrowingWhenNonSortedStrikes()
    {
        // Testing that creation of interpolated smile section with non-sorted strikes throws...

        // basic scenario: sorted strikes, constructor taking stdDevs (total std devs)
        double expiry = 0.25; // 3 months
        Real sqrtT = Math.Sqrt(expiry);
        Rate[] strikes = [90.0, 110.0, 100];

        // total std deviations (i.e., sigma * sqrt(T))
        Real[] stdDevs = [0.20 * sqrtT, 0.15 * sqrtT, 0.18 * sqrtT];
        Real atmLevel = 95.0;

        Assert.ThrowsAny<ArgumentException>(() => new InterpolatedSmileSection<Linear>(expiry, strikes, stdDevs, atmLevel));
    }

    [Fact]
    public void TestDigitalOptionPriceAtLargeStrikes()
    {
        // Testing digital option price at strikes large enough to absorb the differencing gap...

        // The digital differences the option price across a gap. Once the gap falls
        // below the strike's own precision the interval quantizes to whole ulps, and
        // the probability that comes back is wrong without anything reporting it.
        // The low levels also pin the unaffected range, where the floor never binds.
        double expiry = 1.0;
        Real sqrtT = Math.Sqrt(expiry);
        Volatility vol = 0.20;
        Real tol = 1e-4;

        // At the money with a flat smile, P(S > F) = N(-sigma sqrt(T) / 2),
        // independent of the level.
        Real expected = new CumulativeNormalDistribution().Evaluate(-0.5 * vol * sqrtT);

        foreach (Real atmLevel in new[] { 1.0e2, 1.0e3, 1.0e6, 1.0e11 })
        {
            Rate[] strikes = [0.9 * atmLevel, atmLevel, 1.1 * atmLevel];
            Real[] stdDevs = [vol * sqrtT, vol * sqrtT, vol * sqrtT];

            var section = new InterpolatedSmileSection<Linear>(expiry, strikes, stdDevs, atmLevel);

            Real calculated = section.DigitalOptionPrice(atmLevel, OptionType.Call, 1.0);

            if (Math.Abs(calculated - expected) > tol)
            {
                Assert.Fail(
                    $"failed to reproduce the digital option price at a large strike\n   strike:     {atmLevel}"
                    + $"\n   calculated: {calculated}\n   expected:   {expected}\n   diff:       {calculated - expected}"
                    + $"\n   tolerance:  {tol}");
            }
        }
    }

    [Fact]
    public void TestDensityAtLargeStrikes()
    {
        // Testing smile-section density at equity index level strikes, which absorb the differencing gap...

        // density differences the digital across a gap, and the digital itself
        // differences the option price, so the roundoff is |strike|*eps/gap^2
        // against a density falling like 1/strike. The relative error therefore
        // grows like strike^2 and reaches 100% around 3e4 -- Nikkei or Hang Seng
        // levels -- so a fixed gap fails far earlier here than for the digital.
        double expiry = 1.0;
        Real sqrtT = Math.Sqrt(expiry);
        Volatility vol = 0.20;
        Real tol = 1e-3;

        // At 1e2 the floor coincides with the default gap and nothing changes. The
        // middle three are KOSPI, S&P 500 and Nikkei 225 levels, where the unfixed
        // density is respectively 0.2%, 8% and 600% out. Past 1e6 the second
        // difference is pure roundoff and can come back negative.
        foreach (Real atmLevel in new[] { 1.0e2, 2.5e3, 5.0e3, 3.8e4, 1.0e6, 1.0e11 })
        {
            Rate[] strikes = [0.9 * atmLevel, atmLevel, 1.1 * atmLevel];
            Real[] stdDevs = [vol * sqrtT, vol * sqrtT, vol * sqrtT];

            var section = new InterpolatedSmileSection<Linear>(expiry, strikes, stdDevs, atmLevel);

            Real calculated = section.Density(atmLevel, 1.0);

            // At the money with a flat smile the Black density is
            // phi(d2) / (K sigma sqrt(T)) with d2 = -sigma sqrt(T) / 2.
            Real d2 = -0.5 * vol * sqrtT;
            Real expected = new NormalDistribution().Evaluate(d2) / (atmLevel * vol * sqrtT);

            // NaN would make the relative error below compare
            // false against the tolerance and slip through.
            Assert.True(double.IsFinite(calculated));
            Real relError = Math.Abs(calculated - expected) / expected;
            if (relError > tol)
            {
                Assert.Fail(
                    $"failed to reproduce the density at a large strike\n   strike:     {atmLevel}\n   calculated: {calculated}"
                    + $"\n   expected:   {expected}\n   rel error:  {relError}\n   tolerance:  {tol}");
            }
        }
    }

    [Fact]
    public void TestDensityHonoursExplicitGap()
    {
        // Testing that smile-section density honours an explicit gap...

        // A gap above the floor must reach both digitals and the divisor, leaving
        // density the central second difference of the call price.
        double expiry = 1.0;
        Real sqrtT = Math.Sqrt(expiry);
        Volatility vol = 0.20;
        Real atmLevel = 1.0e11;
        Real gap = 1.0e10;

        Rate[] strikes = [0.9 * atmLevel, atmLevel, 1.1 * atmLevel];
        Real[] stdDevs = [vol * sqrtT, vol * sqrtT, vol * sqrtT];

        var section = new InterpolatedSmileSection<Linear>(expiry, strikes, stdDevs, atmLevel);

        Real calculated = section.Density(atmLevel, 1.0, gap);

        Real stdDev = vol * sqrtT;
        Real expected = (BlackFormula(OptionType.Call, atmLevel - gap, atmLevel, stdDev)
            - (2.0 * BlackFormula(OptionType.Call, atmLevel, atmLevel, stdDev))
            + BlackFormula(OptionType.Call, atmLevel + gap, atmLevel, stdDev)) / (gap * gap);

        Real tol = 1e-8;
        Real relError = Math.Abs(calculated - expected) / expected;
        if (relError > tol)
        {
            Assert.Fail(
                $"failed to honour the explicit gap\n   gap:        {gap}\n   calculated: {calculated}\n   expected:   {expected}"
                + $"\n   rel error:  {relError}\n   tolerance:  {tol}");
        }
    }

    // Helper for linear interpolation/extrapolation between two points
    private static Real LinearInterp(Real x, Real x1, Real y1, Real x2, Real y2)
    {
        if (x2 == x1)
        {
            return y1;
        }

        return y1 + ((y2 - y1) * ((x - x1) / (x2 - x1)));
    }

    // QL_CHECK_CLOSE, Boost's BOOST_CHECK_CLOSE: |a - b|/|b| and |a - b|/|a| each within percent/100, with its safe
    // division.
    private static void CheckClose(Real a, Real b, Real percent)
    {
        Real diff = Math.Abs(a - b), tolerance = 0.01 * percent;
        bool close = SafeDivision(diff, Math.Abs(b)) <= tolerance && SafeDivision(diff, Math.Abs(a)) <= tolerance;
        Assert.True(close, $"difference between {a:R} and {b:R} exceeds {percent}%");
    }

    private static Real SafeDivision(Real f1, Real f2)
    {
        // Avoid overflow.
        if (f2 < 1 && f1 > f2 * double.MaxValue)
        {
            return double.MaxValue;
        }

        // Avoid underflow.
        if (f1 == 0 || (f2 > 1 && f1 < f2 * 2.2250738585072014E-308))
        {
            return 0;
        }

        return f1 / f2;
    }
}
