// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The VanillaStorageOption vectors, <c>tests/vectors/cxx/instruments/vanillastorageoption.json</c>:
/// <see cref="VanillaStorageOption"/>'s validation of its quantities and its expiry, against QuantLib's through the C++
/// harness.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class VanillaStorageOptionCxxVectorTests
{
    [Fact]
    public void VanillaStorageOptionCxxVector()
    {
        var file = VectorFile.Load("cxx/instruments/vanillastorageoption");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var option = new VanillaStorageOption(
                new BermudanExercise(c.DateInputs("dates")), c.Double("capacity"), c.Double("load"), c.Double("changeRate"));
            if (c.Name.StartsWith("isExpired", StringComparison.Ordinal))
            {
                check.Output(c, "value", option.IsExpired(c.Context()));
            }
            else
            {
                check.Output(c, c.Expected.TryGetProperty("error", out _) ? "error" : "value", () => Message(option.Validate));
            }
        }

        check.Complete();
    }

    // The refusal's message, QuantLib's text, or "valid" where none comes.
    private static string Message(Action act)
    {
        try
        {
            act();
        }
        catch (NanoQLException e)
        {
            return e.Message;
        }

        return "valid";
    }
}
