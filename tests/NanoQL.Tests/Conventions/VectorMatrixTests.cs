// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="Vector"/> and <see cref="Matrix"/> are working types (docs/porting-playbook.md §2.5): a compound
/// assignment changes the object in place, so an alias sees it, as QuantLib's <c>operator+=</c> changes its target,
/// while a binary operator returns a new object; and <c>Clone()</c> is a deep copy, equal to the original until either
/// changes.
/// </summary>
public class VectorMatrixTests
{
    [Fact]
    public void VectorCompoundAssignmentInPlaceAndCloneDeep()
    {
        Vector v = [1.0, 2.0, 3.0];
        Vector alias = v;
        Vector w = [10.0, 20.0, 30.0];

        Vector sum = v + w;
        Assert.NotSame(v, sum);
        Assert.Equal(new double[] { 1.0, 2.0, 3.0 }, v.AsSpan().ToArray());

        v += w;
        Assert.Same(alias, v);
        Assert.Equal(new double[] { 11.0, 22.0, 33.0 }, alias.AsSpan().ToArray());
        v -= 1.0;
        v *= 2.0;
        v /= w;
        v *= w;
        v /= 2.0;
        v += 1.0;
        v -= w;
        Assert.Equal(new double[] { 1.0, 2.0, 3.0 }, alias.AsSpan().ToArray());

        Vector clone = v.Clone();
        Assert.NotSame(v, clone);
        Assert.True(clone.Equals(v));
        Assert.True(clone.Equals((object)v));
        Assert.Equal(v.GetHashCode(), clone.GetHashCode());
        clone[0] = 42.0;
        clone.Resize(5);
        Assert.Equal(new double[] { 1.0, 2.0, 3.0 }, v.AsSpan().ToArray());
        Assert.False(clone.Equals(v));

        // A NaN equals itself under Equals, as double.Equals has it, but not under QuantLib's ==.
        Vector withNaN = [double.NaN];
        Assert.True(withNaN.Equals(withNaN.Clone()));
        Assert.False(withNaN == withNaN.Clone());
    }

    [Fact]
    public void MatrixCompoundAssignmentInPlaceAndCloneDeep()
    {
        var m = new Matrix(new double[,] { { 1.0, 2.0 }, { 3.0, 4.0 } });
        Matrix alias = m;
        var n = new Matrix(2, 2, 10.0);

        Matrix sum = m + n;
        Assert.NotSame(m, sum);
        Assert.Equal(1.0, m[0, 0]);

        m += n;
        Assert.Same(alias, m);
        Assert.Equal(11.0, alias[0, 0]);
        Assert.Equal(14.0, alias[1, 1]);
        m -= n;
        m *= 4.0;
        m /= 2.0;
        Assert.Equal(new Matrix(new double[,] { { 2.0, 4.0 }, { 6.0, 8.0 } }), alias);

        Matrix clone = m.Clone();
        Assert.NotSame(m, clone);
        Assert.True(clone.Equals(m));
        Assert.True(clone.Equals((object)m));
        Assert.Equal(m.GetHashCode(), clone.GetHashCode());
        clone[1, 0] = 42.0;
        clone.Row(0)[1] = -1.0;
        Assert.Equal(6.0, m[1, 0]);
        Assert.Equal(4.0, m[0, 1]);
        Assert.False(clone.Equals(m));

        // A NaN equals itself under Equals, as double.Equals has it, but not under QuantLib's ==.
        var withNaN = new Matrix(1, 1, double.NaN);
        Assert.True(withNaN.Equals(withNaN.Clone()));
        Assert.False(withNaN == withNaN.Clone());
    }
}
