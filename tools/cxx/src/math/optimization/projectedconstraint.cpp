// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/optimization/projectedconstraint.hpp: ProjectedConstraint, which no wheel class exposes, built inside each
// call by either constructor: its form "values" for (constraint, parameterValues, fixParameters), "projection" for
// (constraint, Projection(parameterValues, fixParameters)). The constraint is a spec (math/optimization/specs.hpp);
// the flags a list of booleans, empty for QuantLib's default, all free.
//
//     ProjectedConstraint::test         (form, constraint, parameterValues, fixParameters, params)   a boolean
//     ProjectedConstraint::upperBound   (form, constraint, parameterValues, fixParameters, params)   an Array
//     ProjectedConstraint::lowerBound   (form, constraint, parameterValues, fixParameters, params)   an Array
//
// or QuantLib's refusal, the projection's included.

#include "math/optimization/specs.hpp"

#include <ql/math/optimization/projectedconstraint.hpp>
#include <ql/math/optimization/projection.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        using namespace optimization;

        std::vector<bool> flags(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected an array of booleans, got " + json::serialize(v));
            std::vector<bool> out;
            for (const json::value& x : v.get_array())
                out.push_back(boolean(x));
            return out;
        }

        ProjectedConstraint make(bool byProjection, const ConstraintSpec& c, const std::vector<Real>& values,
                                 const std::vector<bool>& fix) {
            if (byProjection)
                return ProjectedConstraint(makeConstraint(c), Projection(makeArray(values), fix));
            return ProjectedConstraint(makeConstraint(c), makeArray(values), fix);
        }

        // A function of a projected constraint and an Array.
        Call withConstraint(const Args& a, json::value (*f)(const Constraint&, const Array&)) {
            arity(a, 5);
            std::string form = text(a[0]);
            if (form != "values" && form != "projection")
                throw InvalidParams("a projected constraint's form is values or projection, not " + form);
            bool byProjection = form == "projection";
            ConstraintSpec c = constraint(a[1]);
            std::vector<Real> values = reals(a[2]);
            std::vector<bool> fix = flags(a[3]);
            std::vector<Real> p = reals(a[4]);
            return [=] { return f(make(byProjection, c, values, fix), makeArray(p)); };
        }

        const Register unit({
            {"ProjectedConstraint::test",
             [](const Args& a) {
                 return withConstraint(a, [](const Constraint& c, const Array& p) { return json::value(c.test(p)); });
             }},
            {"ProjectedConstraint::upperBound",
             [](const Args& a) {
                 return withConstraint(a, [](const Constraint& c, const Array& p) { return arrayResult(c.upperBound(p)); });
             }},
            {"ProjectedConstraint::lowerBound",
             [](const Args& a) {
                 return withConstraint(a, [](const Constraint& c, const Array& p) { return arrayResult(c.lowerBound(p)); });
             }},
        });

    }

}
