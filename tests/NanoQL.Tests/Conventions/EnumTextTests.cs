// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// QuantLib's <c>operator&lt;&lt;</c> on an enum isn't ported where the enum's <see cref="object.ToString"/> gives
/// its text (playbook §2): the stream texts equal the member names, and no alias shares a value, so
/// <see cref="Enum.ToString()"/> can't give another name. The enums keep QuantLib's integers, so vectors carry them as
/// integers.
/// </summary>
public class EnumTextTests
{
    [Fact]
    public void TheOptionPositionDurationAveragingAndVolatilityTypesGiveQuantLibsTexts()
    {
        // option.hpp:100-109, position.cpp:26-35, duration.cpp:28-40, volatilitytype.hpp:34-44.
        Assert.Equal("Call", OptionType.Call.ToString());
        Assert.Equal("Put", OptionType.Put.ToString());
        Assert.Equal("Long", PositionType.Long.ToString());
        Assert.Equal("Short", PositionType.Short.ToString());
        Assert.Equal("Simple", DurationType.Simple.ToString());
        Assert.Equal("Macaulay", DurationType.Macaulay.ToString());
        Assert.Equal("Modified", DurationType.Modified.ToString());
        Assert.Equal("ShiftedLognormal", VolatilityType.ShiftedLognormal.ToString());
        Assert.Equal("Normal", VolatilityType.Normal.ToString());

        AssertMembers<OptionType>(new() { ["Put"] = -1, ["Call"] = 1 });
        AssertMembers<PositionType>(new() { ["Long"] = 0, ["Short"] = 1 });
        AssertMembers<DurationType>(new() { ["Simple"] = 0, ["Macaulay"] = 1, ["Modified"] = 2 });
        AssertMembers<VolatilityType>(new() { ["ShiftedLognormal"] = 0, ["Normal"] = 1 });

        // RateAveraging::Type has no operator<< (rateaveraging.hpp); its members and integers are QuantLib's.
        AssertMembers<RateAveragingType>(new() { ["Simple"] = 0, ["Compound"] = 1 });
    }

    // The enum's names and integers are exactly QuantLib's, and no two names share a value.
    private static void AssertMembers<T>(Dictionary<string, int> expected)
        where T : struct, Enum
    {
        string[] names = Enum.GetNames<T>();
        Assert.Equal(expected.Keys.Order(), names.Order());
        foreach (string name in names)
        {
            Assert.Equal(expected[name], Convert.ToInt32(Enum.Parse<T>(name), System.Globalization.CultureInfo.InvariantCulture));
        }

        Assert.Equal(names.Length, Enum.GetValues<T>().Distinct().Count());
    }
}
