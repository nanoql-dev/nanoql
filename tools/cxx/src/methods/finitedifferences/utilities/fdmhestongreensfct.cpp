// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/methods/finitedifferences/utilities/fdmhestongreensfct.hpp, .cpp, which the wheel binds for its enumeration
// only: the Heston model's Green's function, the density after a short time from the spot and the initial variance.
//
// "FdmHestonGreensFct::get" takes (process, locations, trafoType, l0, t, algorithm): the process
// models/equity/hestonspec.hpp's spec; a predefined mesher of the log spot (direction 0) and the variance (direction 1,
// its log under the log transformation); QuantLib's TransformationType (0 plain, 1 power, 2 log); the leverage l0; the
// time; QuantLib's Algorithm (0 zero correlation, 1 Gaussian, 2 semi-analytical). It returns the density at every
// point, in the layout's order.

#include "convert.hpp"
#include "models/equity/hestonspec.hpp"

#include <ql/methods/finitedifferences/meshers/fdmmeshercomposite.hpp>
#include <ql/methods/finitedifferences/meshers/predefined1dmesher.hpp>
#include <ql/methods/finitedifferences/utilities/fdmhestongreensfct.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"FdmHestonGreensFct::get",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 equity::ProcessSpec p = equity::process(a[0]);
                 if (!a[1].is_array() || a[1].get_array().size() != 2)
                     throw InvalidParams("expected the log spot's and the variance's locations, got " +
                                         json::serialize(a[1]));
                 std::vector<Real> x = reals(a[1].get_array()[0]), v = reals(a[1].get_array()[1]);
                 auto trafoType = enumeration<FdmSquareRootFwdOp::TransformationType>(a[2]);
                 Real l0 = real(a[3]);
                 Time t = real(a[4]);
                 auto algorithm = enumeration<FdmHestonGreensFct::Algorithm>(a[5]);
                 return [=] {
                     auto mesher = ext::make_shared<FdmMesherComposite>(ext::make_shared<Predefined1dMesher>(x),
                                                                        ext::make_shared<Predefined1dMesher>(v));
                     Array density =
                         FdmHestonGreensFct(mesher, equity::makeProcess(p), trafoType, l0).get(t, algorithm);
                     json::array out;
                     for (Real d : density)
                         out.emplace_back(d);
                     return json::value(out);
                 };
             }},
        });

    }

}
