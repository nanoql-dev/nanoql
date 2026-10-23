// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2013, 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/kahalesmilesection.hpp ql/termstructures/volatility/kahalesmilesection.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.TermStructures;

/// <summary>
/// An arbitrage-free smile section by Kahale's C¹ interpolation and extrapolation of a source section's call prices
/// (Kahale, "An arbitrage-free interpolation of volatilities", Risk, May 2004).
/// </summary>
/// <remarks>
/// <para>
/// The source's arbitrage-free region (<see cref="SmileSectionUtils"/>) is its core. Left of it the call prices are a
/// Black call plus a constant, right of it a Black call or, with exponential extrapolation, an exponential; inside it
/// the source's prices are kept, or, with interpolation, replaced by Kahale's segment functions, one per grid interval.
/// So in the leftmost interval and right of the last grid point the source's smile is always replaced. A shifted
/// source is handled by shifting its forward and strikes and fitting as for a lognormal one; a normal source is
/// refused. The fit runs at construction.
/// </para>
/// <para>
/// Five of QuantLib's defects are fixed. The put branch of the volatility implies it from the call at the shifted
/// strike, where QuantLib's adds the unshifted one (<c>kahalesmilesection.cpp:255</c>), so with a shift every put-branch
/// volatility reads 0. The left wing's check reads a digital at the wing's midpoint, (k₁ − 2·shift)/2 unshifted for the
/// wing's shifted end k₁, where QuantLib's reads it at (k₁ − shift)/2 (<c>:109-110</c>), which lies in the core once its
/// left end is negative unshifted, so the check fails and the core moves right; with interpolation it also reads a
/// segment function not yet fitted, undefined behaviour. A segment that fails to fit removes the unfitted slot after
/// its own, where QuantLib's removes the slot at the grid index after the segment's (<c>:163</c>), past the slots' end
/// once the core's left index is 2 or more, undefined behaviour; every slot either removes is unfitted, so the two
/// agree wherever QuantLib's is in range. And <see cref="AtmLevel"/> is the given or the source's level, where
/// QuantLib's returns it shifted (<c>kahalesmilesection.hpp:142</c>, <c>.cpp:59</c>), so with a shift its vega and its
/// converted volatilities read the forward plus the shift. And a segment whose fitted function's forward runs far above
/// its strikes, to 10¹⁴ and more, so that QuantLib's Black call and constant cancel and its prices are the
/// cancellation's, even below their intrinsic value (<c>:150-173</c>), is solved again, and evaluated, anchored at its
/// left end's price, the Black call's change taken through complementary normal integrals; a segment whose terms round
/// within 1e-8 of its ends' prices, relative, is QuantLib's, solved and evaluated as QuantLib's, at any scale; one the
/// anchored solve can't fit fails as QuantLib's unsolved one, its right point removed.
/// </para>
/// <para>
/// QuantLib's public helper structs (<c>cFunction</c> and the three solvers' functions) are the fit's internals here.
/// </para>
/// </remarks>
public sealed class KahaleSmileSection : SmileSection
{
    // QuantLib's QL_KAHALE_* constants, "still experimental".
    private const double Fmax = NumericLimits.QL_MAX_REAL;
    private const double Smax = 5.0;
    private const double Acc = 1E-12;
    private const double Eps = NumericLimits.QL_EPSILON;

    // The rounding of a segment function's terms, relative to a price, past which QuantLib's evaluation has lost the
    // price to cancellation: QuantLib's own test's tolerance for the section's reproduction of its grid
    // (testKahaleSmileSection), made relative.
    private const double CancellationTolerance = 1E-8;

    private readonly SmileSection source;
    private readonly double atm;
    private readonly double f;
    private readonly double gap;
    private readonly bool interpolate;
    private readonly bool exponentialExtrapolation;
    private readonly int forcedLeftIndex;
    private readonly int forcedRightIndex;

    // The fit, QuantLib's compute(): set while the constructor runs, which reads it back through DigitalOptionPrice
    // for the left wing's check, as QuantLib's does, and never changed after.
    private ImmutableArray<double> k;
    private ImmutableArray<double> c;
    private ImmutableArray<CFunction?> cFunctions;
    private int leftIndex;
    private int rightIndex;

    /// <summary>Initializes a new instance of the <see cref="KahaleSmileSection"/> class.</summary>
    /// <param name="source">The section whose prices it fits, shifted lognormal.</param>
    /// <param name="atm">The at-the-money level; <see langword="null"/>, QuantLib's <c>Null&lt;Real&gt;()</c>, for the
    /// source's own.</param>
    /// <param name="interpolate">Whether the core's prices are replaced by Kahale's segment functions.</param>
    /// <param name="exponentialExtrapolation">Whether the right wing is exponential, rather than a Black call.</param>
    /// <param name="deleteArbitragePoints">Whether the grid points next to the arbitrage-free region are removed
    /// (<see cref="SmileSectionUtils"/>).</param>
    /// <param name="moneynessGrid">The moneyness grid; empty for QuantLib's default.</param>
    /// <param name="gap">The strikes' distance of the source's digitals the wings read.</param>
    /// <param name="forcedLeftIndex">The grid index the core starts at, at the least.</param>
    /// <param name="forcedRightIndex">The grid index the core ends at, at the most.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentException">The source isn't shifted lognormal, or <see cref="SmileSectionUtils"/>
    /// refuses the grid or the level.</exception>
    /// <exception cref="NanoQLException">No arbitrage-free region is found, or a wing can't be fitted short of the
    /// core's other end.</exception>
    public KahaleSmileSection(
        SmileSection source,
        double? atm = null,
        bool interpolate = false,
        bool exponentialExtrapolation = false,
        bool deleteArbitragePoints = false,
        ReadOnlySpan<double> moneynessGrid = default,
        double gap = 1.0E-5,
        int forcedLeftIndex = -1,
        int forcedRightIndex = int.MaxValue)
        : base(Source(source).ExerciseTime, source.DayCounter, source.VolatilityType, source.Shift)
    {
        this.source = source;
        this.gap = gap;
        this.interpolate = interpolate;
        this.exponentialExtrapolation = exponentialExtrapolation;
        this.forcedLeftIndex = forcedLeftIndex;
        this.forcedRightIndex = forcedRightIndex;

        // only shifted lognormal smile sections are supported
        if (!(source.VolatilityType == VolatilityType.ShiftedLognormal))
        {
            throw new ArgumentException("KahaleSmileSection only supports shifted lognormal source sections", nameof(source));
        }

        var ssutils = new SmileSectionUtils(source, moneynessGrid, atm, deleteArbitragePoints);

        // for shifted smile sections we shift the forward and the strikes
        // and do as if we were in a lognormal setting
        double shift = source.Shift;
        k = [.. ssutils.StrikeGrid().Select(strike => strike + shift)];
        c = ssutils.CallPrices();
        this.atm = ssutils.AtmLevel();
        f = this.atm + shift;

        (leftIndex, rightIndex) = ssutils.ArbitragefreeIndices();
        Compute();
    }

    /// <inheritdoc/>
    public override Date? ExerciseDate => source.ExerciseDate;

    /// <inheritdoc/>
    public override double ExerciseTime => source.ExerciseTime;

    /// <inheritdoc/>
    public override DayCounter? DayCounter => source.DayCounter;

    /// <inheritdoc/>
    public override VolatilityType VolatilityType => source.VolatilityType;

    /// <inheritdoc/>
    public override double Shift => source.Shift;

    /// <inheritdoc/>
    public override Date ReferenceDate() => source.ReferenceDate();

    /// <summary>The minimum strike: minus the shift.</summary>
    /// <returns>The strike.</returns>
    public override double MinStrike() => -Shift;

    /// <summary>The maximum strike: the largest <see cref="double"/>.</summary>
    /// <returns>The strike.</returns>
    public override double MaxStrike() => NumericLimits.QL_MAX_REAL;

    /// <summary>The at-the-money level: the one given, or the source's.</summary>
    /// <returns>The level.</returns>
    public override double? AtmLevel() => atm;

    /// <summary>The core's first strike, shifted.</summary>
    /// <returns>The strike.</returns>
    public double LeftCoreStrike() => k[leftIndex];

    /// <summary>The core's last strike, shifted.</summary>
    /// <returns>The strike.</returns>
    public double RightCoreStrike() => k[rightIndex];

    /// <summary>The core's first and last grid index.</summary>
    /// <returns>The indices.</returns>
    public (int Left, int Right) CoreIndices() => (leftIndex, rightIndex);

    /// <summary>
    /// The price of an option: the fitted functions' in the wings, and in the core the source's, or the segment
    /// functions' with interpolation; a put by put-call parity on the shifted strike and forward.
    /// </summary>
    /// <param name="strike">The strike.</param>
    /// <param name="type">Call or put.</param>
    /// <param name="discount">The discount factor.</param>
    /// <returns>The price.</returns>
    public override double OptionPrice(double strike, OptionType type = OptionType.Call, double discount = 1.0)
    {
        // option prices are directly available, so implement this function
        // rather than use smileSection
        // standard implementation
        double shiftedStrike = Max(strike + Shift, Eps);
        int i = Index(shiftedStrike);
        if (interpolate || i == 0 || i == rightIndex - leftIndex + 1)
        {
            double call = cFunctions[i]!.Value.Evaluate(shiftedStrike);
            return discount * (type == OptionType.Call ? call : call + shiftedStrike - f);
        }
        else
        {
            return source.OptionPrice(strike, type, discount);
        }
    }

    /// <inheritdoc/>
    protected override double VolatilityImpl(double strike)
    {
        double shiftedStrike = Max(strike + Shift, Eps);
        int i = Index(shiftedStrike);
        if (!interpolate && !(i == 0 || i == rightIndex - leftIndex + 1))
        {
            return source.Volatility(strike);
        }

        double call = cFunctions[i]!.Value.Evaluate(shiftedStrike);
        double vol = 0.0;
        try
        {
            OptionType type = shiftedStrike >= f ? OptionType.Call : OptionType.Put;
            vol = BlackFormulaImpliedStdDev(type, shiftedStrike, f, type == OptionType.Put ? shiftedStrike - f + call : call)
                / Math.Sqrt(ExerciseTime);
        }
        catch (Exception e) when (e is ArgumentException or NanoQLException)
        {
            // QuantLib's catch (...): the volatility stays 0.
        }

        return vol;
    }

    private static SmileSection Source(SmileSection source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source;
    }

    // std::max(a, b), which keeps a where b is NaN.
    private static double Max(double a, double b) => a < b ? b : a;

    private static bool Caught(Exception e) => e is ArgumentException or NanoQLException;

    // QuantLib's compute(): the left wing, the segments with interpolation, then the right wing.
    private void Compute()
    {
        cFunctions = [.. new CFunction?[rightIndex - leftIndex + 2]];

        // extrapolation in the leftmost interval
        var brent = new Brent();
        bool success;
        double secl = 0.0;

        do
        {
            success = true;
            try
            {
                double k1 = k[leftIndex];
                double c1 = c[leftIndex];
                double c0 = c[0];
                secl = (c[leftIndex] - c[0]) / (k[leftIndex] - k[0]);
                double sec = (c[leftIndex + 1] - c[leftIndex]) / (k[leftIndex + 1] - k[leftIndex]);
                double c1p;
                if (interpolate)
                {
                    c1p = (secl + sec) / 2;
                }
                else
                {
                    c1p = -source.DigitalOptionPrice(k1 - source.Shift + (gap / 2.0), OptionType.Call, 1.0, gap);

                    // can not extrapolate so throw exception which is caught
                    // below
                    Dummy(secl < c1p && c1p <= 0.0);
                }

                var sh1 = new SHelper1(k1, c0, c1, c1p);
                double s = brent.Solve(sh1.Evaluate, Acc, 0.20, 0.00, Smax); // numerical parameters hardcoded here
                sh1.Evaluate(s);
                cFunctions = cFunctions.SetItem(0, new CFunction(sh1.F, s, 0.0, sh1.B));

                // sanity check - in rare cases we can get digitials
                // which are not monotonic or greater than 1.0
                // due to numerical effects. Move to the next index in
                // these cases. The digital is read at the wing's midpoint.
                double dig = DigitalOptionPrice((k1 - (2.0 * source.Shift)) / 2.0, OptionType.Call, 1.0, gap);
                Dummy(dig >= -c1p && dig <= 1.0);
                if (leftIndex < forcedLeftIndex)
                {
                    leftIndex++;
                    success = false;
                }
            }
            catch (Exception e) when (Caught(e))
            {
                leftIndex++;
                success = false;
            }
        }
        while (!success && leftIndex < rightIndex);

        if (!(leftIndex < rightIndex))
        {
            throw new NanoQLException($"can not extrapolate to left, right index of af region reached ({rightIndex})");
        }

        // interpolation
        double cp0 = 0.0, cp1 = 0.0;

        if (interpolate)
        {
            for (int i = leftIndex; i < rightIndex; i++)
            {
                double k0 = k[i];
                double k1 = k[i + 1];
                double c0 = c[i];
                double c1 = c[i + 1];
                double sec = (c[i + 1] - c[i]) / (k[i + 1] - k[i]);
                if (i == leftIndex)
                {
                    cp0 = leftIndex > 0 ? (secl + sec) / 2.0 : sec;
                }

                double secr = i == rightIndex - 1 ? 0.0 : (c[i + 2] - c[i + 1]) / (k[i + 2] - k[i + 1]);
                cp1 = (sec + secr) / 2.0;
                var ah = new AHelper(k0, k1, c0, c1, cp0, cp1);
                double a = 0.0;
                bool valid = false;
                try
                {
                    // numerical parameters hardcoded here
                    a = brent.Solve(ah.Evaluate, Acc, 0.5 * (cp1 + (1.0 + cp0)), cp1 + Eps, 1.0 + cp0 - Eps);
                    valid = true;
                }
                catch (Exception e) when (Caught(e))
                {
                }

                CFunction cFct = default;
                if (valid)
                {
                    ah.Evaluate(a);
                    cFct = new CFunction(ah.F, ah.S, a, ah.B);

                    // a fit whose prices at its ends are lost to cancellation, its forward far above them, is solved
                    // again on the function anchored at its left end; unsolved, it fails as QuantLib's unsolved one
                    if (cFct.Cancels(k0, c0) || cFct.Cancels(k1, c1))
                    {
                        var anchored = new AHelper(k0, k1, c0, c1, cp0, cp1, true);
                        try
                        {
                            a = brent.Solve(anchored.Evaluate, Acc, 0.5 * (cp1 + (1.0 + cp0)), cp1 + Eps, 1.0 + cp0 - Eps);
                            anchored.Evaluate(a);
                            cFct = CFunction.Anchored(anchored.F, anchored.S, a, k0, c0);
                        }
                        catch (Exception e) when (Caught(e))
                        {
                            valid = false;
                        }
                    }
                }

                if (valid)
                {
                    cFunctions = cFunctions.SetItem(Slot(i), cFct);
                    cp0 = cp1;
                }
                else
                {
                    // delete the right point of the interval where we try to
                    // interpolate, and the unfitted slot after this segment's
                    k = k.RemoveAt(i + 1);
                    c = c.RemoveAt(i + 1);
                    cFunctions = cFunctions.RemoveAt(Slot(i) + 1);
                    rightIndex--;
                    i--;
                }
            }
        }

        // extrapolation of right wing
        do
        {
            success = true;
            try
            {
                double k0 = k[rightIndex];
                double c0 = c[rightIndex];
                double rcp0 = interpolate
                    ? 0.5 * (c[rightIndex] - c[rightIndex - 1]) / (k[rightIndex] - k[rightIndex - 1])
                    : -source.DigitalOptionPrice(k0 - Shift - (gap / 2.0), OptionType.Call, 1.0, gap);
                CFunction cFct;
                if (exponentialExtrapolation)
                {
                    Dummy(-rcp0 / c0 > 0.0); // this is caught below
                    cFct = new CFunction(-rcp0 / c0, Math.Log(c0) - (rcp0 / c0 * k0));
                }
                else
                {
                    var sh = new SHelper(k0, c0, rcp0);
                    double s = brent.Solve(sh.Evaluate, Acc, 0.20, 0.0, Smax); // numerical parameters hardcoded here
                    sh.Evaluate(s);
                    cFct = new CFunction(sh.F, s, 0.0, 0.0);
                }

                cFunctions = cFunctions.SetItem(rightIndex - leftIndex + 1, cFct);
            }
            catch (Exception e) when (Caught(e))
            {
                rightIndex--;
                success = false;
            }

            if (rightIndex > forcedRightIndex)
            {
                rightIndex--;
                success = false;
            }
        }
        while (!success && rightIndex > leftIndex);

        if (!(leftIndex < rightIndex))
        {
            throw new NanoQLException($"can not extrapolate to right, left index of af region reached ({leftIndex})");
        }
    }

    // A segment's slot: QuantLib's leftIndex_ > 0 ? i - leftIndex_ + 1 : 0.
    private int Slot(int i) => leftIndex > 0 ? i - leftIndex + 1 : 0;

    // QuantLib's index(strike): the slot of the function priced at a shifted strike.
    private int Index(double strike)
    {
        int i = Interpolation.UpperBound(k.AsSpan(), strike) - leftIndex;
        return Math.Max(Math.Min(i, rightIndex - leftIndex + 1), 0);
    }

    // QuantLib's QL_REQUIRE(condition, "dummy"), caught by the fit.
    private static void Dummy(bool condition)
    {
        if (!condition)
        {
            throw new NanoQLException("dummy");
        }
    }

    // The Black call's change from k0 to k, f·(N(d₁(k)) − N(d₁(k0))) − (k·N(d₂(k)) − k0·N(d₂(k0))), without the
    // cancellation of its terms: each difference of normal integrals near 1 taken through their complements.
    private static double BlackChange(double f, double s, double k0, double k)
    {
        if (s < NumericLimits.QL_EPSILON)
        {
            return Max(f - k, 0.0) - Max(f - k0, 0.0);
        }

        double d10 = (Math.Log(f / k0) / s) + (s / 2.0), d1 = (Math.Log(f / k) / s) + (s / 2.0);
        double d20 = d10 - s, d2 = d1 - s;
        double n1 = d10 >= 0.0 && d1 >= 0.0
            ? NormalFunctions.Cdf(0.0, 1.0, -d10) - NormalFunctions.Cdf(0.0, 1.0, -d1)
            : NormalFunctions.Cdf(0.0, 1.0, d1) - NormalFunctions.Cdf(0.0, 1.0, d10);
        double n2 = d20 >= 0.0 && d2 >= 0.0
            ? k - k0 - ((k * NormalFunctions.Cdf(0.0, 1.0, -d2)) - (k0 * NormalFunctions.Cdf(0.0, 1.0, -d20)))
            : (k * NormalFunctions.Cdf(0.0, 1.0, d2)) - (k0 * NormalFunctions.Cdf(0.0, 1.0, d20));
        return (f * n1) - n2;
    }

    // QuantLib's cFunction: a Black call on a forward f and a standard deviation s, plus a·k + b, or exp(−a·k + b); or,
    // for a segment whose forward runs far above its strikes, the same function anchored at its left end's price c0,
    // c0 + the Black call's change from k0 + a·(k − k0), without the cancellation of the call and the constant.
    private readonly struct CFunction
    {
        private readonly double f, s, a, b, k0, c0;
        private readonly bool exponential, anchored;

        public CFunction(double f, double s, double a, double b)
        {
            this.f = f;
            this.s = s;
            this.a = a;
            this.b = b;
            exponential = false;
        }

        public CFunction(double a, double b)
        {
            this.a = a;
            this.b = b;
            exponential = true;
        }

        private CFunction(double f, double s, double a, double k0, double c0)
        {
            this.f = f;
            this.s = s;
            this.a = a;
            this.k0 = k0;
            this.c0 = c0;
            anchored = true;
        }

        public static CFunction Anchored(double f, double s, double a, double k0, double c0) => new(f, s, a, k0, c0);

        public double Evaluate(double k)
        {
            if (exponential)
            {
                return Math.Exp((-a * k) + b);
            }

            if (anchored)
            {
                return c0 + BlackChange(f, s, k0, k) + (a * (k - k0));
            }

            if (s < NumericLimits.QL_EPSILON)
            {
                return Max(f - k, 0.0) + (a * k) + b;
            }

            double d1 = (Math.Log(f / k) / s) + (s / 2.0);
            double d2 = d1 - s;
            return (f * NormalFunctions.Cdf(0.0, 1.0, d1)) - (k * NormalFunctions.Cdf(0.0, 1.0, d2)) + (a * k) + b;
        }

        // Whether QuantLib's evaluation at k rounds its terms by more than the tolerance of the price c there.
        public bool Cancels(double k, double c)
        {
            double terms;
            if (s < NumericLimits.QL_EPSILON)
            {
                terms = Math.Abs(Max(f - k, 0.0)) + Math.Abs(a * k) + Math.Abs(b);
            }
            else
            {
                double d1 = (Math.Log(f / k) / s) + (s / 2.0);
                double d2 = d1 - s;
                terms = Math.Abs(f * NormalFunctions.Cdf(0.0, 1.0, d1)) + Math.Abs(k * NormalFunctions.Cdf(0.0, 1.0, d2))
                    + Math.Abs(a * k) + Math.Abs(b);
            }

            return !(Eps * terms <= CancellationTolerance * Math.Abs(c));
        }
    }

    // QuantLib's aHelper: a segment's mismatch at its right end for a slope a, its forward, deviation and constant
    // kept from the last evaluation; anchored, the mismatch of the function anchored at its left end, without the
    // cancellation.
    private sealed class AHelper(double k0, double k1, double c0, double c1, double c0p, double c1p, bool anchored = false)
    {
        public double S { get; private set; }

        public double F { get; private set; }

        public double B { get; private set; }

        public double Evaluate(double a)
        {
            double d20 = NormalFunctions.Quantile(0.0, 1.0, -c0p + a);
            double d21 = NormalFunctions.Quantile(0.0, 1.0, -c1p + a);
            double alpha = (d20 - d21) / (Math.Log(k0) - Math.Log(k1));
            double beta = d20 - (alpha * Math.Log(k0));
            S = -1.0 / alpha;
            F = Math.Exp(S * (beta + (S / 2.0)));
            Dummy(F < Fmax); // this is caught
            if (anchored)
            {
                return c0 + BlackChange(F, S, k0, k1) + (a * (k1 - k0)) - c1;
            }

            var cTmp = new CFunction(F, S, a, 0.0);
            B = c0 - cTmp.Evaluate(k0);
            var cf = new CFunction(F, S, a, B);
            return cf.Evaluate(k1) - c1;
        }
    }

    // QuantLib's sHelper: the right wing's mismatch at its left end for a deviation s, its forward kept.
    private sealed class SHelper(double k0, double c0, double c0p)
    {
        public double F { get; private set; }

        public double Evaluate(double s)
        {
            s = Max(s, 0.0);
            double d20 = NormalFunctions.Quantile(0.0, 1.0, -c0p);
            F = k0 * Math.Exp((s * d20) + (s * s / 2.0));
            Dummy(F < Fmax); // this is caught
            var cf = new CFunction(F, s, 0.0, 0.0);
            return cf.Evaluate(k0) - c0;
        }
    }

    // QuantLib's sHelper1: the left wing's mismatch at its right end for a deviation s, its forward and constant kept.
    private sealed class SHelper1(double k1, double c0, double c1, double c1p)
    {
        public double F { get; private set; }

        public double B { get; private set; }

        public double Evaluate(double s)
        {
            s = Max(s, 0.0);
            double d21 = NormalFunctions.Quantile(0.0, 1.0, -c1p);
            F = k1 * Math.Exp((s * d21) + (s * s / 2.0));
            Dummy(F < Fmax); // this is caught
            B = c0 - F;
            var cf = new CFunction(F, s, 0.0, B);
            return cf.Evaluate(k1) - c1;
        }
    }
}
