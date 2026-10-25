// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/methods/finitedifferences/utilities/fdmmesherintegral.hpp, .cpp, which the wheel doesn't expose: the integral of
// a function's values over a mesher, one direction at a time from the last.
//
// "FdmMesherIntegral::integrate" takes (locations, f, rule): a predefined mesher with one direction per array of
// locations, the values at its points in its layout's order, and the rule "simpson" (DiscreteSimpsonIntegral) or
// "trapezoid" (DiscreteTrapezoidIntegral). It returns the integral. QuantLib's object keeps a reference to its
// integrator, so it is built and used in one expression, as QuantLib's own callers use it.

#include "convert.hpp"

#include <ql/math/integrals/discreteintegrals.hpp>
#include <ql/methods/finitedifferences/meshers/fdmmeshercomposite.hpp>
#include <ql/methods/finitedifferences/meshers/predefined1dmesher.hpp>
#include <ql/methods/finitedifferences/utilities/fdmmesherintegral.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"FdmMesherIntegral::integrate",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 if (!a[0].is_array() || a[0].get_array().empty())
                     throw InvalidParams("expected an array of locations per direction, got " + json::serialize(a[0]));
                 std::vector<std::vector<Real>> locations;
                 for (const json::value& x : a[0].get_array())
                     locations.push_back(reals(x));
                 std::vector<Real> f = reals(a[1]);
                 std::string rule = text(a[2]);
                 if (rule != "simpson" && rule != "trapezoid")
                     throw InvalidParams("a rule \"simpson\" or \"trapezoid\", got " + rule);
                 return [=] {
                     std::vector<ext::shared_ptr<Fdm1dMesher>> meshers;
                     for (const auto& x : locations)
                         meshers.push_back(ext::make_shared<Predefined1dMesher>(x));
                     auto mesher = ext::make_shared<FdmMesherComposite>(meshers);
                     Array values(f.begin(), f.end());
                     Real integral = rule == "simpson"
                                         ? FdmMesherIntegral(mesher, DiscreteSimpsonIntegral()).integrate(values)
                                         : FdmMesherIntegral(mesher, DiscreteTrapezoidIntegral()).integrate(values);
                     return json::value(integral);
                 };
             }},
        });

    }

}
