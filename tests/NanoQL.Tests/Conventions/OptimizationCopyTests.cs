// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The optimizers' boundary between values and working types (docs/porting-playbook.md §2.5): <see cref="Problem"/>,
/// a value, copies the initial value it is given, as <see cref="NonhomogeneousBoundaryConstraint"/> copies its bounds;
/// <see cref="ProblemState"/>'s <see cref="ProblemState.CurrentValue"/> setter copies, as QuantLib's by-value
/// <c>setCurrentValue</c> does; and <see cref="Constraint.Update"/> moves the vector it is given in place, as
/// QuantLib's <c>params += diff*direction</c> does, so a simplex vertex moves.
/// </summary>
public class OptimizationCopyTests
{
    [Fact]
    public void ProblemCopiesItsInitialValue()
    {
        Vector initial = [1.0, 2.0];
        Vector low = [0.0, 0.0], high = [3.0, 3.0];
        var constraint = new NonhomogeneousBoundaryConstraint(low.AsSpan(), high.AsSpan());
        var problem = new Problem(new Quadratic(), constraint, initial.AsSpan());

        initial[0] = 42.0;
        low[0] = 5.0;
        high[1] = -5.0;
        Assert.Equal(new[] { 1.0, 2.0 }, problem.InitialValue);
        Assert.Equal(new[] { 0.0, 0.0 }, constraint.LowerBound([1.0, 1.0]).AsSpan().ToArray());
        Assert.Equal(new[] { 3.0, 3.0 }, constraint.UpperBound([1.0, 1.0]).AsSpan().ToArray());

        // A bound the constraint returns is the caller's own.
        Vector upper = constraint.UpperBound([1.0, 1.0]);
        upper[0] = 99.0;
        Assert.Equal(3.0, constraint.UpperBound([1.0, 1.0])[0]);

        // A state built from the problem starts from a copy: changing it, or minimizing, leaves the problem's.
        var state = new ProblemState(problem);
        state.CurrentValue[1] = 2.5;
        new Simplex(0.1).Minimize(state, new EndCriteria(100, 10, 1e-8, 1e-8, 1e-8));
        Assert.Equal(new[] { 1.0, 2.0 }, problem.InitialValue);
        Assert.Equal(new[] { 1.0, 2.0 }, new ProblemState(problem).CurrentValue.AsSpan().ToArray());
    }

    [Fact]
    public void ProblemStateCurrentValueSetterCopies()
    {
        var state = new ProblemState(new Problem(new Quadratic(), new NoConstraint(), [1.0, 2.0]));
        Vector value = [3.0, 4.0];
        state.CurrentValue = value;
        Assert.NotSame(value, state.CurrentValue);
        value[0] = 42.0;
        Assert.Equal(3.0, state.CurrentValue[0]);

        // The getter returns the state's own vector, as QuantLib's const Array& does.
        Assert.Same(state.CurrentValue, state.CurrentValue);

        // A compound assignment on the property is the binary operator and the setter (SD30): the state gets a new
        // vector, and one read before keeps the old value.
        Vector before = state.CurrentValue;
        state.CurrentValue += new Vector(2, 1.0);
        Assert.NotSame(before, state.CurrentValue);
        Assert.Equal(3.0, before[0]);
        Assert.Equal(new[] { 4.0, 5.0 }, state.CurrentValue.AsSpan().ToArray());

        Assert.Throws<ArgumentNullException>(() => state.CurrentValue = null!);
    }

    [Fact]
    public void ConstraintUpdateMovesItsArgumentInPlace()
    {
        var constraint = new BoundaryConstraint(-1.0, 1.0);
        Vector p = [0.0, 0.5];
        Vector alias = p;

        // The steps 2 and 1 leave the box; 0.5 reaches [0.5, 1], on its edge.
        double diff = constraint.Update(p, [1.0, 1.0], 2.0);
        Assert.Equal(0.5, diff);
        Assert.Same(alias, p);
        Assert.Equal(new[] { 0.5, 1.0 }, alias.AsSpan().ToArray());

        // An array element moves, as Simplex's vertices do; the other elements don't.
        Vector[] vertices = [[0.0, 0.0], [0.0, 0.0]];
        Vector vertex = vertices[1];
        constraint.Update(vertices[1], [1.0, 0.0], 0.25);
        Assert.Same(vertex, vertices[1]);
        Assert.Equal(new[] { 0.25, 0.0 }, vertices[1].AsSpan().ToArray());
        Assert.Equal(new[] { 0.0, 0.0 }, vertices[0].AsSpan().ToArray());

        // The direction isn't changed.
        Vector direction = [1.0, -1.0];
        constraint.Update(p, direction, 0.1);
        Assert.Equal(new[] { 1.0, -1.0 }, direction.AsSpan().ToArray());
    }

    private sealed class Quadratic : CostFunction
    {
        public override double Value(Vector x) => ArrayFunctions.DotProduct(x, x);

        public override Vector Values(Vector x) => new(1, Value(x));
    }
}
