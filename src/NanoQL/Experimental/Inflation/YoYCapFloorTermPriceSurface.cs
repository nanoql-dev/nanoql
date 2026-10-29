// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2009 Bernd Engelmann
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/inflation/yoycapfloortermpricesurface.hpp ql/experimental/inflation/yoycapfloortermpricesurface.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// Year-on-year inflation cap and floor prices by maturity and strike, with the at-the-money year-on-year swap rates
/// and the year-on-year curve they imply: QuantLib's <c>YoYCapFloorTermPriceSurface</c>.
/// </summary>
/// <remarks>
/// <para>
/// An extension point: <see cref="InterpolatedYoYCapFloorTermPriceSurface{TInterpolator2D, TInterpolator1D}"/> derives
/// from it. QuantLib's surface follows the evaluation date (<c>TermStructure(0, …)</c>), so a derived class's factory
/// computes the reference date and passes it in (D15). An option date is the reference date plus its tenor, unadjusted,
/// as QuantLib's. The protected range checks, which QuantLib never calls, are kept; a price past the last maturity or
/// strike extrapolates.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public abstract class YoYCapFloorTermPriceSurface : TermStructure
{
    private readonly ImmutableArray<double> cStrikes;
    private readonly ImmutableArray<double> fStrikes;
    private readonly ImmutableArray<Period> cfMaturities;
    private readonly Matrix cPrice;
    private readonly Matrix fPrice;
    private readonly ImmutableArray<double> cfStrikes;

    /// <summary>Initializes a new instance of the <see cref="YoYCapFloorTermPriceSurface"/> class.</summary>
    /// <param name="referenceDate">The reference date, the evaluation date adjusted.</param>
    /// <param name="fixingDays">The fixing days.</param>
    /// <param name="yyLag">The quoted instruments' observation lag.</param>
    /// <param name="yii">The year-on-year index.</param>
    /// <param name="interpolation">How the index is observed.</param>
    /// <param name="nominal">The nominal curve.</param>
    /// <param name="dc">The day counter.</param>
    /// <param name="cal">The calendar.</param>
    /// <param name="bdc">The business-day convention.</param>
    /// <param name="cStrikes">The cap strikes.</param>
    /// <param name="fStrikes">The floor strikes.</param>
    /// <param name="cfMaturities">The maturities, increasing.</param>
    /// <param name="cPrice">The cap prices, a row per cap strike and a column per maturity.</param>
    /// <param name="fPrice">The floor prices, a row per floor strike and a column per maturity.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its enum.</exception>
    /// <exception cref="NanoQLException">The data are too few, mis-sized, non-positive or not monotonic, with QuantLib's
    /// texts.</exception>
    protected YoYCapFloorTermPriceSurface(
        Date referenceDate,
        int fixingDays,
        Period yyLag,
        YoYInflationIndex yii,
        CPIInterpolationType interpolation,
        YieldTermStructure nominal,
        DayCounter dc,
        Calendar cal,
        BusinessDayConvention bdc,
        IReadOnlyList<double> cStrikes,
        IReadOnlyList<double> fStrikes,
        IReadOnlyList<Period> cfMaturities,
        Matrix cPrice,
        Matrix fPrice)
        : base(referenceDate, cal ?? throw new ArgumentNullException(nameof(cal)), dc, 0)
    {
        ArgumentNullException.ThrowIfNull(yii);
        ArgumentNullException.ThrowIfNull(nominal);
        ArgumentNullException.ThrowIfNull(cStrikes);
        ArgumentNullException.ThrowIfNull(fStrikes);
        ArgumentNullException.ThrowIfNull(cfMaturities);
        ArgumentNullException.ThrowIfNull(cPrice);
        ArgumentNullException.ThrowIfNull(fPrice);
        EnumArguments.Check(interpolation, nameof(interpolation));
        EnumArguments.Check(bdc, nameof(bdc));
        FixingDays = fixingDays;
        BusinessDayConvention = bdc;
        YoYIndex = yii;
        ObservationLag = yyLag;
        NominalTermStructure = nominal;
        IndexIsInterpolated = CPI.IsInterpolated(interpolation);
        this.cStrikes = [.. cStrikes];
        this.fStrikes = [.. fStrikes];
        this.cfMaturities = [.. cfMaturities];
        this.cPrice = cPrice.Clone();
        this.fPrice = fPrice.Clone();
        cfStrikes = CPICapFloorTermPriceSurface.CheckedStrikes(
            this.cStrikes, this.fStrikes, this.cfMaturities, this.cPrice, this.fPrice, false);
    }

    /// <summary>Gets a value indicating whether the index is observed with linear interpolation.</summary>
    public bool IndexIsInterpolated { get; }

    /// <summary>Gets the quoted instruments' observation lag.</summary>
    public virtual Period ObservationLag { get; }

    /// <summary>Gets the index's frequency.</summary>
    public virtual Frequency Frequency => YoYIndex.Frequency;

    /// <summary>Gets the year-on-year index.</summary>
    public YoYInflationIndex YoYIndex { get; }

    /// <summary>Gets the business-day convention.</summary>
    public virtual BusinessDayConvention BusinessDayConvention { get; }

    /// <summary>Gets the fixing days.</summary>
    public virtual int FixingDays { get; }

    /// <summary>Gets the strikes, the floors' and the caps' above them.</summary>
    public virtual ImmutableArray<double> Strikes => cfStrikes;

    /// <summary>Gets the cap strikes.</summary>
    public virtual ImmutableArray<double> CapStrikes => cStrikes;

    /// <summary>Gets the floor strikes.</summary>
    public virtual ImmutableArray<double> FloorStrikes => fStrikes;

    /// <summary>Gets the maturities.</summary>
    public virtual ImmutableArray<Period> Maturities => cfMaturities;

    /// <summary>Gets the lowest strike.</summary>
    public virtual double MinStrike => cfStrikes[0];

    /// <summary>Gets the highest strike.</summary>
    public virtual double MaxStrike => cfStrikes[^1];

    /// <summary>Gets the earliest maturity's date, the reference date plus the first maturity.</summary>
    public virtual Date MinMaturity => ReferenceDate + cfMaturities[0];

    /// <summary>Gets the last maturity's date, the reference date plus the last maturity.</summary>
    public virtual Date MaxMaturity => ReferenceDate + cfMaturities[^1];

    /// <summary>Gets the nominal curve.</summary>
    protected YieldTermStructure NominalTermStructure { get; }

    /// <summary>The cap prices, a row per cap strike and a column per maturity.</summary>
    /// <returns>A copy.</returns>
    protected Matrix CapPrices() => cPrice.Clone();

    /// <summary>The floor prices, a row per floor strike and a column per maturity.</summary>
    /// <returns>A copy.</returns>
    protected Matrix FloorPrices() => fPrice.Clone();

    /// <summary>The at-the-money year-on-year swap rates by maturity time, from put-call parity on the price
    /// data.</summary>
    /// <returns>The times and rates.</returns>
    public abstract (ImmutableArray<double> Times, ImmutableArray<double> Rates) AtmYoYSwapTimeRates();

    /// <summary>The at-the-money year-on-year swap rates by maturity date, from put-call parity on the price
    /// data.</summary>
    /// <returns>The dates and rates.</returns>
    public abstract (ImmutableArray<Date> Dates, ImmutableArray<double> Rates) AtmYoYSwapDateRates();

    /// <summary>The year-on-year curve the swap rates imply.</summary>
    /// <returns>The curve.</returns>
    public abstract YoYInflationTermStructure YoYTS();

    /// <summary>The base date.</summary>
    /// <returns>The date.</returns>
    public abstract Date BaseDate();

    /// <summary>The price at a date and strike, a cap's above the at-the-money swap rate and a floor's below.</summary>
    /// <param name="d">The date.</param>
    /// <param name="k">The strike.</param>
    /// <returns>The price.</returns>
    public abstract double Price(Date d, double k);

    /// <summary>The cap price at a date and strike.</summary>
    /// <param name="d">The date.</param>
    /// <param name="k">The strike.</param>
    /// <returns>The price.</returns>
    public abstract double CapPrice(Date d, double k);

    /// <summary>The floor price at a date and strike.</summary>
    /// <param name="d">The date.</param>
    /// <param name="k">The strike.</param>
    /// <returns>The price.</returns>
    public abstract double FloorPrice(Date d, double k);

    /// <summary>The at-the-money year-on-year swap rate to a date.</summary>
    /// <param name="d">The date.</param>
    /// <param name="extrapolate">Whether to extrapolate past the swap rates' times.</param>
    /// <returns>The rate.</returns>
    public abstract double AtmYoYSwapRate(Date d, bool extrapolate = true);

    /// <summary>The at-the-money year-on-year rate at a date, read on the year-on-year curve an observation lag
    /// earlier.</summary>
    /// <param name="d">The date.</param>
    /// <param name="obsLag">The observation lag; <see langword="null"/>, or QuantLib's −1 days, for the
    /// surface's.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum date.</param>
    /// <returns>The rate.</returns>
    public abstract double AtmYoYRate(Date d, Period? obsLag = null, bool extrapolate = true);

    /// <summary>The price at a tenor and strike.</summary>
    /// <param name="d">The tenor.</param>
    /// <param name="k">The strike.</param>
    /// <returns>The price.</returns>
    public virtual double Price(Period d, double k) => Price(YoYOptionDateFromTenor(d), k);

    /// <summary>The cap price at a tenor and strike.</summary>
    /// <param name="d">The tenor.</param>
    /// <param name="k">The strike.</param>
    /// <returns>The price.</returns>
    public virtual double CapPrice(Period d, double k) => CapPrice(YoYOptionDateFromTenor(d), k);

    /// <summary>The floor price at a tenor and strike.</summary>
    /// <param name="d">The tenor.</param>
    /// <param name="k">The strike.</param>
    /// <returns>The price.</returns>
    public virtual double FloorPrice(Period d, double k) => FloorPrice(YoYOptionDateFromTenor(d), k);

    /// <summary>The at-the-money year-on-year swap rate to a tenor.</summary>
    /// <param name="d">The tenor.</param>
    /// <param name="extrapolate">Whether to extrapolate past the swap rates' times.</param>
    /// <returns>The rate.</returns>
    public virtual double AtmYoYSwapRate(Period d, bool extrapolate = true) =>
        AtmYoYSwapRate(YoYOptionDateFromTenor(d), extrapolate);

    /// <summary>The at-the-money year-on-year rate at a tenor.</summary>
    /// <param name="d">The tenor.</param>
    /// <param name="obsLag">The observation lag; <see langword="null"/>, or QuantLib's −1 days, for the
    /// surface's.</param>
    /// <param name="extrapolate">Whether to extrapolate past the curve's maximum date.</param>
    /// <returns>The rate.</returns>
    public virtual double AtmYoYRate(Period d, Period? obsLag = null, bool extrapolate = true) =>
        AtmYoYRate(YoYOptionDateFromTenor(d), obsLag, extrapolate);

    /// <summary>The option date of a tenor: the reference date plus the tenor.</summary>
    /// <param name="p">The tenor.</param>
    /// <returns>The date.</returns>
    public virtual Date YoYOptionDateFromTenor(Period p) => ReferenceDate + p;

    /// <summary>Whether a strike lies within the surface's strikes: QuantLib's protected check, which nothing
    /// calls.</summary>
    /// <param name="k">The strike.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    protected virtual bool CheckStrike(double k) => MinStrike <= k && k <= MaxStrike;

    /// <summary>Whether a date lies within the surface's maturities: QuantLib's protected check, which nothing
    /// calls.</summary>
    /// <param name="d">The date.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    protected virtual bool CheckMaturity(Date d) => MinMaturity <= d && d <= MaxMaturity;
}
