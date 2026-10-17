// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The encoding self-vector, <c>tests/vectors/harness/encoding.json</c>: QuantLib's null sentinels, the non-finite
/// reals, plain literals, a scaled amount and a throw, as the wheel writes them, read back to the values nanoql uses.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class EncodingVectorTests
{
    [Fact]
    public void Encoding() => Check("harness/encoding");

    /// <summary>
    /// Reads back one encoding self-vector: the wheel's, or its twin from the C++ harness
    /// (<see cref="EncodingCxxVectorTests"/>).
    /// </summary>
    /// <param name="path">The file's path under <c>tests/vectors</c>, without <c>.json</c>.</param>
    internal static void Check(string path)
    {
        var file = VectorFile.Load(path);
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            switch (c.Name)
            {
                case "null sentinels":
                    check.Output(c, "nullReal", (double?)null);
                    check.Output(c, "nullInteger", (int?)null);
                    check.Output(c, "nullDate", null);
                    break;
                case "non-finite reals":
                    check.Output(c, "nan", double.NaN);
                    check.Output(c, "infinity", double.PositiveInfinity);
                    check.Output(c, "minusInfinity", double.NegativeInfinity);
                    break;
                case "literals":
                    check.Output(c, "integer", 46310);
                    check.Output(c, "real", 0.1 + 0.2);
                    check.Output(c, "boolean", true);
                    check.Output(c, "string", "Actual/360");
                    check.Output(c, "array", new[] { 1.0, -2.5, 1e-300 });
                    break;
                case "scaled amount":
                    // Inside the amount's own scale (1e-10 x 1e6), outside 1.0's.
                    check.Output(c, "amount", 1234.5 + 5e-5);
                    break;
                case "throwing call":
                    check.Output(c, "result", () => throw new NanoQLException("day 29 outside month (2) day-range [1,28]"));
                    break;
                default:
                    Assert.Fail($"unexpected case '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }
}
