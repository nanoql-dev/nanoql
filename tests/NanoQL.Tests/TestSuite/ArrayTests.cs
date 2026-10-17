// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/array.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Utilities;
using static NanoQL.Numerics.ArrayFunctions;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class ArrayTests
{
    // QL_EPSILON (qldefines.hpp): machine epsilon.
    private const double QL_EPSILON = 2.220446049250313e-16;

    // BOOST_ERROR and BOOST_CHECK record a failure and go on; the case fails at its end.
    private readonly List<string> errors = [];

    [Fact]
    public void TestConstruction()
    {
        // Testing array construction...

        // empty array
        var a1 = new Vector();
        if (!a1.Empty)
        {
            errors.Add($"default-initialized array is not empty (size = {a1.Size})");
        }

        // sized array
        Size size = 5;
        var a2 = new Vector(size);
        if (a2.Size != size)
        {
            errors.Add($"array not of the required size\n    required:  {size}\n    resulting: {a2.Size}");
        }

        // sized array, constant values
        Real value = 42.0;
        var a3 = new Vector(size, value);
        if (a3.Size != size)
        {
            errors.Add($"array not of the required size\n    required:  {size}\n    resulting: {a3.Size}");
        }

        Size i;
        for (i = 0; i < size; ++i)
        {
            if (a3[i] != value)
            {
                errors.Add($"{DataFormattersFunctions.Ordinal(i + 1)} element not with required value\n    required:  {value}\n    resulting: {a3[i]}");
            }
        }

        // sized array, incremental values
        Real increment = 3.0;
        var a4 = new Vector(size, value, increment);
        if (a4.Size != size)
        {
            errors.Add($"array not of the required size\n    required:  {size}\n    resulting: {a4.Size}");
        }

        for (i = 0; i < size; i++)
        {
            if (a4[i] != value + (i * increment))
            {
                errors.Add($"{DataFormattersFunctions.Ordinal(i + 1)} element not with required value\n    required:  {value + (i * increment)}\n    resulting: {a4[i]}");
            }
        }

        // copy constructor
        Vector a5 = a1.Clone();
        if (a5.Size != a1.Size)
        {
            errors.Add($"copy not of the same size as original\n    original:  {a1.Size}\n    copy:      {a5.Size}");
        }

        Vector a6 = a3.Clone();
        if (a6.Size != a3.Size)
        {
            errors.Add($"copy not of the same size as original\n    original:  {a3.Size}\n    copy:      {a6.Size}");
        }

        for (i = 0; i < a3.Size; i++)
        {
            if (a6[i] != a3[i])
            {
                errors.Add($"{DataFormattersFunctions.Ordinal(i + 1)} element of copy not with same value as original\n    original:  {a3[i]}\n    copy:      {a6[i]}");
            }
        }

        // transform
        var a10 = new Vector(5);
        for (i = 0; i < a10.Size; i++)
        {
            a10[i] = i;
        }

        // std::transform over begin() and end(), in place: the enumerator's elements are references.
        foreach (ref double x in a10)
        {
            x = FSquared(x);
        }

        for (i = 0; i < a10.Size; i++)
        {
            Real calculated = FSquared(i);
            if (Math.Abs(a10[i] - calculated) >= 1e-5)
            {
                errors.Add($"Array transform test failed {a10[i]} {calculated}");
            }
        }

        // recast initializer list to Real
        Vector a11 = [1, 2, 3, 4, 5];
        if (a2.Size != size)
        {
            errors.Add($"Array not of the required size\n    required:  {size}\n    resulting: {a2.Size}");
        }

        for (i = 0; i < size; ++i)
        {
            if (a11[i] != (Real)(i + 1))
            {
                errors.Add($"{DataFormattersFunctions.Ordinal(i + 1)} element not with required value\n    required:  {(Real)(i + 1)}\n    resulting: {a11[i]}");
            }
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void TestArrayFunctions()
    {
        // Testing array functions...

        static Vector GetArray()
        {
            var a = new Vector(5);
            for (Size i = 0; i < a.Size; ++i)
            {
                a[i] = Math.Sin(i) + 1.1;
            }

            return a;
        }

        Vector a = GetArray();

        const double exponential = -2.3;
        Vector p_lvalue = Pow(a, exponential);
        Vector e_lvalue = Exp(a);
        Vector l_lvalue = Log(a);
        Vector s_lvalue = Sqrt(a);
        Vector a_lvalue = Abs(a);
        Vector p_rvalue = Pow(GetArray(), exponential);
        Vector e_rvalue = Exp(GetArray());
        Vector l_rvalue = Log(GetArray());
        Vector s_rvalue = Sqrt(GetArray());
        Vector a_rvalue = Abs(GetArray());

        const double tol = 10 * QL_EPSILON;
        for (Size i = 0; i < a.Size; ++i)
        {
            if (Math.Abs(p_lvalue[i] - Math.Pow(a[i], exponential)) > tol)
            {
                Assert.Fail("Array function test Pow failed (lvalue)");
            }

            if (Math.Abs(p_rvalue[i] - Math.Pow(a[i], exponential)) > tol)
            {
                Assert.Fail("Array function test Pow failed (lvalue)");
            }

            if (Math.Abs(e_lvalue[i] - Math.Exp(a[i])) > tol)
            {
                Assert.Fail("Array function test Exp failed (lvalue)");
            }

            if (Math.Abs(e_rvalue[i] - Math.Exp(a[i])) > tol)
            {
                Assert.Fail("Array function test Exp failed (rvalue)");
            }

            if (Math.Abs(l_lvalue[i] - Math.Log(a[i])) > tol)
            {
                Assert.Fail("Array function test Log failed (lvalue)");
            }

            if (Math.Abs(l_rvalue[i] - Math.Log(a[i])) > tol)
            {
                Assert.Fail("Array function test Log failed (rvalue)");
            }

            if (Math.Abs(s_lvalue[i] - Math.Sqrt(a[i])) > tol)
            {
                Assert.Fail("Array function test Sqrt failed (lvalue)");
            }

            if (Math.Abs(s_rvalue[i] - Math.Sqrt(a[i])) > tol)
            {
                Assert.Fail("Array function test Sqrt failed (rvalue)");
            }

            if (Math.Abs(a_lvalue[i] - Math.Abs(a[i])) > tol)
            {
                Assert.Fail("Array function test Abs failed (lvalue)");
            }

            if (Math.Abs(a_rvalue[i] - Math.Abs(a[i])) > tol)
            {
                Assert.Fail("Array function test Abs failed (rvalue)");
            }
        }
    }

    // Adapted: without its checks that an iterator taken before a resize still points at the array's storage, lines
    // 215, 217 and 221 (the iterator and the two BOOST_CHECK(iter == a.begin())): a C# reference to a Vector's
    // storage is a span, and Vector.Resize moves the elements to new storage when it shrinks, where QuantLib keeps
    // its storage (Vector.Resize's remarks). The resizes and size checks around them are kept.
    [Fact]
    public void TestArrayResize()
    {
        // Testing array resize...

        var a = new Vector(10, 1.0, 1.0);

        for (Size i = 0; i < 10; ++i)
        {
            CheckClose(a[i], 1 + i, 10 * QL_EPSILON);
        }

        a.Resize(5);
        Check(a.Size == 5, "a.size() == 5");

        for (Size i = 0; i < 5; ++i)
        {
            CheckClose(a[i], 1 + i, 10 * QL_EPSILON);
        }

        a.Resize(15);
        Check(a.Size == 15, "a.size() == 15");

        for (Size i = 0; i < 5; ++i)
        {
            CheckClose(a[i], 1 + i, 10 * QL_EPSILON);
        }

        a.Resize(a.Size);

        a.Resize(10);
        Check(a.Size == 10, "a.size() == 10");

        Assert.Empty(errors);
    }

    [Fact]
    public void TestArrayOperators()
    {
        // Testing array operators...

        static Vector GetArray() => [1.1, 2.2, 3.3];

        Vector a = GetArray();

        Vector positive = [1.1, 2.2, 3.3];
        Vector lvalue_positive = +a;
        Vector rvalue_positive = +GetArray();

        CheckCloseArray(lvalue_positive, positive);
        CheckCloseArray(rvalue_positive, positive);

        Vector negative = [-1.1, -2.2, -3.3];
        Vector lvalue_negative = -a;
        Vector rvalue_negative = -GetArray();

        CheckCloseArray(lvalue_negative, negative);
        CheckCloseArray(rvalue_negative, negative);

        Vector array_sum = [2.2, 4.4, 6.6];
        Vector lvalue_lvalue_sum = a + a;
        Vector lvalue_rvalue_sum = a + GetArray();
        Vector rvalue_lvalue_sum = GetArray() + a;
        Vector rvalue_rvalue_sum = GetArray() + GetArray();

        CheckCloseArray(lvalue_lvalue_sum, array_sum);
        CheckCloseArray(lvalue_rvalue_sum, array_sum);
        CheckCloseArray(rvalue_lvalue_sum, array_sum);
        CheckCloseArray(rvalue_rvalue_sum, array_sum);

        Vector scalar_sum = [2.2, 3.3, 4.4];
        Vector lvalue_real_sum = a + 1.1;
        Vector rvalue_real_sum = GetArray() + 1.1;
        Vector real_lvalue_sum = 1.1 + a;
        Vector real_rvalue_sum = 1.1 + GetArray();

        CheckCloseArray(lvalue_real_sum, scalar_sum);
        CheckCloseArray(rvalue_real_sum, scalar_sum);
        CheckCloseArray(real_lvalue_sum, scalar_sum);
        CheckCloseArray(real_rvalue_sum, scalar_sum);

        Vector array_difference = [0.0, 0.0, 0.0];
        Vector lvalue_lvalue_difference = a - a;
        Vector lvalue_rvalue_difference = a - GetArray();
        Vector rvalue_lvalue_difference = GetArray() - a;
        Vector rvalue_rvalue_difference = GetArray() - GetArray();

        CheckCloseArray(lvalue_lvalue_difference, array_difference);
        CheckCloseArray(lvalue_rvalue_difference, array_difference);
        CheckCloseArray(rvalue_lvalue_difference, array_difference);
        CheckCloseArray(rvalue_rvalue_difference, array_difference);

        Vector scalar_difference_1 = [0.0, +1.1, +2.2];
        Vector scalar_difference_2 = [0.0, -1.1, -2.2];
        Vector lvalue_real_difference = a - 1.1;
        Vector rvalue_real_difference = GetArray() - 1.1;
        Vector real_lvalue_difference = 1.1 - a;
        Vector real_rvalue_difference = 1.1 - GetArray();

        CheckCloseArray(lvalue_real_difference, scalar_difference_1);
        CheckCloseArray(rvalue_real_difference, scalar_difference_1);
        CheckCloseArray(real_lvalue_difference, scalar_difference_2);
        CheckCloseArray(real_rvalue_difference, scalar_difference_2);

        Vector array_product = [1.1 * 1.1, 2.2 * 2.2, 3.3 * 3.3];
        Vector lvalue_lvalue_product = a * a;
        Vector lvalue_rvalue_product = a * GetArray();
        Vector rvalue_lvalue_product = GetArray() * a;
        Vector rvalue_rvalue_product = GetArray() * GetArray();

        CheckCloseArray(lvalue_lvalue_product, array_product);
        CheckCloseArray(lvalue_rvalue_product, array_product);
        CheckCloseArray(rvalue_lvalue_product, array_product);
        CheckCloseArray(rvalue_rvalue_product, array_product);

        Vector scalar_product = [1.1 * 1.1, 2.2 * 1.1, 3.3 * 1.1];
        Vector lvalue_real_product = a * 1.1;
        Vector rvalue_real_product = GetArray() * 1.1;
        Vector real_lvalue_product = 1.1 * a;
        Vector real_rvalue_product = 1.1 * GetArray();

        CheckCloseArray(lvalue_real_product, scalar_product);
        CheckCloseArray(rvalue_real_product, scalar_product);
        CheckCloseArray(real_lvalue_product, scalar_product);
        CheckCloseArray(real_rvalue_product, scalar_product);

        Vector array_quotient = [1.0, 1.0, 1.0];
        Vector lvalue_lvalue_quotient = a / a;
        Vector lvalue_rvalue_quotient = a / GetArray();
        Vector rvalue_lvalue_quotient = GetArray() / a;
        Vector rvalue_rvalue_quotient = GetArray() / GetArray();

        CheckCloseArray(lvalue_lvalue_quotient, array_quotient);
        CheckCloseArray(lvalue_rvalue_quotient, array_quotient);
        CheckCloseArray(rvalue_lvalue_quotient, array_quotient);
        CheckCloseArray(rvalue_rvalue_quotient, array_quotient);

        Vector scalar_quotient_1 = [1.1 / 1.1, 2.2 / 1.1, 3.3 / 1.1];
        Vector scalar_quotient_2 = [1.1 / 1.1, 1.1 / 2.2, 1.1 / 3.3];
        Vector lvalue_real_quotient = a / 1.1;
        Vector rvalue_real_quotient = GetArray() / 1.1;
        Vector real_lvalue_quotient = 1.1 / a;
        Vector real_rvalue_quotient = 1.1 / GetArray();

        CheckCloseArray(lvalue_real_quotient, scalar_quotient_1);
        CheckCloseArray(rvalue_real_quotient, scalar_quotient_1);
        CheckCloseArray(real_lvalue_quotient, scalar_quotient_2);
        CheckCloseArray(real_rvalue_quotient, scalar_quotient_2);

        Assert.Empty(errors);
    }

    // QuantLib's FSquared functor.
    private static Real FSquared(Real x) => x * x;

    // Boost's safe_fpt_division: f1 / f2, without overflow or underflow.
    private static double SafeDivision(double f1, double f2)
    {
        if (f2 < 1.0 && f1 > f2 * double.MaxValue)
        {
            return double.MaxValue;
        }

        if (Math.Abs(f1) <= NormalMin || (f2 > 1.0 && f1 < f2 * NormalMin))
        {
            return 0.0;
        }

        return f1 / f2;
    }

    // std::numeric_limits<double>::min(): the smallest normal double.
    private const double NormalMin = 2.2250738585072014e-308;

    // QL_CHECK_CLOSE, Boost's BOOST_CHECK_CLOSE: |actual - expected| within the tolerance, in percent, of both values.
    private void CheckClose(double actual, double expected, double tolerancePercent)
    {
        double diff = Math.Abs(actual - expected), fraction = tolerancePercent * 0.01;
        if (!(SafeDivision(diff, Math.Abs(expected)) <= fraction && SafeDivision(diff, Math.Abs(actual)) <= fraction))
        {
            errors.Add($"difference between {actual:R} and {expected:R} exceeds {tolerancePercent:R}%");
        }
    }

    private void Check(bool condition, string text)
    {
        if (!condition)
        {
            errors.Add($"check {text} has failed");
        }
    }

    // QL_CHECK_CLOSE_ARRAY: the sizes required equal, then each element within 100 epsilon percent.
    private void CheckCloseArray(Vector actual, Vector expected)
    {
        Assert.True(actual.Size == expected.Size, "critical check actual.size() == expected.size() has failed");
        for (int i = 0; i < actual.Size; i++)
        {
            CheckClose(actual[i], expected[i], 100 * QL_EPSILON);
        }
    }
}
