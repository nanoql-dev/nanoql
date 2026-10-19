// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2006 Giorgio Facchinetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/swaptionvolstructuresutilities.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

/// <summary>
/// The swaption volatility test data QuantLib's swaption volatility structure tests share: the market conventions, an
/// at-the-money volatility matrix and a volatility cube's spreads. It holds no cases.
/// </summary>
/// <remarks>
/// QuantLib's data also hold each value as a quote handle (<c>volsHandle</c>, <c>volSpreadsHandle</c>); a quote is a
/// value here, so the matrices are the data.
/// </remarks>
internal static class SwaptionVolStructuresUtilitiesTests
{
    internal sealed class SwaptionTenors
    {
        public List<Period> Options { get; } = [];

        public List<Period> Swaps { get; } = [];
    }

    internal sealed class SwaptionMarketConventions
    {
        public Calendar Calendar { get; private set; } = new NullCalendar();

        public BusinessDayConvention OptionBdc { get; private set; }

        public DayCounter DayCounter { get; private set; } = new Actual365Fixed();

        public void SetConventions()
        {
            Calendar = new TARGET();
            OptionBdc = BusinessDayConvention.ModifiedFollowing;
            DayCounter = new Actual365Fixed();
        }
    }

    internal sealed class AtmVolatility
    {
        public SwaptionTenors Tenors { get; } = new();

        public Matrix Vols { get; private set; } = new();

        public void SetMarketData()
        {
            Tenors.Options.Clear();
            Tenors.Options.AddRange(
            [
                new Period(1, TimeUnit.Months),
                new Period(6, TimeUnit.Months),
                new Period(1, TimeUnit.Years),
                new Period(5, TimeUnit.Years),
                new Period(10, TimeUnit.Years),
                new Period(30, TimeUnit.Years),
            ]);
            Tenors.Swaps.Clear();
            Tenors.Swaps.AddRange(
            [
                new Period(1, TimeUnit.Years),
                new Period(5, TimeUnit.Years),
                new Period(10, TimeUnit.Years),
                new Period(30, TimeUnit.Years),
            ]);
            Vols = new Matrix(new[,]
            {
                { 0.1300, 0.1560, 0.1390, 0.1220 },
                { 0.1440, 0.1580, 0.1460, 0.1260 },
                { 0.1600, 0.1590, 0.1470, 0.1290 },
                { 0.1640, 0.1470, 0.1370, 0.1220 },
                { 0.1400, 0.1300, 0.1250, 0.1100 },
                { 0.1130, 0.1090, 0.1070, 0.0930 },
            });
        }
    }

    internal sealed class VolatilityCube
    {
        public SwaptionTenors Tenors { get; } = new();

        public Matrix VolSpreads { get; private set; } = new();

        public List<Spread> StrikeSpreads { get; } = [];

        public void SetMarketData()
        {
            Tenors.Options.Clear();
            Tenors.Options.AddRange(
            [
                new Period(1, TimeUnit.Years),
                new Period(10, TimeUnit.Years),
                new Period(30, TimeUnit.Years),
            ]);
            Tenors.Swaps.Clear();
            Tenors.Swaps.AddRange(
            [
                new Period(2, TimeUnit.Years),
                new Period(10, TimeUnit.Years),
                new Period(30, TimeUnit.Years),
            ]);
            StrikeSpreads.Clear();
            StrikeSpreads.AddRange([-0.020, -0.005, +0.000, +0.005, +0.020]);
            VolSpreads = new Matrix(new[,]
            {
                { 0.0599, 0.0049, 0.0000, -0.0001, 0.0127 },
                { 0.0729, 0.0086, 0.0000, -0.0024, 0.0098 },
                { 0.0738, 0.0102, 0.0000, -0.0039, 0.0065 },
                { 0.0465, 0.0063, 0.0000, -0.0032, -0.0010 },
                { 0.0558, 0.0084, 0.0000, -0.0050, -0.0057 },
                { 0.0576, 0.0083, 0.0000, -0.0043, -0.0014 },
                { 0.0437, 0.0059, 0.0000, -0.0030, -0.0006 },
                { 0.0533, 0.0078, 0.0000, -0.0045, -0.0046 },
                { 0.0545, 0.0079, 0.0000, -0.0042, -0.0020 },
            });
        }
    }
}
