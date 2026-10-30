// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Extensions.Conventions;
using NanoQL.Indexes;
using NanoQL.Tests.Extensions;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// QuantLib's 27 Ibor families at every tenor the presets' probes used, through <see cref="ConventionSet.QuantLib"/>'s
/// family records against QuantLib's values, and against the coded family field by field; a tenor whose unit a family
/// doesn't take is refused both ways.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IndexFamiliesVectorTests
{
    [Fact]
    public void IndexFamiliesVector()
    {
        var file = VectorFile.Load("conventions/index_families");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            string family = c.String("family");
            Period tenor = Utilities.PeriodParser.Parse(c.String("tenor"));
            if (c.Expected.TryGetProperty("index", out _))
            {
                // a unit the family takes no tenor in: refused through the set as by the coded family
                check.Output(c, "index", () => ConventionSet.QuantLib.IborIndex(family, tenor));
                Assert.ThrowsAny<ArgumentException>(() => CodedPresets.IborFamilies[family](tenor));
                continue;
            }

            IborIndex index = ConventionSet.QuantLib.IborIndex(family, tenor);
            check.Output(c, "name", index.Name);
            check.Output(c, "familyName", index.FamilyName);
            check.Output(c, "tenor", new object[] { index.Tenor.Length, index.Tenor.Units });
            check.Output(c, "fixingDays", index.FixingDays);
            check.Output(c, "currency", index.Currency!.Code);
            check.Output(c, "fixingCalendar", index.FixingCalendar.Name);
            check.Output(c, "businessDayConvention", index.BusinessDayConvention);
            check.Output(c, "endOfMonth", index.EndOfMonth);
            check.Output(c, "dayCounter", index.DayCounter.Name);

            // the coded family, field by field
            Assert.Equal(IndexFields.Of(CodedPresets.IborFamilies[family](tenor)), IndexFields.Of(index));
        }

        check.Complete();
    }
}
