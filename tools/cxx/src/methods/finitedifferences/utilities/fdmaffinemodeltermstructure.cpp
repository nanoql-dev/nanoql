// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/methods/finitedifferences/utilities/fdmaffinemodeltermstructure.hpp, .cpp, which the wheel doesn't expose: the
// curve an affine model implies at a state of its factors. "FdmAffineModelTermStructure::discount" takes (curve, model,
// parameters, state, referenceDate, times, dates): the model a Hull-White ([a, sigma]) or a G2 ([a, sigma, b, eta,
// rho]) on the curve (a curve spec of indexes/indexspec.hpp), the curve built on a null calendar and the model curve's
// day counter from referenceDate, the model's reference date the curve's, the state the factors (one for Hull-White,
// its short rate; two for G2). It returns {"discount", "discountAt"}: the discount at each time and at each date
// serial.

#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/math/array.hpp>
#include <ql/methods/finitedifferences/utilities/fdmaffinemodeltermstructure.hpp>
#include <ql/models/shortrate/onefactormodels/hullwhite.hpp>
#include <ql/models/shortrate/twofactormodels/g2.hpp>
#include <ql/time/calendars/nullcalendar.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"FdmAffineModelTermStructure::discount",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 indexes::CurveSpec curve = indexes::curve(a[0]);
                 std::string model = text(a[1]);
                 std::vector<Real> p = reals(a[2]), state = reals(a[3]);
                 DateSpec reference = date(a[4]);
                 std::vector<Real> times = reals(a[5]);
                 std::vector<Integer> dates = integers(a[6]);
                 if (model == "HullWhite" ? p.size() != 2 || state.size() != 1
                                          : model != "G2" || p.size() != 5 || state.size() != 2)
                     throw InvalidParams("expected HullWhite [a, sigma] at [r], or G2 [a, sigma, b, eta, rho] at [x, y]");
                 return [=] {
                     Handle<YieldTermStructure> ts = indexes::makeCurve(curve);
                     ext::shared_ptr<AffineModel> m;
                     if (model == "HullWhite")
                         m = ext::make_shared<HullWhite>(ts, p[0], p[1]);
                     else
                         m = ext::make_shared<G2>(ts, p[0], p[1], p[2], p[3], p[4]);
                     Array r(state.begin(), state.end());
                     FdmAffineModelTermStructure s(r, NullCalendar(), ts->dayCounter(), makeDate(reference),
                                                   ts->referenceDate(), m);
                     json::array discount, discountAt;
                     for (Real t : times)
                         discount.push_back(json::value(s.discount(t)));
                     for (Integer d : dates)
                         discountAt.push_back(json::value(s.discount(Date(d))));
                     json::object out;
                     out["discount"] = discount;
                     out["discountAt"] = discountAt;
                     return json::value(out);
                 };
             }},
        });

    }

}
