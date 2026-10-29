// SPDX-FileCopyrightText: Copyright (C) 2010, 2011 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2010 Chris Kenyon
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/inflation/cpicapfloortermpricesurface.hpp ql/experimental/inflation/cpicapfloortermpricesurface.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// CPI cap and floor prices by maturity and strike, filled across all strikes by put-call parity: QuantLib's
/// <c>CPICapFloorTermPriceSurface</c>.
/// </summary>
/// <remarks>
/// <para>
/// A CPI cap or floor has one flow, on cumulative inflation to its maturity, so no stripping is needed, and the index's
/// zero-inflation curve gives the at-the-money level. Strikes are quoted as yearly average inflation and become
/// actual strikes as (1 + quote)^T, as zero-coupon inflation swaps quote; the observation lag is the quoted
/// instruments'.
/// </para>
/// <para>
/// An extension point: <see cref="InterpolatedCPICapFloorTermPriceSurface{TInterpolator2D}"/> derives from it. QuantLib's
/// surface follows the evaluation date (<c>TermStructure(0, …)</c>), so a derived class's factory computes the
/// reference date and passes it in (D15); <see cref="AtmRate"/> and <see cref="Price(Date, double, EvaluationContext)"/>
/// read the index's fixings, from the context. The protected range checks, which QuantLib never calls, are kept; a
/// price past the last maturity or strike extrapolates.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public abstract class CPICapFloorTermPriceSurface : TermStructure
{
    private readonly ImmutableArray<double> cStrikes;
    private readonly ImmutableArray<double> fStrikes;
    private readonly ImmutableArray<Period> cfMaturities;
    private readonly Matrix cPrice;
    private readonly Matrix fPrice;
    private readonly ImmutableArray<double> cfStrikes;

    /// <summary>Initializes a new instance of the <see cref="CPICapFloorTermPriceSurface"/> class.</summary>
    /// <param name="referenceDate">The reference date, the evaluation date adjusted.</param>
    /// <param name="nominal">The prices' nominal.</param>
    /// <param name="baseRate">The at-the-money rate at the reference date, where no inflation time has passed.</param>
    /// <param name="observationLag">The quoted instruments' observation lag.</param>
    /// <param name="cal">The calendar adjusting option dates.</param>
    /// <param name="bdc">The convention adjusting option dates.</param>
    /// <param name="dc">The day counter.</param>
    /// <param name="zii">The zero-inflation index, which must hold a zero-inflation curve.</param>
    /// <param name="interpolationType">How the index is observed.</param>
    /// <param name="yts">The nominal curve.</param>
    /// <param name="cStrikes">The cap strikes, quoted.</param>
    /// <param name="fStrikes">The floor strikes, quoted.</param>
    /// <param name="cfMaturities">The maturities, increasing.</param>
    /// <param name="cPrice">The cap prices, a row per cap strike and a column per maturity.</param>
    /// <param name="fPrice">The floor prices, a row per floor strike and a column per maturity.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its enum.</exception>
    /// <exception cref="NanoQLException">The index has no zero-inflation curve, or the data are too few, mis-sized,
    /// non-positive or not monotonic, with QuantLib's texts.</exception>
    protected CPICapFloorTermPriceSurface(
        Date referenceDate,
        double nominal,
        double baseRate,
        Period observationLag,
        Calendar cal,
        BusinessDayConvention bdc,
        DayCounter dc,
        ZeroInflationIndex zii,
        CPIInterpolationType interpolationType,
        YieldTermStructure yts,
        IReadOnlyList<double> cStrikes,
        IReadOnlyList<double> fStrikes,
        IReadOnlyList<Period> cfMaturities,
        Matrix cPrice,
        Matrix fPrice)
        : base(referenceDate, cal ?? throw new ArgumentNullException(nameof(cal)), dc, 0)
    {
        ArgumentNullException.ThrowIfNull(zii);
        ArgumentNullException.ThrowIfNull(cStrikes);
        ArgumentNullException.ThrowIfNull(fStrikes);
        ArgumentNullException.ThrowIfNull(cfMaturities);
        ArgumentNullException.ThrowIfNull(cPrice);
        ArgumentNullException.ThrowIfNull(fPrice);
        EnumArguments.Check(bdc, nameof(bdc));
        EnumArguments.Check(interpolationType, nameof(interpolationType));

        // does the index have a TS?
        if (zii.ZeroInflationTermStructure is null)
        {
            throw new NanoQLException("ZITS missing from index");
        }

        NominalTermStructure = yts ?? throw new NanoQLException("nominal TS missing");
        ZeroInflationIndex = zii;
        InterpolationType = interpolationType;
        Nominal = nominal;
        BusinessDayConvention = bdc;
        ObservationLag = observationLag;
        BaseRate = baseRate;
        this.cStrikes = [.. cStrikes];
        this.fStrikes = [.. fStrikes];
        this.cfMaturities = [.. cfMaturities];
        this.cPrice = cPrice.Clone();
        this.fPrice = fPrice.Clone();
        cfStrikes = CheckedStrikes(this.cStrikes, this.fStrikes, this.cfMaturities, this.cPrice, this.fPrice, true);
    }

    /// <summary>Gets the quoted instruments' observation lag.</summary>
    public virtual Period ObservationLag { get; }

    /// <summary>Gets the index's frequency.</summary>
    public virtual Frequency Frequency => ZeroInflationIndex.Frequency;

    /// <summary>Gets the at-the-money rate at the reference date.</summary>
    public virtual double BaseRate { get; }

    /// <summary>Gets the prices' nominal.</summary>
    public virtual double Nominal { get; }

    /// <summary>Gets the convention adjusting option dates.</summary>
    public virtual BusinessDayConvention BusinessDayConvention { get; }

    /// <summary>Gets the zero-inflation index.</summary>
    public ZeroInflationIndex ZeroInflationIndex { get; }

    /// <summary>Gets the strikes, the floors' and the caps' above them, quoted.</summary>
    public virtual ImmutableArray<double> Strikes => cfStrikes;

    /// <summary>Gets the cap strikes, quoted.</summary>
    public virtual ImmutableArray<double> CapStrikes => cStrikes;

    /// <summary>Gets the floor strikes, quoted.</summary>
    public virtual ImmutableArray<double> FloorStrikes => fStrikes;

    /// <summary>Gets the maturities.</summary>
    public virtual ImmutableArray<Period> Maturities => cfMaturities;

    /// <summary>Gets the lowest strike.</summary>
    public virtual double MinStrike => cfStrikes[0];

    /// <summary>Gets the highest strike.</summary>
    public virtual double MaxStrike => cfStrikes[^1];

    /// <summary>Gets the earliest maturity's date, the reference date plus the first maturity.</summary>
    public virtual Date MinDate => ReferenceDate + cfMaturities[0];

    /// <inheritdoc/>
    /// <remarks>The reference date plus the last maturity.</remarks>
    public override Date MaxDate => ReferenceDate + cfMaturities[^1];

    /// <summary>Gets how the index is observed.</summary>
    protected CPIInterpolationType InterpolationType { get; }

    /// <summary>Gets the nominal curve.</summary>
    protected YieldTermStructure NominalTermStructure { get; }

    /// <summary>The base date: the index's zero-inflation curve's.</summary>
    /// <returns>The date.</returns>
    public virtual Date BaseDate() => ZeroInflationIndex.ZeroInflationTermStructure!.BaseDate;

    /// <summary>The cap prices, a row per cap strike and a column per maturity.</summary>
    /// <returns>A copy.</returns>
    public virtual Matrix CapPrices() => cPrice.Clone();

    /// <summary>The floor prices, a row per floor strike and a column per maturity.</summary>
    /// <returns>A copy.</returns>
    public virtual Matrix FloorPrices() => fPrice.Clone();

    /// <summary>
    /// The at-the-money rate to a maturity: the index's lagged fixing growth from the reference date, as a yearly rate
    /// over its inflation time; the base rate where no time has passed.
    /// </summary>
    /// <param name="maturity">The maturity.</param>
    /// <param name="context">The evaluation context, whose fixings the index reads.</param>
    /// <returns>The rate.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="NanoQLException">A fixing can't be read or forecast.</exception>
    public double AtmRate(Date maturity, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        double f0 = CPI.LaggedFixing(ZeroInflationIndex, ReferenceDate, ObservationLag, InterpolationType, context);
        double f1 = CPI.LaggedFixing(ZeroInflationIndex, maturity, ObservationLag, InterpolationType, context);
        double t = InflationTermStructureFunctions.InflationYearFraction(
            ZeroInflationIndex.Frequency,
            CPI.IsInterpolated(InterpolationType),
            DayCounter,
            ReferenceDate - ObservationLag,
            maturity - ObservationLag);
        return t > 0.0 ? Math.Pow(f1 / f0, 1 / t) - 1.0 : BaseRate;
    }

    /// <summary>The option date of a tenor: the reference date plus the tenor, adjusted.</summary>
    /// <param name="p">The tenor.</param>
    /// <returns>The date.</returns>
    public virtual Date CpiOptionDateFromTenor(Period p) => Calendar!.Adjust(ReferenceDate + p, BusinessDayConvention);

    /// <summary>The price at a tenor and a quoted strike, a cap's above the at-the-money rate and a floor's below.</summary>
    /// <param name="d">The tenor.</param>
    /// <param name="k">The strike, quoted.</param>
    /// <param name="context">The evaluation context, whose fixings the at-the-money rate reads.</param>
    /// <returns>The price.</returns>
    public virtual double Price(Period d, double k, EvaluationContext context) => Price(CpiOptionDateFromTenor(d), k, context);

    /// <summary>The cap price at a tenor and a quoted strike.</summary>
    /// <param name="d">The tenor.</param>
    /// <param name="k">The strike, quoted.</param>
    /// <returns>The price.</returns>
    public virtual double CapPrice(Period d, double k) => CapPrice(CpiOptionDateFromTenor(d), k);

    /// <summary>The floor price at a tenor and a quoted strike.</summary>
    /// <param name="d">The tenor.</param>
    /// <param name="k">The strike, quoted.</param>
    /// <returns>The price.</returns>
    public virtual double FloorPrice(Period d, double k) => FloorPrice(CpiOptionDateFromTenor(d), k);

    /// <summary>The price at a date and a quoted strike, a cap's above the at-the-money rate and a floor's below.</summary>
    /// <param name="d">The date.</param>
    /// <param name="k">The strike, quoted.</param>
    /// <param name="context">The evaluation context, whose fixings the at-the-money rate reads.</param>
    /// <returns>The price.</returns>
    public abstract double Price(Date d, double k, EvaluationContext context);

    /// <summary>The cap price at a date and a quoted strike.</summary>
    /// <param name="d">The date.</param>
    /// <param name="k">The strike, quoted.</param>
    /// <returns>The price.</returns>
    public abstract double CapPrice(Date d, double k);

    /// <summary>The floor price at a date and a quoted strike.</summary>
    /// <param name="d">The date.</param>
    /// <param name="k">The strike, quoted.</param>
    /// <returns>The price.</returns>
    public abstract double FloorPrice(Date d, double k);

    /// <summary>Whether a strike lies within the surface's strikes: QuantLib's protected check, which nothing
    /// calls.</summary>
    /// <param name="k">The strike.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    protected virtual bool CheckStrike(double k) => MinStrike <= k && k <= MaxStrike;

    /// <summary>Whether a date lies within the surface's maturities: QuantLib's protected check, which nothing
    /// calls.</summary>
    /// <param name="d">The date.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    protected virtual bool CheckMaturity(Date d) => MinDate <= d && d <= MaxDate;

    // QuantLib's constructor's checks, and the strikes: the floors' and the caps' above the last floor strike. The CPI
    // surface's text for non-decreasing cap prices shows them, the YoY surface's doesn't.
    internal static ImmutableArray<double> CheckedStrikes(
        ImmutableArray<double> cStrikes,
        ImmutableArray<double> fStrikes,
        ImmutableArray<Period> cfMaturities,
        Matrix cPrice,
        Matrix fPrice,
        bool showCapPrices)
    {
        // data consistency checking, enough data?
        Require(fStrikes.Length > 1, "not enough floor strikes");
        Require(cStrikes.Length > 1, "not enough cap strikes");
        Require(cfMaturities.Length > 1, "not enough maturities");
        Require(fStrikes.Length == fPrice.Rows, "floor strikes vs floor price rows not equal");
        Require(cStrikes.Length == cPrice.Rows, "cap strikes vs cap price rows not equal");
        Require(cfMaturities.Length == fPrice.Columns, "maturities vs floor price columns not equal");
        Require(cfMaturities.Length == cPrice.Columns, "maturities vs cap price columns not equal");

        // data has correct properties (positive, monotonic)?
        for (int j = 0; j < cfMaturities.Length; j++)
        {
            Require(cfMaturities[j] > new Period(0, TimeUnit.Days), "non-positive maturities");
            if (j > 0)
            {
                Require(cfMaturities[j] > cfMaturities[j - 1], "non-increasing maturities");
            }

            for (int i = 0; i < fPrice.Rows; i++)
            {
                Require(fPrice[i, j] > 0.0, $"non-positive floor price: {Show(fPrice[i, j])}");
                if (i > 0)
                {
                    Require(fPrice[i, j] >= fPrice[i - 1, j], "non-increasing floor prices");
                }
            }

            for (int i = 0; i < cPrice.Rows; i++)
            {
                Require(cPrice[i, j] > 0.0, $"non-positive cap price: {Show(cPrice[i, j])}");
                if (i > 0)
                {
                    if (!(cPrice[i, j] <= cPrice[i - 1, j]))
                    {
                        throw new NanoQLException(showCapPrices
                            ? $"non-decreasing cap prices: {Show(cPrice[i, j])} then {Show(cPrice[i - 1, j])}"
                            : "non-decreasing cap prices");
                    }
                }
            }
        }

        // Get the set of strikes, noting that repeats, overlaps are
        // expected between caps and floors but that no overlap in the
        // output is allowed so no repeats or overlaps are used
        var strikes = ImmutableArray.CreateBuilder<double>();
        strikes.AddRange(fStrikes);
        const double Eps = 0.0000001;
        double maxFstrike = fStrikes[^1];
        foreach (double k in cStrikes)
        {
            if (k > maxFstrike + Eps)
            {
                strikes.Add(k);
            }
        }

        // final consistency checking
        Require(strikes.Count > 2, "overall not enough strikes");
        for (int i = 1; i < strikes.Count; i++)
        {
            Require(strikes[i] > strikes[i - 1], "cfStrikes not increasing");
        }

        return strikes.ToImmutable();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new NanoQLException(message);
        }
    }

    private static string Show(double x) => Utilities.DataFormattersFunctions.General(x);
}
