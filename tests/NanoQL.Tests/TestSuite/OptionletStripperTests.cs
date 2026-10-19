// SPDX-FileCopyrightText: Copyright (C) 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 Laurent Hoffmann
// SPDX-FileCopyrightText: Copyright (C) 2015, 2016 Michael von den Driesch
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/optionletstripper.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Utilities;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

// Adapted, throughout: the evaluation date a case sets is its context's; a relinkable handle is the curve itself,
// built first, with the index on it; a structure built on settlement days, whose reference date QuantLib moves, is its
// Create factory under the context; StrippedOptionletAdapter's constructor is Create(stripper, context), and
// enableExtrapolation() a copy with extrapolation enabled; each engine is a static Calculate; MakeCapFloor's
// withPricingEngine is the engine's call on its ToCapFloor(context); a stripper's lazy strip runs at construction, so
// a first query's throw or no-throw is asserted at construction; an index fixing added is a context's fixing.
[Trait("Oracle", "QuantLib")]
public class OptionletStripperTests
{
    [Fact]
    public void TestFlatTermVolatilityStripping1()
    {
        // Testing forward/forward vol stripping from flat term vol surface using OptionletStripper1 class...

        var vars = new CommonVars(new Date(28, October, 2013));
        vars.SetFlatTermVolSurface();

        var iborIndex = new Euribor6M(vars.YieldTermStructure);

        OptionletStripper optionletStripper1 = new OptionletStripper1(
            vars.FlatTermVolSurface, iborIndex, vars.Context, null, vars.Accuracy);

        OptionletVolatilityStructure vol =
            StrippedOptionletAdapter.Create(optionletStripper1, vars.Context).EnableExtrapolation();

        for (Size tenorIndex = 0; tenorIndex < vars.OptionTenors.Length; ++tenorIndex)
        {
            for (Size strikeIndex = 0; strikeIndex < vars.Strikes.Length; ++strikeIndex)
            {
                CapFloor cap = new MakeCapFloor(CapFloorType.Cap, vars.OptionTenors[tenorIndex], iborIndex)
                    .WithStrike(vars.Strikes[strikeIndex])
                    .ToCapFloor(vars.Context);

                Real priceFromStrippedVolatility =
                    BlackCapFloorEngine.Calculate(cap, vars.Context, vars.YieldTermStructure, vol).NPV!.Value;

                Real priceFromConstantVolatility = BlackCapFloorEngine.Calculate(
                    cap, vars.Context, vars.YieldTermStructure, vars.TermV[tenorIndex, strikeIndex]).NPV!.Value;

                Real error = Math.Abs(priceFromStrippedVolatility - priceFromConstantVolatility);
                if (error > vars.Tolerance)
                {
                    Assert.Fail(PriceMessage(
                        vars.OptionTenors[tenorIndex], vars.Strikes[strikeIndex], priceFromStrippedVolatility,
                        priceFromConstantVolatility, error, vars.Tolerance));
                }
            }
        }
    }

    [Fact]
    public void TestTermVolatilityStripping1()
    {
        // Testing forward/forward vol stripping from non-flat term vol surface using OptionletStripper1 class...

        var vars = new CommonVars(new Date(28, October, 2013));
        vars.SetCapFloorTermVolSurface();

        var iborIndex = new Euribor6M(vars.YieldTermStructure);

        OptionletStripper optionletStripper1 = new OptionletStripper1(
            vars.CapFloorVolSurface, iborIndex, vars.Context, null, vars.Accuracy);

        OptionletVolatilityStructure vol =
            StrippedOptionletAdapter.Create(optionletStripper1, vars.Context).EnableExtrapolation();

        for (Size tenorIndex = 0; tenorIndex < vars.OptionTenors.Length; ++tenorIndex)
        {
            for (Size strikeIndex = 0; strikeIndex < vars.Strikes.Length; ++strikeIndex)
            {
                CapFloor cap = new MakeCapFloor(CapFloorType.Cap, vars.OptionTenors[tenorIndex], iborIndex)
                    .WithStrike(vars.Strikes[strikeIndex])
                    .ToCapFloor(vars.Context);

                Real priceFromStrippedVolatility =
                    BlackCapFloorEngine.Calculate(cap, vars.Context, vars.YieldTermStructure, vol).NPV!.Value;

                Real priceFromConstantVolatility = BlackCapFloorEngine.Calculate(
                    cap, vars.Context, vars.YieldTermStructure, vars.TermV[tenorIndex, strikeIndex]).NPV!.Value;

                Real error = Math.Abs(priceFromStrippedVolatility - priceFromConstantVolatility);
                if (error > vars.Tolerance)
                {
                    Assert.Fail(PriceMessage(
                        vars.OptionTenors[tenorIndex], vars.Strikes[strikeIndex], priceFromStrippedVolatility,
                        priceFromConstantVolatility, error, vars.Tolerance));
                }
            }
        }
    }

    [Fact]
    public void TestTermVolatilityStrippingNormalVol()
    {
        // Testing forward/forward vol stripping from non-flat normal vol term vol surface for normal vol setup using
        // OptionletStripper1 class...

        var vars = new CommonVars(new Date(30, April, 2015));
        vars.SetRealCapFloorTermVolSurface();

        var iborIndex = new Euribor6M(vars.ForwardingYTS);

        OptionletStripper optionletStripper1 = new OptionletStripper1(
            vars.CapFloorVolRealSurface, iborIndex, vars.Context, null, vars.Accuracy, 100, vars.DiscountingYTS,
            VolatilityType.Normal);

        OptionletVolatilityStructure vol =
            StrippedOptionletAdapter.Create(optionletStripper1, vars.Context).EnableExtrapolation();

        for (Size tenorIndex = 0; tenorIndex < vars.OptionTenors.Length; ++tenorIndex)
        {
            for (Size strikeIndex = 0; strikeIndex < vars.Strikes.Length; ++strikeIndex)
            {
                CapFloor cap = new MakeCapFloor(CapFloorType.Cap, vars.OptionTenors[tenorIndex], iborIndex)
                    .WithStrike(vars.Strikes[strikeIndex])
                    .ToCapFloor(vars.Context);

                Real priceFromStrippedVolatility =
                    BachelierCapFloorEngine.Calculate(cap, vars.Context, vars.DiscountingYTS!, vol).NPV!.Value;

                Real priceFromConstantVolatility = BachelierCapFloorEngine.Calculate(
                    cap, vars.Context, vars.DiscountingYTS!, vars.TermV[tenorIndex, strikeIndex]).NPV!.Value;

                Real error = Math.Abs(priceFromStrippedVolatility - priceFromConstantVolatility);
                if (error > vars.Tolerance)
                {
                    Assert.Fail(PriceMessage(
                        vars.OptionTenors[tenorIndex], vars.Strikes[strikeIndex], priceFromStrippedVolatility,
                        priceFromConstantVolatility, error, vars.Tolerance));
                }
            }
        }
    }

    [Fact]
    public void TestTermVolatilityStrippingShiftedLogNormalVol()
    {
        // Testing forward/forward vol stripping from non-flat normal vol term vol surface for normal vol setup using
        // OptionletStripper1 class...

        var vars = new CommonVars(new Date(30, April, 2015));
        Real shift = 0.03;
        vars.SetRealCapFloorTermVolSurface();

        var iborIndex = new Euribor6M(vars.ForwardingYTS);

        OptionletStripper optionletStripper1 = new OptionletStripper1(
            vars.CapFloorVolRealSurface, iborIndex, vars.Context, null, vars.Accuracy, 100, vars.DiscountingYTS,
            VolatilityType.ShiftedLognormal, shift, true);

        OptionletVolatilityStructure vol =
            StrippedOptionletAdapter.Create(optionletStripper1, vars.Context).EnableExtrapolation();

        for (Size strikeIndex = 0; strikeIndex < vars.Strikes.Length; ++strikeIndex)
        {
            for (Size tenorIndex = 0; tenorIndex < vars.OptionTenors.Length; ++tenorIndex)
            {
                CapFloor cap = new MakeCapFloor(CapFloorType.Cap, vars.OptionTenors[tenorIndex], iborIndex)
                    .WithStrike(vars.Strikes[strikeIndex])
                    .ToCapFloor(vars.Context);

                Real priceFromStrippedVolatility =
                    BlackCapFloorEngine.Calculate(cap, vars.Context, vars.DiscountingYTS!, vol).NPV!.Value;

                Real priceFromConstantVolatility = BlackCapFloorEngine.Calculate(
                    cap, vars.Context, vars.DiscountingYTS!, vars.TermV[tenorIndex, strikeIndex],
                    vars.CapFloorVolRealSurface.DayCounter, shift).NPV!.Value;

                Real error = Math.Abs(priceFromStrippedVolatility - priceFromConstantVolatility);
                if (error > vars.Tolerance)
                {
                    Assert.Fail(PriceMessage(
                        vars.OptionTenors[tenorIndex], vars.Strikes[strikeIndex], priceFromStrippedVolatility,
                        priceFromConstantVolatility, error, vars.Tolerance));
                }
            }
        }
    }

    [Fact]
    public void TestFlatTermVolatilityStripping2()
    {
        // Testing forward/forward vol stripping from flat term vol surface using OptionletStripper2 class...

        var vars = new CommonVars(new Date(28, October, 2013));
        vars.SetFlatTermVolCurve();
        vars.SetFlatTermVolSurface();

        var iborIndex = new Euribor6M(vars.YieldTermStructure);

        // optionletstripper1
        var optionletStripper1 = new OptionletStripper1(
            vars.FlatTermVolSurface, iborIndex, vars.Context, null, vars.Accuracy);

        OptionletVolatilityStructure vol1 =
            StrippedOptionletAdapter.Create(optionletStripper1, vars.Context).EnableExtrapolation();

        // optionletstripper2
        OptionletStripper optionletStripper2 =
            new OptionletStripper2(optionletStripper1, vars.FlatTermVolCurve, vars.Context);

        OptionletVolatilityStructure vol2 =
            StrippedOptionletAdapter.Create(optionletStripper2, vars.Context).EnableExtrapolation();

        // consistency check: diff(stripped vol1-stripped vol2)
        for (Size strikeIndex = 0; strikeIndex < vars.Strikes.Length; ++strikeIndex)
        {
            for (Size tenorIndex = 0; tenorIndex < vars.OptionTenors.Length; ++tenorIndex)
            {
                Volatility strippedVol1 =
                    vol1.Volatility(vars.OptionTenors[tenorIndex], vars.Strikes[strikeIndex], true);

                Volatility strippedVol2 =
                    vol2.Volatility(vars.OptionTenors[tenorIndex], vars.Strikes[strikeIndex], true);

                // vol from flat vol surface (for comparison only)
                Volatility flatVol =
                    vars.FlatTermVolSurface.Volatility(vars.OptionTenors[tenorIndex], vars.Strikes[strikeIndex], true);

                Real error = Math.Abs(strippedVol1 - strippedVol2);
                if (error > vars.Tolerance)
                {
                    Assert.Fail(VolMessage(
                        vars.OptionTenors[tenorIndex], vars.Strikes[strikeIndex], strippedVol1, strippedVol2, flatVol,
                        error, vars.Tolerance));
                }
            }
        }
    }

    [Fact]
    public void TestTermVolatilityStripping2()
    {
        // Testing forward/forward vol stripping from non-flat term vol surface using OptionletStripper2 class...

        var vars = new CommonVars(new Date(30, April, 2015));
        vars.SetCapFloorTermVolCurve();
        vars.SetCapFloorTermVolSurface();

        var iborIndex = new Euribor6M(vars.YieldTermStructure);

        // optionletstripper1
        var optionletStripper1 = new OptionletStripper1(
            vars.CapFloorVolSurface, iborIndex, vars.Context, null, vars.Accuracy);

        OptionletVolatilityStructure vol1 =
            StrippedOptionletAdapter.Create(optionletStripper1, vars.Context).EnableExtrapolation();

        // optionletstripper2
        OptionletStripper optionletStripper2 =
            new OptionletStripper2(optionletStripper1, vars.CapFloorVolCurve, vars.Context);

        OptionletVolatilityStructure vol2 =
            StrippedOptionletAdapter.Create(optionletStripper2, vars.Context).EnableExtrapolation();

        // consistency check: diff(stripped vol1-stripped vol2)
        for (Size strikeIndex = 0; strikeIndex < vars.Strikes.Length; ++strikeIndex)
        {
            for (Size tenorIndex = 0; tenorIndex < vars.OptionTenors.Length; ++tenorIndex)
            {
                Volatility strippedVol1 =
                    vol1.Volatility(vars.OptionTenors[tenorIndex], vars.Strikes[strikeIndex], true);

                Volatility strippedVol2 =
                    vol2.Volatility(vars.OptionTenors[tenorIndex], vars.Strikes[strikeIndex], true);

                // vol from flat vol surface (for comparison only)
                Volatility flatVol =
                    vars.CapFloorVolSurface.Volatility(vars.OptionTenors[tenorIndex], vars.Strikes[strikeIndex], true);

                Real error = Math.Abs(strippedVol1 - strippedVol2);
                if (error > vars.Tolerance)
                {
                    Assert.Fail(VolMessage(
                        vars.OptionTenors[tenorIndex], vars.Strikes[strikeIndex], strippedVol1, strippedVol2, flatVol,
                        error, vars.Tolerance));
                }
            }
        }
    }

    // Adapted: the relinked curve (lines 925-926) is a new curve, with a new index and a new stripper on it (S7D6),
    // since a stripper strips once; IborCoupon::Settings' at-par switch (line 897) is the context's.
    [Fact]
    public void TestSwitchStrike()
    {
        // Testing switch strike level and recalibration of level in case of curve relinking...

        var vars = new CommonVars(new Date(28, October, 2013));
        bool usingAtParCoupons = vars.Context.UseAtParCoupons;
        vars.SetCapFloorTermVolSurface();

        YieldTermStructure yieldTermStructure =
            FlatForward.Create(0, vars.Calendar, 0.03, vars.DayCounter, vars.Context);

        var iborIndex = new Euribor6M(yieldTermStructure);

        var optionletStripper1 = new OptionletStripper1(
            vars.CapFloorVolSurface, iborIndex, vars.Context, null, vars.Accuracy);

        Real expected = usingAtParCoupons ? 0.02981223 : 0.02981258;

        Real error = Math.Abs(optionletStripper1.SwitchStrike - expected);
        if (error > vars.Tolerance)
        {
            Assert.Fail(SwitchMessage(expected, optionletStripper1.SwitchStrike, error, vars.Tolerance));
        }

        yieldTermStructure = FlatForward.Create(0, vars.Calendar, 0.05, vars.DayCounter, vars.Context);
        iborIndex = new Euribor6M(yieldTermStructure);
        optionletStripper1 = new OptionletStripper1(
            vars.CapFloorVolSurface, iborIndex, vars.Context, null, vars.Accuracy);

        expected = usingAtParCoupons ? 0.0499371 : 0.0499381;

        error = Math.Abs(optionletStripper1.SwitchStrike - expected);
        if (error > vars.Tolerance)
        {
            Assert.Fail(SwitchMessage(expected, optionletStripper1.SwitchStrike, error, vars.Tolerance));
        }
    }

    [Fact]
    public void TestFlatTermVolatilityStripping1SofrPaymentLag()
    {
        // Testing forward/forward vol stripping from flat term vol surface using OptionletStripper1 class with an
        // overnight index and a payment lag...
        CheckSofrOptionletStripping(true, 2);
    }

    [Fact]
    public void TestTermVolatilityStripping1SofrPaymentLag()
    {
        // Testing forward/forward vol stripping from non-flat term vol surface using OptionletStripper1 class with an
        // overnight index and a payment lag...
        CheckSofrOptionletStripping(false, 2);
    }

    [Fact]
    public void TestFlatTermVolatilityStripping1IborPaymentLag()
    {
        // Testing forward/forward vol stripping from flat term vol surface using OptionletStripper1 class with an Ibor
        // index and a payment lag...
        CheckIborOptionletStripping(true, 2);
    }

    [Fact]
    public void TestTermVolatilityStripping1IborPaymentLag()
    {
        // Testing forward/forward vol stripping from non-flat term vol surface using OptionletStripper1 class with an
        // Ibor index and a payment lag...
        CheckIborOptionletStripping(false, 2);
    }

    // Adapted: the legacy engine's quote, set per point (lines 1305-1306, 1328), is the volatility passed to each call.
    [Fact]
    public void TestIborReferenceDateCompatibility()
    {
        // Testing that Ibor stripping preserves the legacy volatility reference date...

        var vars = new CommonVars(new Date(28, October, 2013));
        vars.SetFlatTermVolSurface();

        CapFloorTermVolSurface surface = CapFloorTermVolSurface.Create(
            2, vars.Calendar, BusinessDayConvention.Following, vars.OptionTenors, vars.Strikes, vars.TermV,
            vars.Context, vars.DayCounter);
        var iborIndex = new Euribor6M(vars.YieldTermStructure);
        var stripper1 = new OptionletStripper1(surface, iborIndex, vars.Context, null, vars.Accuracy);

        Matrix capFloorPrices = stripper1.CapFloorPrices();
        Period capFloorLength = 2 * iborIndex.Tenor;
        for (Size i = 0; i < capFloorPrices.Rows; ++i)
        {
            for (Size j = 0; j < vars.Strikes.Length; ++j)
            {
                Volatility volQuote = surface.Volatility(capFloorLength, vars.Strikes[j], true);
                CapFloorType type = vars.Strikes[j] < stripper1.SwitchStrike ? CapFloorType.Floor : CapFloorType.Cap;
                CapFloor legacyCap = new MakeCapFloor(type, capFloorLength, iborIndex)
                    .WithStrike(vars.Strikes[j])
                    .ToCapFloor(vars.Context);
                CheckSmall(
                    capFloorPrices[i, j] - BlackCapFloorEngine.Calculate(
                        legacyCap, vars.Context, vars.YieldTermStructure, volQuote, vars.DayCounter).NPV!.Value,
                    1.0e-12);
            }

            capFloorLength += iborIndex.Tenor;
        }

        double[] atmVols = [.. Enumerable.Repeat(0.18, vars.OptionTenors.Length)];
        CapFloorTermVolCurve atmCurve = CapFloorTermVolCurve.Create(
            2, vars.Calendar, BusinessDayConvention.Following, vars.OptionTenors, atmVols, vars.Context,
            vars.DayCounter);
        var stripper2 = new OptionletStripper2(stripper1, atmCurve, vars.Context);
        ImmutableArray<Rate> atmStrikes = stripper2.AtmCapFloorStrikes;
        ImmutableArray<Real> atmPrices = stripper2.AtmCapFloorPrices;
        for (Size i = 0; i < vars.OptionTenors.Length; ++i)
        {
            CapFloor legacyCap = new MakeCapFloor(CapFloorType.Cap, vars.OptionTenors[i], iborIndex)
                .WithStrike(atmStrikes[i])
                .ToCapFloor(vars.Context);
            CheckSmall(
                atmPrices[i] - BlackCapFloorEngine.Calculate(
                    legacyCap, vars.Context, vars.YieldTermStructure, 0.18, vars.DayCounter).NPV!.Value,
                1.0e-12);
        }
    }

    [Fact]
    public void TestTermVolatilityStripping2IborPaymentLag()
    {
        // Testing ATM adjustment with OptionletStripper2 using an Ibor index and a payment lag...
        CheckIborOptionletStripping2(2);
    }

    [Fact]
    public void TestFlatTermVolatilityStripping2SofrPaymentLag()
    {
        // Testing ATM adjustment from a flat term vol curve using OptionletStripper2 with an overnight index and a
        // payment lag...
        CheckSofrOptionletStripping2(true, 2);
    }

    [Fact]
    public void TestTermVolatilityStripping2SofrPaymentLag()
    {
        // Testing ATM adjustment from a non-flat term vol curve using OptionletStripper2 with an overnight index and a
        // payment lag...
        CheckSofrOptionletStripping2(false, 2);
    }

    [Fact]
    public void TestTermVolatilityStripping2IborFrontStub()
    {
        // Testing that OptionletStripper2 still accepts a front-stub Ibor cap...
        CheckIborOptionletStripping2FrontStub();
    }

    [Fact]
    public void TestOptionletStripper2RejectsEmptyIborCap()
    {
        // Testing that OptionletStripper2 rejects an Ibor cap with no optionlets...
        var vars = new CommonVars(new Date(28, October, 2013));
        vars.SetFlatTermVolSurface();
        var iborIndex = new Euribor6M(vars.YieldTermStructure);
        var stripper1 = new OptionletStripper1(vars.FlatTermVolSurface, iborIndex, vars.Context, null, vars.Accuracy);

        Period[] tenors = [new Period(6, TimeUnit.Months), new Period(1, TimeUnit.Years)];
        double[] atmVols = [0.18, 0.18];
        CapFloorTermVolCurve atmCurve = CapFloorTermVolCurve.Create(
            0, vars.Calendar, BusinessDayConvention.Following, tenors, atmVols, vars.Context, vars.DayCounter);

        // BOOST_CHECK_THROW(stripper2->atmCapFloorPrices(), Error), at construction.
        CheckThrowsError(() => _ = new OptionletStripper2(stripper1, atmCurve, vars.Context));
    }

    [Fact]
    public void TestOptionletStripper2ToleratesMismatchedReferenceDate()
    {
        // Testing that OptionletStripper2 still accepts mismatched volatility reference dates...
        var vars = new CommonVars(new Date(28, October, 2013));
        vars.SetFlatTermVolSurface();
        var iborIndex = new Euribor6M(vars.YieldTermStructure);
        var stripper1 = new OptionletStripper1(vars.FlatTermVolSurface, iborIndex, vars.Context, null, vars.Accuracy);

        double[] atmVols = [.. Enumerable.Repeat(0.18, vars.OptionTenors.Length)];

        // different settlement days than the source surface
        CapFloorTermVolCurve atmCurve = CapFloorTermVolCurve.Create(
            2, vars.Calendar, BusinessDayConvention.Following, vars.OptionTenors, atmVols, vars.Context,
            vars.DayCounter);

        // BOOST_CHECK_NO_THROW(stripper2->atmCapFloorPrices()), at construction.
        var stripper2 = new OptionletStripper2(stripper1, atmCurve, vars.Context);
        _ = stripper2.AtmCapFloorPrices;
    }

    [Fact]
    public void TestOptionletStripper2AdjustsZeroBaseVolatility()
    {
        // Testing OptionletStripper2 adjustment from zero base volatility...
        var vars = new CommonVarsON();
        vars.SetSofrHandle();
        vars.SetFlatSofrCapFloorVolSurface();
        vars.Vols = new Matrix(vars.Expiries.Length, vars.Strikes.Length, 0.0);
        vars.CapfloorVol = CapFloorTermVolSurface.Create(
            2, vars.Calendar, vars.Convention, vars.Expiries, vars.Strikes, vars.Vols, vars.Context, vars.Dc);

        var sofrIndex = new Sofr(vars.SofrCurveHandle);
        EvaluationContext context = vars.WithSofrFixing(sofrIndex);
        var stripper1 = new OptionletStripper1(
            vars.CapfloorVol, sofrIndex, context, null, 1e-6, 100, vars.SofrCurveHandle, VolatilityType.Normal, 0.0,
            true, vars.Tenor, 2);

        double[] atmVols = [.. Enumerable.Repeat(0.05, vars.Expiries.Length)];
        CapFloorTermVolCurve atmCurve = CapFloorTermVolCurve.Create(
            2, vars.Calendar, vars.Convention, vars.Expiries, atmVols, context, vars.Dc);
        var stripper2 = new OptionletStripper2(stripper1, atmCurve, context);

        ImmutableArray<Volatility> spreads = stripper2.SpreadsVol;
        foreach (Volatility spread in spreads)
        {
            Assert.True(spread > 0.0, $"spread {spread} isn't positive");
        }
    }

    // Bootstraps an OptionletStripper1 from an overnight (SOFR) surface, with an explicit payment lag applied both to
    // the calibration leg (inside the stripper) and to an externally-built reference leg of the same tenor/lag. Cap
    // prices from the two should agree at every surface grid point.
    // Adapted: the quoted volatility's structure (lines 990-993) is a ConstantOptionletVolatility on its value.
    private static void CheckSofrOptionletStripping(bool flatVol, Natural paymentLag)
    {
        var vars = new CommonVarsON();
        vars.SetSofrHandle();
        if (flatVol)
        {
            vars.SetFlatSofrCapFloorVolSurface();
        }
        else
        {
            vars.SetNonFlatSofrCapFloorVolSurface();
        }

        var sofrIndex = new Sofr(vars.SofrCurveHandle);
        EvaluationContext context = vars.WithSofrFixing(sofrIndex);

        Real notional = 1.0;

        var optionletSurf = new OptionletStripper1(
            vars.CapfloorVol, sofrIndex, context, null, 1e-6, 100, vars.SofrCurveHandle, VolatilityType.Normal, 0.0,
            false, vars.Tenor, paymentLag);

        OptionletVolatilityStructure ovsHandle = StrippedOptionletAdapter.Create(optionletSurf, context);

        Real tolerance = 2.5e-8;

        for (Size tenorIndex = 0; tenorIndex < vars.Expiries.Length; ++tenorIndex)
        {
            Date endDate = vars.Calendar.Advance(vars.StartDate, vars.Expiries[tenorIndex], vars.Convention);
            var schedule = new Schedule(
                vars.StartDate, endDate, vars.Tenor, vars.Calendar, vars.Convention, vars.Convention,
                DateGenerationRule.Forward, false, context);
            Leg sofrLeg = new OvernightLeg(schedule, sofrIndex)
                .WithNotionals(notional)
                .WithPaymentAdjustment(BusinessDayConvention.ModifiedFollowing)
                .WithPaymentLag(paymentLag)
                .WithCouponPricer(new CompoundingOvernightIndexedCouponPricer())
                .ToLeg();

            for (Size strikeIndex = 0; strikeIndex < vars.Strikes.Length; ++strikeIndex)
            {
                Rate[] strikes = [vars.Strikes[strikeIndex]];

                var strippedCap = new Cap(sofrLeg, strikes);
                Real strippedCapPrice =
                    BachelierCapFloorEngine.Calculate(strippedCap, context, vars.SofrCurveHandle, ovsHandle).NPV!.Value;

                var quotedVolHandle = new ConstantOptionletVolatility(
                    vars.CapfloorVol.ReferenceDate, vars.Calendar, vars.Convention, vars.Vols[tenorIndex, strikeIndex],
                    vars.Dc, VolatilityType.Normal);
                var cap = new Cap(sofrLeg, strikes);
                Real capPrice =
                    BachelierCapFloorEngine.Calculate(cap, context, vars.SofrCurveHandle, quotedVolHandle).NPV!.Value;

                Real error = Math.Abs(capPrice - strippedCapPrice);
                if (error > tolerance)
                {
                    Assert.Fail($"\noption tenor:  {vars.Expiries[tenorIndex]}"
                                + $"\nstrike:        {DataFormattersFunctions.Rate(vars.Strikes[strikeIndex])}"
                                + $"\nquoted vol price:   {capPrice}"
                                + $"\nstripped vol price: {strippedCapPrice}"
                                + $"\nerror:         {error}"
                                + $"\ntolerance:     {tolerance}");
                }
            }
        }
    }

    // Same idea as checkSofrOptionletStripping, but for the Ibor branch: the reference leg and the stripper's internal
    // calibration leg are both built (by hand, bypassing MakeCapFloor) with the same explicit payment lag.
    private static void CheckIborOptionletStripping(bool flatVol, Natural paymentLag)
    {
        var vars = new CommonVars(new Date(28, October, 2013));
        if (flatVol)
        {
            vars.SetFlatTermVolSurface();
        }
        else
        {
            vars.SetCapFloorTermVolSurface();
        }

        CapFloorTermVolSurface surface = flatVol ? vars.FlatTermVolSurface : vars.CapFloorVolSurface;

        var iborIndex = new Euribor6M(vars.YieldTermStructure);

        var optionletStripper1 = new OptionletStripper1(
            surface, iborIndex, vars.Context, null, vars.Accuracy, 100, null, VolatilityType.ShiftedLognormal, 0.0,
            false, null, paymentLag);

        OptionletVolatilityStructure vol =
            StrippedOptionletAdapter.Create(optionletStripper1, vars.Context).EnableExtrapolation();

        Date startDate = iborIndex.ValueDate(iborIndex.FixingCalendar.Adjust(vars.Context.EvaluationDate));

        for (Size tenorIndex = 0; tenorIndex < vars.OptionTenors.Length; ++tenorIndex)
        {
            Date endDate = startDate + vars.OptionTenors[tenorIndex];
            var schedule = new Schedule(
                startDate, endDate, iborIndex.Tenor, iborIndex.FixingCalendar, iborIndex.BusinessDayConvention,
                iborIndex.BusinessDayConvention, DateGenerationRule.Backward, false, vars.Context);
            Leg leg = new IborLeg(schedule, iborIndex)
                .WithNotionals(1.0)
                .WithPaymentDayCounter(iborIndex.DayCounter)
                .WithPaymentAdjustment(iborIndex.BusinessDayConvention)
                .WithPaymentLag(paymentLag)
                .ToLeg(vars.Context);

            for (Size strikeIndex = 0; strikeIndex < vars.Strikes.Length; ++strikeIndex)
            {
                Rate[] strikes = [vars.Strikes[strikeIndex]];

                var strippedCap = new CapFloor(CapFloorType.Cap, leg, strikes);
                Real priceFromStrippedVolatility =
                    BlackCapFloorEngine.Calculate(strippedCap, vars.Context, vars.YieldTermStructure, vol).NPV!.Value;

                var cap = new CapFloor(CapFloorType.Cap, leg, strikes);
                Real priceFromConstantVolatility = BlackCapFloorEngine.Calculate(
                    cap, vars.Context, vars.YieldTermStructure, vars.TermV[tenorIndex, strikeIndex]).NPV!.Value;

                Real error = Math.Abs(priceFromStrippedVolatility - priceFromConstantVolatility);
                if (error > vars.Tolerance)
                {
                    Assert.Fail(PriceMessage(
                        vars.OptionTenors[tenorIndex], vars.Strikes[strikeIndex], priceFromStrippedVolatility,
                        priceFromConstantVolatility, error, vars.Tolerance));
                }
            }
        }
    }

    private static void CheckIborOptionletStripping2(Natural paymentLag)
    {
        var vars = new CommonVars(new Date(28, October, 2013));
        vars.SetFlatTermVolCurve();
        vars.SetFlatTermVolSurface();

        var iborIndex = new Euribor6M(vars.YieldTermStructure);
        var stripper1 = new OptionletStripper1(
            vars.FlatTermVolSurface, iborIndex, vars.Context, null, vars.Accuracy, 100, null,
            VolatilityType.ShiftedLognormal, 0.0, false, null, paymentLag);
        var stripper2 = new OptionletStripper2(stripper1, vars.FlatTermVolCurve, vars.Context);

        // The flat ATM curve and flat surface imply an exact zero adjustment; the tight price tolerance below
        // therefore isolates leg conventions.
        foreach (Volatility spread in stripper2.SpreadsVol)
        {
            CheckSmall(spread, 1.0e-12);
        }

        OptionletVolatilityStructure vol = StrippedOptionletAdapter.Create(stripper2, vars.Context).EnableExtrapolation();

        ImmutableArray<Rate> strikes = stripper2.AtmCapFloorStrikes;
        ImmutableArray<Real> targetPrices = stripper2.AtmCapFloorPrices;
        Date startDate = iborIndex.ValueDate(iborIndex.FixingCalendar.Adjust(vars.Context.EvaluationDate));

        for (Size i = 0; i < vars.OptionTenors.Length; ++i)
        {
            Date endDate = startDate + vars.OptionTenors[i];
            var schedule = new Schedule(
                startDate, endDate, iborIndex.Tenor, iborIndex.FixingCalendar, iborIndex.BusinessDayConvention,
                iborIndex.BusinessDayConvention, DateGenerationRule.Backward, false, vars.Context);
            Leg leg = new IborLeg(schedule, iborIndex)
                .WithNotionals(1.0)
                .WithPaymentDayCounter(iborIndex.DayCounter)
                .WithPaymentAdjustment(iborIndex.BusinessDayConvention)
                .WithPaymentLag(paymentLag)
                .ToLeg(vars.Context)
                .RemoveAt(0);

            var cap = new CapFloor(CapFloorType.Cap, leg, [strikes[i]]);
            Real npv = BlackCapFloorEngine.Calculate(cap, vars.Context, vars.YieldTermStructure, vol).NPV!.Value;
            Real error = Math.Abs(npv - targetPrices[i]);
            if (error > vars.Tolerance)
            {
                Assert.Fail($"\noption tenor: {vars.OptionTenors[i]}"
                            + $"\nATM strike:   {DataFormattersFunctions.Rate(strikes[i])}"
                            + $"\ntarget price: {targetPrices[i]}"
                            + $"\nstripper price: {npv}"
                            + $"\nerror:        {error}"
                            + $"\ntolerance:    {vars.Tolerance}");
            }
        }
    }

    // Adapted: the any_of over the first uncovered smile (lines 1172-1175) is LINQ's Any.
    private static void CheckSofrOptionletStripping2(bool flatVol, Natural paymentLag)
    {
        var vars = new CommonVarsON();
        vars.SetSofrHandle();
        if (flatVol)
        {
            vars.SetFlatSofrCapFloorVolSurface();
        }
        else
        {
            vars.SetNonFlatSofrCapFloorVolSurface();
        }

        var sofrIndex = new Sofr(vars.SofrCurveHandle);
        EvaluationContext context = vars.WithSofrFixing(sofrIndex);

        double[] atmVols = new double[vars.Expiries.Length];
        for (Size i = 0; i < atmVols.Length; ++i)
        {
            Volatility atmVol = vars.Vols[i, 1];
            if (!flatVol)
            {
                atmVol += 0.0001;
            }

            atmVols[i] = atmVol;
        }

        CapFloorTermVolCurve atmCurve = CapFloorTermVolCurve.Create(
            2, vars.Calendar, vars.Convention, vars.Expiries, atmVols, context, vars.Dc);

        var stripper1 = new OptionletStripper1(
            vars.CapfloorVol, sofrIndex, context, null, 1e-6, 100, vars.SofrCurveHandle, VolatilityType.Normal, 0.0,
            false, vars.Tenor, paymentLag);
        var stripper2 = new OptionletStripper2(stripper1, atmCurve, context);

        OptionletVolatilityStructure vol = StrippedOptionletAdapter.Create(stripper2, context).EnableExtrapolation();

        ImmutableArray<Rate> strikes = stripper2.AtmCapFloorStrikes;
        ImmutableArray<Real> targetPrices = stripper2.AtmCapFloorPrices;
        Real tolerance = 1.0e-6;

        if (flatVol)
        {
            ImmutableArray<Rate> firstUncoveredSmile = stripper2.OptionletStrikes(4);
            bool containsOneYearAtmStrike = firstUncoveredSmile.Any(strike => Math.Abs(strike - strikes[0]) < 1.0e-12);
            Assert.False(containsOneYearAtmStrike, "the 1Y SOFR ATM adjustment was applied beyond its final optionlet");
        }

        for (Size i = 0; i < vars.Expiries.Length; ++i)
        {
            Date endDate = vars.Calendar.Advance(vars.StartDate, vars.Expiries[i], vars.Convention);
            var schedule = new Schedule(
                vars.StartDate, endDate, vars.Tenor, vars.Calendar, vars.Convention, vars.Convention,
                DateGenerationRule.Forward, false, context);
            Leg leg = new OvernightLeg(schedule, sofrIndex)
                .WithNotionals(1.0)
                .WithPaymentCalendar(vars.Calendar)
                .WithPaymentAdjustment(vars.Convention)
                .WithPaymentLag(paymentLag)
                .WithCouponPricer(new CompoundingOvernightIndexedCouponPricer())
                .ToLeg();

            var cap = new CapFloor(CapFloorType.Cap, leg, [strikes[i]]);
            Real npv = BachelierCapFloorEngine.Calculate(cap, context, vars.SofrCurveHandle, vol).NPV!.Value;
            Real error = Math.Abs(npv - targetPrices[i]);
            if (error > tolerance)
            {
                Assert.Fail($"\noption tenor: {vars.Expiries[i]}"
                            + $"\nATM strike:   {DataFormattersFunctions.Rate(strikes[i])}"
                            + $"\ntarget price: {targetPrices[i]}"
                            + $"\nstripper price: {npv}"
                            + $"\nerror:        {error}"
                            + $"\ntolerance:    {tolerance}");
            }
        }
    }

    private static void CheckIborOptionletStripping2FrontStub()
    {
        var vars = new CommonVars(new Date(28, October, 2013));
        vars.SetFlatTermVolSurface();

        var iborIndex = new Euribor6M(vars.YieldTermStructure);
        var stripper1 = new OptionletStripper1(vars.FlatTermVolSurface, iborIndex, vars.Context, null, vars.Accuracy);

        Period[] tenors = [new Period(9, TimeUnit.Months), new Period(1, TimeUnit.Years)];
        double[] atmVols = [0.19, 0.19];
        CapFloorTermVolCurve atmCurve = CapFloorTermVolCurve.Create(
            0, vars.Calendar, BusinessDayConvention.Following, tenors, atmVols, vars.Context, vars.DayCounter);

        // Front-stub caps are not on the optionlet grid, but the historical behaviour is to strip them anyway rather
        // than throw. BOOST_CHECK_NO_THROW(stripper2->atmCapFloorPrices()), at construction.
        var stripper2 = new OptionletStripper2(stripper1, atmCurve, vars.Context);
        foreach (Volatility spread in stripper2.SpreadsVol)
        {
            Assert.True(double.IsFinite(spread), $"spread {spread} isn't finite");
        }

        OptionletVolatilityStructure vol = StrippedOptionletAdapter.Create(stripper2, vars.Context).EnableExtrapolation();
        ImmutableArray<Rate> strikes = stripper2.AtmCapFloorStrikes;
        ImmutableArray<Real> targetPrices = stripper2.AtmCapFloorPrices;
        CapFloor cap = new MakeCapFloor(CapFloorType.Cap, tenors[0], iborIndex)
            .WithStrike(strikes[0])
            .ToCapFloor(vars.Context);
        CheckSmall(
            BlackCapFloorEngine.Calculate(cap, vars.Context, vars.YieldTermStructure, vol).NPV!.Value - targetPrices[0],
            vars.Tolerance);
    }

    private static string PriceMessage(
        Period optionTenor, Rate strike, Real strippedPrice, Real constantPrice, Real error, Real tolerance) =>
        $"\noption tenor:       {optionTenor}"
        + $"\nstrike:             {DataFormattersFunctions.Rate(strike)}"
        + $"\nstripped vol price: {DataFormattersFunctions.Rate(strippedPrice)}"
        + $"\nconstant vol price: {DataFormattersFunctions.Rate(constantPrice)}"
        + $"\nerror:              {DataFormattersFunctions.Rate(error)}"
        + $"\ntolerance:          {DataFormattersFunctions.Rate(tolerance)}";

    private static string VolMessage(
        Period optionTenor, Rate strike, Volatility vol1, Volatility vol2, Volatility flatVol, Real error,
        Real tolerance) =>
        $"\noption tenor:  {optionTenor}"
        + $"\nstrike:        {DataFormattersFunctions.Rate(strike)}"
        + $"\nstripped vol1: {DataFormattersFunctions.Rate(vol1)}"
        + $"\nstripped vol2: {DataFormattersFunctions.Rate(vol2)}"
        + $"\nflat vol:      {DataFormattersFunctions.Rate(flatVol)}"
        + $"\nerror:         {DataFormattersFunctions.Rate(error)}"
        + $"\ntolerance:     {DataFormattersFunctions.Rate(tolerance)}";

    private static string SwitchMessage(Real expected, Real computed, Real error, Real tolerance) =>
        "\nSwitchstrike not correctly computed:  "
        + $"\nexpected switch strike: {DataFormattersFunctions.Rate(expected)}"
        + $"\ncomputed switch strike: {DataFormattersFunctions.Rate(computed)}"
        + $"\nerror:         {DataFormattersFunctions.Rate(error)}"
        + $"\ntolerance:     {DataFormattersFunctions.Rate(tolerance)}";

    // QL_CHECK_SMALL: |fpv| <= tolerance.
    private static void CheckSmall(double fpv, double tolerance) =>
        Assert.True(Math.Abs(fpv) <= tolerance, $"|{fpv}| exceeds {tolerance}");

    // BOOST_CHECK_THROW(..., Error): QuantLib's Error is an ArgumentException or a NanoQLException in nanoql.
    private static void CheckThrowsError(Action action)
    {
        Exception? e = Record.Exception(action);
        Assert.True(
            e is ArgumentException or NanoQLException,
            $"expected an ArgumentException or a NanoQLException, got {e?.GetType().Name ?? "no exception"}");
    }

    private sealed class CommonVars
    {
        public CommonVars(Date today)
        {
            Context = new EvaluationContext { EvaluationDate = today };
            Accuracy = 1.0e-6;
            Tolerance = 2.5e-8;
        }

        // global data
        public EvaluationContext Context { get; }

        public Calendar Calendar { get; private set; } = null!;

        public DayCounter DayCounter { get; private set; } = null!;

        public YieldTermStructure YieldTermStructure { get; private set; } = null!;

        public YieldTermStructure? DiscountingYTS { get; private set; }

        public YieldTermStructure? ForwardingYTS { get; private set; }

        public ImmutableArray<Rate> Strikes { get; private set; } = [];

        public ImmutableArray<Period> OptionTenors { get; private set; } = [];

        public Matrix TermV { get; private set; } = new();

        public ImmutableArray<Rate> AtmTermV { get; private set; } = [];

        public CapFloorTermVolCurve CapFloorVolCurve { get; private set; } = null!;

        public CapFloorTermVolCurve FlatTermVolCurve { get; private set; } = null!;

        public CapFloorTermVolSurface CapFloorVolSurface { get; private set; } = null!;

        public CapFloorTermVolSurface FlatTermVolSurface { get; private set; } = null!;

        public CapFloorTermVolSurface CapFloorVolRealSurface { get; private set; } = null!;

        public Real Accuracy { get; }

        public Real Tolerance { get; }

        public void SetTermStructure()
        {
            Calendar = new TARGET();
            DayCounter = new Actual365Fixed();

            Rate flatFwdRate = 0.04;
            YieldTermStructure = FlatForward.Create(0, Calendar, flatFwdRate, DayCounter, Context);
        }

        public void SetRealTermStructure()
        {
            Calendar = new TARGET();
            DayCounter = new Actual365Fixed();

            int[] datesTmp =
            [
                42124, 42129, 42143, 42221, 42254, 42282, 42313, 42345,
                42374, 42405, 42465, 42495, 42587, 42681, 42772, 42860, 43227,
                43956, 44321, 44686, 45051, 45418, 45782, 46147, 46512, 47609,
                49436, 51263, 53087, 56739, 60392,
            ];

            Rate[] rates =
            [
                -0.00292, -0.00292, -0.001441, -0.00117, -0.001204,
                -0.001212, -0.001223, -0.001236, -0.001221, -0.001238,
                -0.001262, -0.00125, -0.001256, -0.001233, -0.00118, -0.001108,
                -0.000619, 0.000833, 0.001617, 0.002414, 0.003183, 0.003883,
                0.004514, 0.005074, 0.005606, 0.006856, 0.00813, 0.008709,
                0.009136, 0.009601, 0.009384,
            ];

            DiscountingYTS = new InterpolatedZeroCurve<Linear>(
                datesTmp.Select(d => new Date(d)), rates, DayCounter, Calendar);

            datesTmp =
            [
                42124, 42313, 42436, 42556, 42618, 42800, 42830, 42860,
                43227, 43591, 43956, 44321, 44686, 45051, 45418, 45782, 46147,
                46512, 46878, 47245, 47609, 47973, 48339, 48704, 49069, 49436,
                49800, 50165, 50530, 50895, 51263, 51627, 51991, 52356, 52722,
                53087, 54913, 56739, 60392, 64045,
            ];

            rates =
            [
                0.000649, 0.000649, 0.000684, 0.000717, 0.000745, 0.000872,
                0.000905, 0.000954, 0.001532, 0.002319, 0.003147, 0.003949,
                0.004743, 0.00551, 0.006198, 0.006798, 0.007339, 0.007832,
                0.008242, 0.008614, 0.008935, 0.009205, 0.009443, 0.009651,
                0.009818, 0.009952, 0.010054, 0.010146, 0.010206, 0.010266,
                0.010315, 0.010365, 0.010416, 0.010468, 0.010519, 0.010571,
                0.010757, 0.010806, 0.010423, 0.010217,
            ];

            ForwardingYTS = new InterpolatedZeroCurve<Linear>(
                datesTmp.Select(d => new Date(d)), rates, DayCounter, Calendar);
        }

        public void SetFlatTermVolCurve()
        {
            SetTermStructure();

            OptionTenors = [.. Enumerable.Range(0, 10).Select(i => new Period(i + 1, TimeUnit.Years))];

            Volatility flatVol = .18;

            double[] curveVHandle = [.. Enumerable.Repeat(flatVol, OptionTenors.Length)];

            FlatTermVolCurve = CapFloorTermVolCurve.Create(
                0, Calendar, BusinessDayConvention.Following, OptionTenors, curveVHandle, Context, DayCounter);
        }

        public void SetFlatTermVolSurface()
        {
            SetTermStructure();

            OptionTenors = [.. Enumerable.Range(0, 10).Select(i => new Period(i + 1, TimeUnit.Years))];

            Strikes = [.. Enumerable.Range(0, 10).Select(j => (j + 1) / 100.0)];

            Volatility flatVol = .18;
            TermV = new Matrix(OptionTenors.Length, Strikes.Length, flatVol);
            FlatTermVolSurface = CapFloorTermVolSurface.Create(
                0, Calendar, BusinessDayConvention.Following, OptionTenors, Strikes, TermV, Context, DayCounter);
        }

        public void SetCapFloorTermVolCurve()
        {
            SetTermStructure();

            // atm cap volatility curve
            OptionTenors = MarketOptionTenors;

            // atm capfloor vols from mkt vol matrix using flat yield curve
            AtmTermV =
            [
                0.090304, 0.12180, 0.13077, 0.14832, 0.15570, 0.15816, 0.15932, 0.16035,
                0.15951, 0.15855, 0.15754, 0.15459, 0.15163, 0.14575, 0.14175, 0.13889,
            ];

            CapFloorVolCurve = CapFloorTermVolCurve.Create(
                0, Calendar, BusinessDayConvention.Following, OptionTenors, AtmTermV, Context, DayCounter);
        }

        public void SetCapFloorTermVolSurface()
        {
            SetTermStructure();

            // cap volatility smile matrix
            OptionTenors = MarketOptionTenors;

            Strikes = [0.015, 0.0175, 0.02, 0.0225, 0.025, 0.03, 0.035, 0.04, 0.05, 0.06, 0.07, 0.08, 0.1];

            double[] rows =
            [
                0.287, 0.274, 0.256, 0.245, 0.227, 0.148, 0.096, 0.09, 0.11, 0.139, 0.166, 0.19, 0.214,
                0.303, 0.258, 0.22, 0.203, 0.19, 0.153, 0.126, 0.118, 0.147, 0.165, 0.18, 0.192, 0.212,
                0.303, 0.257, 0.216, 0.196, 0.182, 0.154, 0.134, 0.127, 0.149, 0.166, 0.18, 0.192, 0.212,
                0.305, 0.266, 0.226, 0.203, 0.19, 0.167, 0.151, 0.144, 0.16, 0.172, 0.183, 0.193, 0.209,
                0.294, 0.261, 0.216, 0.201, 0.19, 0.171, 0.158, 0.151, 0.163, 0.172, 0.181, 0.188, 0.201,
                0.276, 0.248, 0.212, 0.199, 0.189, 0.172, 0.16, 0.155, 0.162, 0.17, 0.177, 0.183, 0.195,
                0.26, 0.237, 0.21, 0.198, 0.188, 0.172, 0.161, 0.156, 0.161, 0.167, 0.173, 0.179, 0.19,
                0.25, 0.231, 0.208, 0.196, 0.187, 0.172, 0.162, 0.156, 0.16, 0.165, 0.17, 0.175, 0.185,
                0.244, 0.226, 0.206, 0.195, 0.186, 0.171, 0.161, 0.156, 0.158, 0.162, 0.166, 0.171, 0.18,
                0.239, 0.222, 0.204, 0.193, 0.185, 0.17, 0.16, 0.155, 0.156, 0.159, 0.163, 0.168, 0.177,
                0.235, 0.219, 0.202, 0.192, 0.183, 0.169, 0.159, 0.154, 0.154, 0.156, 0.16, 0.164, 0.173,
                0.227, 0.212, 0.197, 0.187, 0.179, 0.166, 0.156, 0.151, 0.149, 0.15, 0.153, 0.157, 0.165,
                0.22, 0.206, 0.192, 0.183, 0.175, 0.162, 0.153, 0.147, 0.144, 0.144, 0.147, 0.151, 0.158,
                0.211, 0.197, 0.185, 0.176, 0.168, 0.156, 0.147, 0.142, 0.138, 0.138, 0.14, 0.144, 0.151,
                0.204, 0.192, 0.18, 0.171, 0.164, 0.152, 0.143, 0.138, 0.134, 0.134, 0.137, 0.14, 0.148,
                0.2, 0.187, 0.176, 0.167, 0.16, 0.148, 0.14, 0.135, 0.131, 0.132, 0.135, 0.139, 0.146,
            ];
            TermV = new Matrix(OptionTenors.Length, Strikes.Length, rows);

            CapFloorVolSurface = CapFloorTermVolSurface.Create(
                0, Calendar, BusinessDayConvention.Following, OptionTenors, Strikes, TermV, Context, DayCounter);
        }

        // Adapted: termV /= 100 (line 389) divides each copied value.
        public void SetRealCapFloorTermVolSurface()
        {
            SetRealTermStructure();

            // cap volatility smile matrix
            OptionTenors = MarketOptionTenors;

            // 16
            Strikes = [-0.005, -0.0025, -0.00125, 0.0, 0.00125, 0.0025, 0.005, 0.01, 0.015, 0.02, 0.03, 0.05, 0.1];

            // 13
            double[] rawVols =
            [
                0.49, 0.39, 0.34, 0.31, 0.34, 0.37, 0.50, 0.75, 0.99, 1.21, 1.64, 2.44, 4.29,
                0.44, 0.36, 0.33, 0.31, 0.33, 0.35, 0.45, 0.65, 0.83, 1.00, 1.32, 1.93, 3.30,
                0.40, 0.35, 0.33, 0.31, 0.33, 0.34, 0.41, 0.55, 0.69, 0.82, 1.08, 1.56, 2.68,
                0.42, 0.39, 0.38, 0.37, 0.38, 0.39, 0.43, 0.54, 0.64, 0.74, 0.94, 1.31, 2.18,
                0.46, 0.43, 0.42, 0.41, 0.42, 0.43, 0.47, 0.56, 0.66, 0.75, 0.93, 1.28, 2.07,
                0.49, 0.47, 0.46, 0.45, 0.46, 0.47, 0.51, 0.59, 0.68, 0.76, 0.93, 1.25, 1.99,
                0.51, 0.49, 0.49, 0.48, 0.49, 0.50, 0.54, 0.62, 0.70, 0.78, 0.94, 1.24, 1.94,
                0.52, 0.51, 0.51, 0.51, 0.52, 0.53, 0.56, 0.63, 0.71, 0.79, 0.94, 1.23, 1.89,
                0.53, 0.52, 0.52, 0.52, 0.53, 0.54, 0.57, 0.65, 0.72, 0.79, 0.94, 1.21, 1.83,
                0.55, 0.54, 0.54, 0.54, 0.55, 0.56, 0.59, 0.66, 0.72, 0.79, 0.91, 1.15, 1.71,
                0.56, 0.56, 0.56, 0.56, 0.57, 0.58, 0.61, 0.67, 0.72, 0.78, 0.89, 1.09, 1.59,
                0.59, 0.58, 0.58, 0.59, 0.59, 0.60, 0.63, 0.68, 0.73, 0.78, 0.86, 1.03, 1.45,
                0.61, 0.61, 0.61, 0.61, 0.62, 0.62, 0.64, 0.69, 0.73, 0.77, 0.85, 1.02, 1.44,
                0.62, 0.62, 0.63, 0.63, 0.64, 0.64, 0.65, 0.69, 0.72, 0.76, 0.82, 0.96, 1.32,
                0.62, 0.63, 0.63, 0.63, 0.65, 0.66, 0.66, 0.68, 0.72, 0.74, 0.80, 0.93, 1.25,
                0.62, 0.62, 0.62, 0.62, 0.66, 0.67, 0.67, 0.67, 0.72, 0.72, 0.78, 0.90, 1.25,
            ];

            TermV = new Matrix(OptionTenors.Length, Strikes.Length, [.. rawVols.Select(v => v / 100)]);

            CapFloorVolRealSurface = CapFloorTermVolSurface.Create(
                0, Calendar, BusinessDayConvention.Following, OptionTenors, Strikes, TermV, Context, DayCounter);
        }

        private static ImmutableArray<Period> MarketOptionTenors =>
        [
            new Period(1, TimeUnit.Years), new Period(18, TimeUnit.Months), new Period(2, TimeUnit.Years),
            new Period(3, TimeUnit.Years), new Period(4, TimeUnit.Years), new Period(5, TimeUnit.Years),
            new Period(6, TimeUnit.Years), new Period(7, TimeUnit.Years), new Period(8, TimeUnit.Years),
            new Period(9, TimeUnit.Years), new Period(10, TimeUnit.Years), new Period(12, TimeUnit.Years),
            new Period(15, TimeUnit.Years), new Period(20, TimeUnit.Years), new Period(25, TimeUnit.Years),
            new Period(30, TimeUnit.Years),
        ];
    }

    private sealed class CommonVarsON
    {
        public CommonVarsON()
        {
            Today = new Date(15, April, 2025);
            Context = new EvaluationContext { EvaluationDate = Today };
            StartDate = new Date(17, April, 2025);
            EndDate = new Date(17, April, 2030);
            Tenor = new Period(3, TimeUnit.Months);
            Calendar = new UnitedStates(UnitedStatesMarket.FederalReserve);
            Convention = BusinessDayConvention.ModifiedFollowing;
            Dc = new Actual360();
        }

        public Date Today { get; }

        public EvaluationContext Context { get; }

        public Date StartDate { get; }

        public Date EndDate { get; }

        public Period Tenor { get; }

        public Calendar Calendar { get; }

        public BusinessDayConvention Convention { get; }

        public DayCounter Dc { get; }

        public YieldTermStructure SofrCurveHandle { get; private set; } = null!;

        public ImmutableArray<Rate> Strikes { get; private set; } = [];

        public ImmutableArray<Period> Expiries { get; private set; } = [];

        public Matrix Vols { get; set; } = new();

        public CapFloorTermVolSurface CapfloorVol { get; set; } = null!;

        // sofrIndex->addFixing(Date(15, April, 2025), 3.04/100.0): the context with the fixing.
        public EvaluationContext WithSofrFixing(OvernightIndex sofrIndex) =>
            Context with { Fixings = Context.Fixings.AddFixing(sofrIndex, new Date(15, April, 2025), 3.04 / 100.0) };

        public void SetSofrHandle()
        {
            Date[] dates =
            [
                new Date(15, Apr, 2025), new Date(16, Apr, 2025), new Date(28, Apr, 2025), new Date(21, May, 2025),
                new Date(21, Jul, 2025), new Date(21, Oct, 2025), new Date(21, Apr, 2026), new Date(21, Apr, 2027),
                new Date(19, Apr, 2028), new Date(22, Apr, 2030), new Date(21, Apr, 2032), new Date(19, Apr, 2035),
                new Date(21, Apr, 2037), new Date(19, Apr, 2040), new Date(19, Apr, 2045), new Date(20, Apr, 2050),
                new Date(21, Apr, 2055),
            ];

            Rate[] zeroRates =
            [
                3.039872 / 100.0, 3.082092 / 100.0, 3.67902 / 100.0, 3.791077 / 100.0, 4.147655 / 100.0,
                4.498917 / 100.0, 4.688082 / 100.0, 4.486636 / 100.0, 4.228873 / 100.0, 3.949601 / 100.0,
                3.814579 / 100.0, 3.731412 / 100.0, 3.718794 / 100.0, 3.704788 / 100.0, 3.599069 / 100.0,
                3.401666 / 100.0, 3.221372 / 100.0,
            ];

            SofrCurveHandle = new InterpolatedZeroCurve<Linear>(dates, zeroRates, new Actual365Fixed(), Calendar);
        }

        public void SetFlatSofrCapFloorVolSurface()
        {
            Strikes = [0.03, 0.035, 0.04];

            Expiries = [.. Enumerable.Range(1, 10).Select(i => new Period(i, TimeUnit.Years))];

            Vols = new Matrix(Expiries.Length, Strikes.Length, 30.0 / 10000.0);

            CapfloorVol = CapFloorTermVolSurface.Create(2, Calendar, Convention, Expiries, Strikes, Vols, Context, Dc);
        }

        // A mild, smoothly-varying (term structure + skew) surface. Deliberately gentle: a surface with sharp
        // strike/expiry jumps can make the fine (indexTenor-spaced) internal calibration grid used for an overnight
        // index imply a slightly negative optionlet price at some deep-OTM point purely from interpolation noise, which
        // OptionletStripper1 (with dontThrow=false) correctly refuses to silently accept.
        public void SetNonFlatSofrCapFloorVolSurface()
        {
            Strikes = [0.03, 0.035, 0.04];

            Expiries = [.. Enumerable.Range(1, 10).Select(i => new Period(i, TimeUnit.Years))];

            Real[] skewBps = [4.0, 2.0, 0.0]; // higher vol at lower (more OTM put) strikes

            Vols = new Matrix(Expiries.Length, Strikes.Length);
            for (Size i = 0; i < Vols.Rows; ++i)
            {
                for (Size j = 0; j < Vols.Columns; ++j)
                {
                    Vols[i, j] = (25.0 + (1.5 * (i + 1)) + skewBps[j]) / 10000.0;
                }
            }

            CapfloorVol = CapFloorTermVolSurface.Create(2, Calendar, Convention, Expiries, Strikes, Vols, Context, Dc);
        }
    }
}
