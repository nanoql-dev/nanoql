// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The strippers' facts' setup, the cap survey's probe p40's: on the context's date, TARGET, a flat 4% Actual/365 (Fixed)
/// curve on that date, Euribor 6M on it, and a 4 × 4 surface of cap term volatilities (1Y, 2Y, 3Y, 5Y by 2%, 3%, 4%,
/// 5%), with no settlement days unless given.
/// </summary>
internal static class StripperFixture
{
    public static readonly Period[] Tenors =
    [
        new(1, TimeUnit.Years), new(2, TimeUnit.Years), new(3, TimeUnit.Years), new(5, TimeUnit.Years),
    ];

    public static readonly double[] Strikes = [0.02, 0.03, 0.04, 0.05];

    public static readonly double[,] Vols =
    {
        { 0.30, 0.25, 0.22, 0.21 }, { 0.28, 0.24, 0.21, 0.20 }, { 0.26, 0.23, 0.20, 0.19 }, { 0.24, 0.21, 0.19, 0.18 },
    };

    public static CapFloorTermVolSurface Surface(EvaluationContext context, double[,]? vols = null, int settlementDays = 0) =>
        CapFloorTermVolSurface.Create(
            settlementDays, new TARGET(), BusinessDayConvention.Following, Tenors, Strikes, new Matrix(vols ?? Vols),
            context, new Actual365Fixed());

    public static IborIndex Index(EvaluationContext context) =>
        new Euribor6M(new FlatForward(context.EvaluationDate, 0.04, new Actual365Fixed()));

    public static OptionletStripper1 Strip(
        EvaluationContext context, double[,]? vols = null, bool dontThrow = false, int settlementDays = 0) =>
        new(Surface(context, vols, settlementDays), Index(context), context, dontThrow: dontThrow);
}
