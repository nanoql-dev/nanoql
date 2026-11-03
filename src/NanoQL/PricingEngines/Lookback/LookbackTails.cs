// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic lookback engines' products of a power and a normal probability past double's range: at small
/// volatilities λ = 2b/σ² is large, s^(−λ) overflows to +∞ while its normal factor underflows to 0, and QuantLib's
/// product is NaN where its value is finite (4.75614712250357 for a seasoned fixed call at σ 0.3%).
/// </summary>
/// <remarks>
/// Where QuantLib's product is finite it stands, its bits. Elsewhere the product is taken in logs, s^a·P = exp(a ln s +
/// ln P): ln N(z) from Laplace's continued fraction below z = −5 (N(z) = φ(z) R(z), R(z) = 1/(t + 1/(t + 2/(t + 3/(t +
/// ⋯)))) at t = −z, 100 terms from the tail, within 1.6e-16 relative of mpmath's down to z = −1e5) and from QuantLib's
/// N above; ln M(h, k; ρ), the bivariate normal, as the integral of φ(u) N(w), w = (k − ρu)/√(1 − ρ²), over u ≤ h. Its
/// log-integrand is concave, so the integral is taken outward from its peak, on segments at fixed steps and at the
/// peak's own width, which falls as 1/|h| where the peak is on h, every segment on NanoQL's adaptive Kronrod rule at
/// 1e-12 of the whole; the integrand is e^(g(u) − g(peak)) with its exponent's terms of order h² and h cancelled
/// analytically, the arguments reaching 1e6 at σ 1e-7. Checked against QuantLib's formulas at 80 digits, the bivariate
/// normal there by the same peak-outward integral in mpmath (a single quadrature from −∞ is 2.9e-4 off on M =
/// N(−45.1)): 280 cells of the four engines from σ 1% to 1e-5, on carries of ±4%, within 3.3e-15 of max(|value|, 1),
/// and 12 cells at σ 1e-6 and 1e-7 within 2.4e-15 (<c>Divergences/LookbackSmallVolatilityTests.cs</c>).
/// </remarks>
internal static class LookbackTails
{
    private const int FractionTerms = 100;
    private const double Tolerance = 1e-12;
    private const int Cap = 100000;

    private static readonly CumulativeNormalDistribution F = new();
    private static readonly double LogSqrtTwoPi = 0.5 * Math.Log(2.0 * Math.PI);

    /// <summary>s^a·N(z), its log taken where the plain product isn't finite.</summary>
    /// <param name="product">QuantLib's product, s^a·N(z).</param>
    /// <param name="s">The base, positive.</param>
    /// <param name="a">The exponent.</param>
    /// <param name="z">The normal's argument.</param>
    /// <returns>The product.</returns>
    internal static double PowerTimesN(double product, double s, double a, double z) =>
        double.IsFinite(product) ? product : Math.Exp((a * Math.Log(s)) + LogN(z));

    /// <summary>c·s^a·M(h, k; ρ), its log taken where the plain product isn't finite.</summary>
    /// <param name="product">QuantLib's product.</param>
    /// <param name="c">A positive factor.</param>
    /// <param name="s">The base, positive.</param>
    /// <param name="a">The exponent.</param>
    /// <param name="h">The bivariate normal's first argument.</param>
    /// <param name="k">Its second argument.</param>
    /// <param name="rho">Its correlation.</param>
    /// <returns>The product.</returns>
    internal static double PowerTimesM(double product, double c, double s, double a, double h, double k, double rho) =>
        double.IsFinite(product) ? product : Math.Exp(Math.Log(c) + (a * Math.Log(s)) + LogM(h, k, rho));

    /// <summary>ln N(z), finite to z = −1e4 and beyond.</summary>
    /// <param name="z">The argument.</param>
    /// <returns>ln N(z).</returns>
    internal static double LogN(double z) =>
        !(z < -5.0) ? Math.Log(F.Evaluate(z)) : (-0.5 * z * z) - LogSqrtTwoPi - Math.Log(Fraction(-z));

    // φ(z)/N(z) at z = −t below −5, Laplace's continued fraction t + 1/(t + 2/(t + 3/(t + ⋯))), from the tail
    private static double Fraction(double t)
    {
        double fraction = t;
        for (int k = FractionTerms; k >= 1; --k)
        {
            fraction = t + (k / fraction);
        }

        return fraction;
    }

    // φ(w)/N(w), the inverse Mills ratio
    private static double Psi(double w) => w < -5.0 ? Fraction(-w) : Math.Exp((-0.5 * w * w) - LogSqrtTwoPi) / F.Evaluate(w);

    /// <summary>ln M(h, k; ρ), finite in the tails.</summary>
    /// <param name="h">The first argument.</param>
    /// <param name="k">The second argument.</param>
    /// <param name="rho">The correlation, in [−1, 1].</param>
    /// <returns>ln M; −∞ where M is 0.</returns>
    internal static double LogM(double h, double k, double rho)
    {
        if (rho == 1.0)
        {
            return LogN(Math.Min(h, k));
        }

        if (rho == -1.0)
        {
            double m = F.Evaluate(h) + F.Evaluate(k) - 1.0;
            return m > 0.0 ? Math.Log(m) : double.NegativeInfinity;
        }

        if (rho == 0.0)
        {
            return LogN(h) + LogN(k);
        }

        // M = ∫_{−∞}^{h} φ(u) N(w) du, w = (k − ρu)/s, its log-integrand g concave, so unimodal; g'(u) = −u − β ψ(w)
        // with β = ρ/s and ψ = φ/N, and the peak at h where g' is still positive there
        double sr = Math.Sqrt(1.0 - (rho * rho));
        double beta = rho / sr;
        double Slope(double u) => -u - (beta * Psi((k - (rho * u)) / sr));

        double peak = h;
        if (Slope(h) < 0.0)
        {
            double lo = h - 1.0;
            while (Slope(lo) < 0.0)
            {
                lo = h - (2.0 * (h - lo));
            }

            double hi = h;
            for (int i = 0; i < 200 && hi - lo > 1e-12 * Math.Max(1.0, Math.Abs(hi)); ++i)
            {
                double mid = 0.5 * (lo + hi);
                if (Slope(mid) > 0.0)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            peak = 0.5 * (lo + hi);
        }

        double wp = (k - (rho * peak)) / sr;
        double top = (-0.5 * peak * peak) - LogSqrtTwoPi + LogN(wp);

        // the integrand at an offset v from the peak, e^(g(p + v) − g(p)), its exponent taken without subtracting two
        // values of order p²: where w and w_p are in the continued fraction's range, ln N(w) − ln N(w_p) is
        // −Δw(w + w_p)/2 − ln(ψ(w)/ψ(w_p)), Δw = −βv, and with φ's −v(p + v/2) the exponent is
        // v(βw_p − p) − (1 + β²)v²/2 − ln(ψ(w)/ψ(w_p)), its slope at the peak taken once, where its two terms of order
        // p, rounded at every v, would leave noise of order εp in the integrand
        double linear = (beta * wp) - peak;
        double Integrand(double v)
        {
            double w = wp - (beta * v);
            double exponent = w < -5.0 && wp < -5.0
                ? (v * linear) - (0.5 * (1.0 + (beta * beta)) * v * v) - Math.Log(Fraction(-w) / Fraction(-wp))
                : (-v * (peak + (0.5 * v))) + LogN(w) - LogN(wp);
            return Math.Exp(exponent);
        }

        // the peak's width, from g'' = −1 − β² ψ(w)(w + ψ(w)), or the slope at a peak on h, which grows with |h|
        double psi = Psi(wp);
        double width = 1.0 / Math.Sqrt(1.0 + (beta * beta * psi * (wp + psi)));
        double slope = Slope(peak);
        if (slope > 0.0)
        {
            width = Math.Min(width, 1.0 / slope);
        }

        // the offsets at fixed steps and at the width's, up to h
        double[] steps = [0.01, 0.1, 1.0, 10.0, 40.0];
        var points = new SortedSet<double> { 0.0 };
        foreach (double step in steps)
        {
            points.Add(-step);
            points.Add(step);
            if (width < 1.0)
            {
                points.Add(-step * width);
                points.Add(step * width);
            }
        }

        double end = Math.Min(h - peak, 40.0);
        double[] offsets = [.. points.Where(v => v < end), end];

        // every segment at one absolute tolerance on the whole integral, its scale the sum of the segments' 15-point
        // first estimates, which the segments about the peak, the integrand near 1 on them, dominate: a far segment's
        // own first estimate can miss its mass by orders of magnitude, and a relative tolerance on it can't be met
        var estimator = new GaussKronrodAdaptive(double.MaxValue, 15);
        double scale = 0.0;
        for (int i = 0; i + 1 < offsets.Length; ++i)
        {
            scale += estimator.Integrate(Integrand, offsets[i], offsets[i + 1]);
        }

        double integral = 0.0;
        var rule = new GaussKronrodAdaptive(Tolerance, Cap);
        for (int i = 0; i + 1 < offsets.Length; ++i)
        {
            integral += rule.Integrate(v => Integrand(v) / scale, offsets[i], offsets[i + 1]) * scale;
        }

        return top + Math.Log(integral);
    }
}
