// SPDX-FileCopyrightText: Copyright (C) 2005 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/optionlet/capletvariancecurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A caplet volatility curve: the caplet volatilities by date, interpolated in variance by a
/// <see cref="BlackVarianceCurve"/> (not forced monotone), the same at every strike.
/// </summary>
/// <remarks>
/// One of QuantLib's defects is fixed: QuantLib's smile sections are flat shifted-lognormal sections with no shift
/// whatever the curve's type and displacement, so a normal curve's section prices as lognormal; here they carry the
/// curve's <see cref="VolatilityType"/> and <see cref="Displacement"/>.
/// </remarks>
public sealed class CapletVarianceCurve : OptionletVolatilityStructure
{
    private readonly BlackVarianceCurve blackCurve;
    private readonly VolatilityType type;
    private readonly double displacement;

    /// <summary>Initializes a new instance of the <see cref="CapletVarianceCurve"/> class.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="dates">The caplets' dates, sorted, after the reference date.</param>
    /// <param name="capletVolCurve">The caplet volatilities, one per date.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="type">The volatilities' type.</param>
    /// <param name="displacement">The displacement of shifted lognormal volatilities.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">The variance curve refuses the dates or volatilities.</exception>
    public CapletVarianceCurve(
        Date referenceDate,
        IEnumerable<Date> dates,
        IEnumerable<double> capletVolCurve,
        DayCounter dayCounter,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double displacement = 0.0)
        : base(referenceDate, null, BusinessDayConvention.Following, dayCounter)
    {
        EnumArguments.Check(type, nameof(type));
        blackCurve = new BlackVarianceCurve(referenceDate, dates, capletVolCurve, dayCounter, false);
        this.type = type;
        this.displacement = displacement;
    }

    /// <inheritdoc/>
    public override Date MaxDate => blackCurve.MaxDate;

    /// <inheritdoc/>
    public override VolatilityType VolatilityType => type;

    /// <inheritdoc/>
    public override double Displacement => displacement;

    /// <inheritdoc/>
    public override double MinStrike() => blackCurve.MinStrike();

    /// <inheritdoc/>
    public override double MaxStrike() => blackCurve.MaxStrike();

    /// <summary>A flat smile section at the option time, of the at-the-money volatility (QuantLib's dummy strike
    /// 0.05), with the curve's type and displacement.</summary>
    /// <param name="optionTime">The option time.</param>
    /// <returns>The section.</returns>
    protected override SmileSection SmileSectionImpl(double optionTime)
    {
        // dummy strike
        double atmVol = blackCurve.BlackVol(optionTime, 0.05, true);
        return new FlatSmileSection(optionTime, atmVol, DayCounter, null, type, displacement);
    }

    /// <inheritdoc/>
    protected override double VolatilityImpl(double optionTime, double strike) => blackCurve.BlackVol(optionTime, strike, true);
}
