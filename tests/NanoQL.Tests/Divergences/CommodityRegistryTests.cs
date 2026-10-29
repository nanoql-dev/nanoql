// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The units of measure, commodity types and payment terms as values (the S-xrt plan's XRR1, ruled 2026-10-28).
/// QuantLib keeps a process-wide registry for each, keyed by a unit's name, a type's code and a term's name, and a
/// second construction with a known key returns the first construction's data whatever it was given: on QuantLib 1.44
/// (the run's <c>probes\xi_c1.cpp</c>) <c>UnitOfMeasure("Barrels", "B2", Mass)</c> after <c>("Barrels", "BBL",
/// Volume)</c> has code BBL and type Volume, <c>CommodityType("Gasoil", "HOX")</c> after <c>("Heating Oil", "HOX")</c>
/// is named Heating Oil, and <c>PaymentTerm("Term R", TradeDate, 10, TARGET)</c> after <c>("Term R", PricingDate, 5,
/// NullCalendar)</c> counts 5 days from the pricing date on the null calendar. nanoql has no registry: each holds the
/// data it is given. Equality stays QuantLib's: a unit by code, a type by code, a term by name.
/// </summary>
public class CommodityRegistryTests
{
    [Fact]
    public void ASecondUnitWithAKnownNameKeepsItsOwnCodeAndType()
    {
        _ = new UnitOfMeasure("Barrels", "BBL", UnitOfMeasureType.Volume);
        var second = new UnitOfMeasure("Barrels", "B2", UnitOfMeasureType.Mass);
        Assert.Equal("B2", second.Code);
        Assert.Equal(UnitOfMeasureType.Mass, second.UnitType);
        Assert.NotEqual(new BarrelUnitOfMeasure(), second);
    }

    [Fact]
    public void ASecondTypeWithAKnownCodeKeepsItsOwnName()
    {
        var first = new CommodityType("Heating Oil", "HOX");
        var second = new CommodityType("Gasoil", "HOX");
        Assert.Equal("Gasoil", second.Name);
        Assert.Equal(first, second);
    }

    [Fact]
    public void ASecondTermWithAKnownNameKeepsItsOwnEventOffsetAndCalendar()
    {
        var first = new PaymentTerm("Term R", PaymentTermEventType.PricingDate, 5, new NullCalendar());
        var second = new PaymentTerm("Term R", PaymentTermEventType.TradeDate, 10, new TARGET());
        Assert.Equal(PaymentTermEventType.TradeDate, second.EventType);
        Assert.Equal(10, second.OffsetDays);
        Assert.Equal(new TARGET(), second.Calendar);
        Assert.Equal(first, second);
        Assert.Equal(new Date(27, Month.January, 2025), second.GetPaymentDate(new Date(17, Month.January, 2025)));
    }
}
