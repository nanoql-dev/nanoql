// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/optimization/constraint.hpp, .cpp: the members of Constraint the wheel doesn't expose, over the five
// constraints. A constraint argument is a spec (math/optimization/specs.hpp); an Array argument or result is a list of
// reals.

#include "math/optimization/specs.hpp"

using namespace QuantLib;

namespace harness {

    namespace {

        using namespace optimization;

        // A function of a constraint and an Array.
        Call withParams(const Args& a, json::value (*f)(const Constraint&, const Array&)) {
            arity(a, 2);
            ConstraintSpec c = constraint(a[0]);
            std::vector<Real> p = reals(a[1]);
            return [=] { return f(makeConstraint(c), makeArray(p)); };
        }

        const Register unit({
            {"Constraint::test",
             [](const Args& a) {
                 return withParams(a, [](const Constraint& c, const Array& p) { return json::value(c.test(p)); });
             }},
            {"Constraint::upperBound",
             [](const Args& a) {
                 return withParams(a, [](const Constraint& c, const Array& p) { return arrayResult(c.upperBound(p)); });
             }},
            {"Constraint::lowerBound",
             [](const Args& a) {
                 return withParams(a, [](const Constraint& c, const Array& p) { return arrayResult(c.lowerBound(p)); });
             }},
            {"Constraint::update",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 ConstraintSpec c = constraint(a[0]);
                 std::vector<Real> p = reals(a[1]), direction = reals(a[2]);
                 Real beta = real(a[3]);
                 // Array's sum checks the sizes, so a direction of another size is QuantLib's refusal.
                 return [=] {
                     Array params = makeArray(p);
                     Real diff = makeConstraint(c).update(params, makeArray(direction), beta);
                     return json::value(json::object{{"params", arrayResult(params)}, {"diff", diff}});
                 };
             }},
        });

    }

}
