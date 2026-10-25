// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/methods/finitedifferences/operators/modtriplebandlinearop.hpp, which the wheel doesn't expose: a triple-band
// operator whose bands are written through its accessors.
//
// "ModTripleBandLinearOp::outputs" takes (locations, direction, derivative, writes, r, s): a predefined mesher with one
// direction per array of locations; a FirstDerivativeOp ("first") or SecondDerivativeOp ("second") in the direction,
// copied into a ModTripleBandLinearOp, or ("none") a ModTripleBandLinearOp(direction, mesher) whose bands the writes
// must then set at every point (QuantLib leaves them unset); the writes, each [band, i, value] with band "lower",
// "diag" or "upper", in order. It returns {"lower", "diag", "upper"}, the bands read back at every point, "apply", the
// operator applied to r, and "solveSplitting", solve_splitting(r, s).

#include "convert.hpp"

#include <ql/methods/finitedifferences/meshers/fdmmeshercomposite.hpp>
#include <ql/methods/finitedifferences/meshers/predefined1dmesher.hpp>
#include <ql/methods/finitedifferences/operators/firstderivativeop.hpp>
#include <ql/methods/finitedifferences/operators/modtriplebandlinearop.hpp>
#include <ql/methods/finitedifferences/operators/secondderivativeop.hpp>

#include <memory>
#include <string>
#include <tuple>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        json::array toJson(const Array& a) {
            json::array out;
            for (Real x : a)
                out.emplace_back(x);
            return out;
        }

        const Register unit({
            {"ModTripleBandLinearOp::outputs",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 if (!a[0].is_array() || a[0].get_array().empty())
                     throw InvalidParams("expected an array of locations per direction, got " + json::serialize(a[0]));
                 std::vector<std::vector<Real>> locations;
                 for (const json::value& x : a[0].get_array())
                     locations.push_back(reals(x));
                 Integer direction = integer(a[1]);
                 std::string derivative = text(a[2]);
                 if (derivative != "first" && derivative != "second" && derivative != "none")
                     throw InvalidParams("a derivative \"first\", \"second\" or \"none\", got " + derivative);
                 if (!a[3].is_array())
                     throw InvalidParams("expected [band, i, value] writes, got " + json::serialize(a[3]));
                 std::vector<std::tuple<std::string, Integer, Real>> writes;
                 for (const json::value& w : a[3].get_array()) {
                     if (!w.is_array() || w.get_array().size() != 3)
                         throw InvalidParams("expected a [band, i, value] write, got " + json::serialize(w));
                     std::string band = text(w.get_array()[0]);
                     if (band != "lower" && band != "diag" && band != "upper")
                         throw InvalidParams("a band \"lower\", \"diag\" or \"upper\", got " + band);
                     writes.emplace_back(band, integer(w.get_array()[1]), real(w.get_array()[2]));
                 }
                 std::vector<Real> r = reals(a[4]);
                 Real s = real(a[5]);
                 Size n = 1;
                 for (const auto& x : locations)
                     n *= x.size();
                 if (direction < 0 || static_cast<Size>(direction) >= locations.size())
                     throw InvalidParams("the direction must name one of the mesher's");
                 if (r.size() != n)
                     throw InvalidParams("r must hold one value per mesher point");
                 for (const auto& w : writes)
                     if (std::get<1>(w) < 0 || static_cast<Size>(std::get<1>(w)) >= n)
                         throw InvalidParams("a write's index must name a mesher point");
                 return [=] {
                     std::vector<ext::shared_ptr<Fdm1dMesher>> meshers;
                     for (const auto& x : locations)
                         meshers.push_back(ext::make_shared<Predefined1dMesher>(x));
                     auto mesher = ext::make_shared<FdmMesherComposite>(meshers);
                     const Size d = static_cast<Size>(direction);
                     std::unique_ptr<ModTripleBandLinearOp> op;
                     if (derivative == "first")
                         op = std::make_unique<ModTripleBandLinearOp>(FirstDerivativeOp(d, mesher));
                     else if (derivative == "second")
                         op = std::make_unique<ModTripleBandLinearOp>(SecondDerivativeOp(d, mesher));
                     else
                         op = std::make_unique<ModTripleBandLinearOp>(d, mesher);
                     for (const auto& [band, i, value] : writes) {
                         const Size k = static_cast<Size>(i);
                         if (band == "lower")
                             op->lower(k) = value;
                         else if (band == "diag")
                             op->diag(k) = value;
                         else
                             op->upper(k) = value;
                     }
                     const ModTripleBandLinearOp& read = *op;
                     json::array lower, diag, upper;
                     for (Size k = 0; k < n; ++k) {
                         lower.emplace_back(read.lower(k));
                         diag.emplace_back(read.diag(k));
                         upper.emplace_back(read.upper(k));
                     }
                     Array ra(r.begin(), r.end());
                     return json::value(json::object{{"lower", lower},
                                                     {"diag", diag},
                                                     {"upper", upper},
                                                     {"apply", toJson(op->apply(ra))},
                                                     {"solveSplitting", toJson(op->solve_splitting(ra, s))}});
                 };
             }},
        });

    }

}
