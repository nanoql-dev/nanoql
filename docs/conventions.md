<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Market conventions as data

QuantLib keeps its market conventions in code: a calendar's holidays, a currency's rounding, an index's fixing days, a
swap builder's fixed leg by currency. nanoql keeps QuantLib's values and lets them be configured. The library reads
each preset's and each market builder's conventions through a `ConventionProvider` when the object is built, QuantLib's
values unless a provider configures them, and the `NanoQL.Extensions` package reads and writes them as XML records:

```csharp
using NanoQL;
using NanoQL.Extensions.Conventions;

// a desk's conventions over QuantLib's presets, the system's defaults from here on
ConventionProvider.Default = ConventionSet.QuantLib.With(ConventionsXml.Read("desk-conventions.xml"));

// built after it, so they take the desk's values
var euribor6M = new Euribor6M(forwardingCurve);
var swap = new MakeVanillaSwap(new Period(5, TimeUnit.Years), euribor6M).WithFixedRate(0.03).ToVanillaSwap(context);
```

With nothing configured every object is QuantLib's. Every value nanoql ships is QuantLib's, generated from QuantLib and
checked against it; [the catalogue](conventions-catalogue.md) lists them all.

## The format

A conventions file is a `Conventions` element in the namespace `https://nanoql.net/schemas/conventions`, holding
records in any order. Each record is an element named after its kind, with a `name` attribute, an optional `base`
attribute, and its fields as child elements, each named after nanoql's API:

```xml
<Conventions xmlns="https://nanoql.net/schemas/conventions">
  <!-- TARGET with an extra holiday, the preset itself replaced -->
  <Calendar name="TARGET" base="TARGET">
    <AddedHoliday>2026-12-24</AddedHoliday>
  </Calendar>

  <!-- a family member: Euribor at two months, every other field the family's -->
  <IborIndex name="Euribor2M" base="Euribor"><Tenor>2M</Tenor></IborIndex>

  <!-- a swap convention of a desk's own -->
  <SwapConvention name="EUR-SWAP">
    <SettlementDays>2</SettlementDays>
    <FixedLegTenor>1Y</FixedLegTenor>
    <FixedLegConvention>ModifiedFollowing</FixedLegConvention>
    <FixedLegDayCounter>Thirty360.BondBasis</FixedLegDayCounter>
    <Index>Euribor</Index>
    <IndexTenor>6M</IndexTenor>
  </SwapConvention>
</Conventions>
```

- **The schema,** `nanoql-conventions.xsd`, documents every element; `ConventionsXml.Read` validates against it and
  refuses a bad file with its line and position. `ConventionsXml.WriteDefaults(folder)` writes the shipped files and
  the schema, for an editor or a diff.
- **Other tools' content,** elements and attributes in their own namespaces, is kept through a read and a write.
- **Several files** read as one snapshot: `ConventionsXml.Read(paths)`. A set layers snapshots one on another with
  `With`, a later layer's record replacing an earlier one's of the same name.

## Names, bases and aliases

- **A coded preset keeps its class's name:** a calendar by its type and market (`TARGET`, `UnitedStates.SOFR`), a day
  counter by its type and convention (`Actual360`, `Thirty360.BondBasis`), a currency by its ISO code, an index, a
  region or a unit of measure by its class (`Euribor6M`, `EURegion`, `BarrelUnitOfMeasure`). A record under that name
  configures the preset, and a preset is built as its own class with the configured values.
- **A builder's defaults** are records named after it: `MakeVanillaSwap.EUR`, `MakeVanillaSwap.GBP` and the other
  currencies', `MakeOIS` and `MakeOIS.Sonia` by the index's class, `MakeCreditDefaultSwap`, `MakeCapFloor`,
  `MakeSwaption`, `MakeCms`, `MakeMultipleResetsSwap`, `MakeYoYInflationCapFloor` and `FixedRateBond`. Each holds only
  the options its builder fixes, read when the builder builds and needs one. GBP's and AUD's fixed leg tenors depend on
  the swap's tenor, so their records carry a threshold, compared as QuantLib compares it.
- **Each kind has its own names:** the calendars share one name space, the day counters another, the indexes another,
  and each instrument convention kind its own. So `USD-SOFR` can be both an alias of the `Sofr` index and an OIS
  convention.
- **A base** names a record of the same kind, and the record gives only the fields it changes: every other field comes
  from the base, down its chain, then from QuantLib's reference. A record based on its own name replaces the record of
  that name in the layers below. A convention takes its builder's defaults without naming them as its base.
- **An alias** names a calendar, day counter, currency, region, index or unit of measure by another name:
  `<Alias name="bbl" base="BarrelUnitOfMeasure" />`.

### FpML's codes

nanoql ships FpML's codes from four of its lists as aliases of nanoql's names: the `day-count-fraction`,
`business-center` and `floating-rate-index` coding schemes and the `BusinessDayConventionEnum`. So `ACT/360` names
`Actual360`, `USGS` names `UnitedStates.GovernmentBond` and `USD-SOFR` names `Sofr`. Only a code that
maps to a nanoql name exactly is an alias: a business centre naming a city's banking days, `GBLO`, `USNY` or `JPTO`, is
no QuantLib calendar exactly, so it ships no alias, and a desk adds its own where it treats one as a calendar it uses.

The codes are FpML's, under the FpML® Public License, Version 3.0, whose text is in
`LICENSES/LicenseRef-FpML-Public-License-3.0.txt`. The mapping of each code to a nanoql name is nanoql's own work, not
part of the FpML Specifications, under the Apache License, Version 2.0. The licence's Exhibit A notice:

```text
The FpML Specifications of this document are subject to the FpML Public
License (the “License”); you may not use the FpML Specifications except in
compliance with the License. You may obtain a copy of the License at
https://www.FpML.org.

The FpML Specifications distributed under the License are distributed on an
“AS IS” basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
the License for the specific language governing rights and limitations under
the License.

The Licensor of the FpML Specifications is the International Swaps and
Derivatives Association, Inc. All Rights Reserved.
```

## The kinds

- **Time:** calendar presets and calendars based on one, with holidays added and removed; joint calendars; bespoke
  calendars; day counter presets, `Business252` on a calendar; currencies.
- **Indexes:** regions; Ibor indexes and families, overnight indexes, swap index families, overnight-indexed swap
  indexes, zero and year-on-year inflation indexes. A preset that takes constructor arguments (`AUCPI`, `GenericCPI`,
  the daily-tenor Libors) takes them from a record based on it.
- **Instrument conventions:** schedule, swap, OIS, CDS, zero-coupon and year-on-year inflation swap, cross-currency
  basis and fixed-against-floating swap, FX swap and forward, cap and floor, swaption, CMS, multiple-resets swap,
  year-on-year inflation cap and floor, and bond. Each resolves in a set to the builders and rate helpers it feeds:
  `set.Swap("EUR-SWAP").Builder(tenor, curve)`, `set.Ois("USD-SOFR").RateHelper(rate, tenor, context)`.
- **Commodities** (experimental, as the types they configure): commodity types, units of measure, conversion factors,
  payment terms, commodity indexes, and the commodity and quantity settings. A set's conversions,
  `set.UnitOfMeasureConversions()`, are QuantLib's known factors with the set's records added as QuantLib adds one, a
  record between a known factor's units replacing it; two known factors ship the right way round
  ([the divergences](divergences.md), the known unit factors). The settings' records named `CommoditySettings` and
  `QuantitySettings` are QuantLib's defaults; `EvaluationContext` keeps its own, QuantLib's.

Trade and market data are no conventions: a trade's dates, rates and nominal are a builder's arguments, and curves,
quotes, fixings, forward curves and exchange contracts are market objects, which no record holds.

## The provider and the set

- **`ConventionProvider`** has `QuantLib`, QuantLib's values; `Default`, the system's provider, `QuantLib` until set
  and restored by setting null; `Current`, the innermost scope's provider or else `Default`; and `Use(provider)`,
  which opens a scope and returns the `IDisposable` that closes it. A scope flows with asynchronous calls, as a culture
  does. A provider of a user's own, a database's or a service's, derives from it and overrides the members it
  configures, each taking the preset's name and QuantLib's reference.
- **`ConventionSet`** is the provider the XML records answer: `ConventionSet.QuantLib` holds the shipped records,
  `ConventionSet.Empty` none, and `With(snapshot)` layers a file on a set, resolving every record, so a misspelt name,
  a cycle or a value the library refuses fails when the file loads. A set is immutable. Its lookups build an object in
  the set's scope: `set.Calendar("TARGET")`, `set.IborIndex("Euribor6M", curve)`,
  `set.UnitOfMeasure("BarrelUnitOfMeasure")`.
- **`Empty`** is strict: an index, region, unit of measure or builder default it doesn't define is refused, so a file
  read onto it must define them. Calendars, day counters and currencies are code, and resolve to QuantLib's presets
  there too.
- **`set.Provenance()`** reports where each name comes from, QuantLib's records or a file's, and warns of a record that
  overrides no name and that nothing references, a likely misspelling (`Euribor6m`).
- **`ConventionRecords.Describe(name, value)`** turns a built object back into the records that rebuild it in a set.
- **A field is never ignored:** a route that can't take a field the record gives, such as a rate helper whose legs
  share one calendar given two, refuses the record, naming the route, the kind, the record and the field.

## What follows which provider

A provider is read only when an object is built, never while it prices, so a price doesn't depend on the scope it runs
in.

- **A preset** (a calendar, a currency, an index, a unit of measure) reads `Current` once, in its constructor, and its
  nested presets (an index's calendar, a currency's triangulation currency) are built in the same provider's scope.
- **An index keeps the provider it was built with,** and every clone of it keeps the same one.
- **A market builder takes the provider of its index,** reads its defaults from it when it builds, and builds in it.
  The CDS builder, with no index, takes the provider current when it is created.
- **The outer object's provider wins** for a builder built inside it: a swap index's underlying swap takes the swap
  index's provider, so a swap index built under one set on an Ibor index built under another takes the swap index's
  set's swap defaults and the Ibor index's set's conventions for its floating leg.
- **Whatever is built on an index later follows the index:** a swap index's underlying swap, a rate helper's swap when
  a bootstrap re-dates it, an engine's auxiliary swaps. An object with no index that builds after its construction
  keeps the provider current at its construction.
- **QuantLib's registry defaults,** `ExchangeRates.Default`, `UnitOfMeasureConversions.Default` and
  `EvaluationContext`'s defaults, are QuantLib's, whatever is current when they are first read.

### Set `Default` before building indexes

So setting `Default` reaches the objects built after it, and everything built on them. Objects built before keep their
conventions, and a builder on an earlier index takes that index's set, so no object mixes two sets. Set `Default` once
at startup, before building indexes; after a reload, rebuild the indexes to take it, as curves are rebuilt.

## An example: a SOFR curve

`tests/NanoQL.Tests/Extensions/SofrCurveExample.cs` builds a SOFR curve through an illustrative OIS convention, in a
file of its own, `SofrCurveExample.xml`:

```xml
<OisConvention name="USD-SOFR">
  <Index>Sofr</Index>
  <SettlementDays>2</SettlementDays>
  <PaymentLag>2</PaymentLag>
  <PaymentFrequency>Annual</PaymentFrequency>
  <PaymentCalendar>USGS</PaymentCalendar>
  <FixedLegDayCounter>ACT/360</FixedLegDayCounter>
</OisConvention>
```

Swaps settling two days after the trade, paid annually two business days after each period ends on the US government
bond calendar, the fixed leg on Actual/360; `USGS` and `ACT/360` are FpML's codes. The example reads the file over
QuantLib's presets, makes a rate helper per quote through the convention, and bootstraps the curve:

```csharp
ConventionSet set = ConventionSet.QuantLib.With(ConventionsXml.Read("SofrCurveExample.xml"));
var context = new EvaluationContext { EvaluationDate = new Date(15, Month.October, 2026) };
OisConvention sofr = set.Ois("USD-SOFR");

OISRateHelper[] helpers = [.. Quotes.Select(q => sofr.RateHelper(q.Rate, q.Tenor, context))];
var curve = new PiecewiseLogLinearDiscount(context.EvaluationDate, helpers, new Actual365Fixed(), context);
```

The quotes are illustrative SOFR swap rates from one month to ten years, 4.30% to 3.70%; the curve's list of
instruments is code. Every helper reprices to its quote within 1e-12. A 5-year SOFR swap paying 3.60% fixed on 10
million is then priced on the curve twice, on the example's conventions and on `MakeOIS`'s defaults:

```csharp
OvernightIndexedSwap onExample = sofr.Builder(new Period(5, TimeUnit.Years), curve)
    .WithFixedRate(0.036).WithNominal(10e6).ToOvernightIndexedSwap(context);
OvernightIndexedSwap onDefaults = new MakeOIS(new Period(5, TimeUnit.Years), set.OvernightIndex("Sofr", curve))
    .WithFixedRate(0.036).WithNominal(10e6).ToOvernightIndexedSwap(context);
```

| Conventions | Payment lag | NPV |
|---|---|---|
| The example's | 2 days | 0.0000, the 5-year quote's swap, at par |
| `MakeOIS`'s defaults | none | −3.1103 |
| Difference | | 3.1103 |

The two swaps differ only in their payment dates, so on a curve bootstrapped with the lag a swap without it is worth
3.11 less on 10 million.
