// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/lattices/extendedbinomialtree.hpp, .cpp: the time-dependent binomial trees, which the wheel doesn't
// bind. "ExtendedBinomialTree::nodes" takes (type, referenceDate, spot, r, q, vols, end, steps, strike, columns): the
// tree's type, one of the seven Extended trees by its class name, on a Black-Scholes-Merton process of flat forwards
// from the reference date on Actual/365 (Fixed) and a volatility that is constant for one value in vols and, for three,
// a BlackVarianceCurve at one, two and five years; it returns {"columns", "underlying", "probabilities"}: the tree's column
// count, each of the first columns' node values in turn, and each of those columns' up probability at node 0.

#include "convert.hpp"

#include <ql/experimental/lattices/extendedbinomialtree.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/volatility/equityfx/blackvariancecurve.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <cstdint>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        ext::shared_ptr<StochasticProcess1D> process(const Date& reference, Real spot, Real r, Real q,
                                                     const std::vector<Real>& vols) {
            DayCounter dc = Actual365Fixed();
            ext::shared_ptr<BlackVolTermStructure> vol;
            if (vols.size() == 1)
                vol = ext::make_shared<BlackConstantVol>(reference, NullCalendar(), vols[0], dc);
            else if (vols.size() == 3)
                vol = ext::make_shared<BlackVarianceCurve>(
                    reference, std::vector<Date>{reference + 365, reference + 730, reference + 1825}, vols, dc);
            else
                throw InvalidParams("a volatility is one value, or three for one, two and five years");
            return ext::make_shared<BlackScholesMertonProcess>(
                Handle<Quote>(ext::make_shared<SimpleQuote>(spot)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(reference, q, dc)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(reference, r, dc)),
                Handle<BlackVolTermStructure>(vol));
        }

        template <class T>
        json::value describe(const ext::shared_ptr<T>& tree, Size columns) {
            json::object out;
            out["columns"] = static_cast<std::int64_t>(tree->columns());
            json::array underlying, probabilities;
            for (Size i = 0; i < columns && i + 1 < tree->columns(); ++i) {
                for (Size j = 0; j < tree->size(i); ++j)
                    underlying.emplace_back(tree->underlying(i, j));
                probabilities.emplace_back(tree->probability(i, 0, 1));
            }
            out["underlying"] = underlying;
            out["probabilities"] = probabilities;
            return json::value(out);
        }

        const Register unit({
            // (type, referenceDate, spot, r, q, vols, end, steps, strike, columns)
            {"ExtendedBinomialTree::nodes",
             [](const Args& a) -> Call {
                 arity(a, 10);
                 std::string type = text(a[0]);
                 DateSpec reference = date(a[1]);
                 Real spot = real(a[2]), r = real(a[3]), q = real(a[4]);
                 std::vector<Real> vols = reals(a[5]);
                 Real end = real(a[6]);
                 Size steps = static_cast<Size>(integer(a[7]));
                 Real strike = real(a[8]);
                 Size columns = static_cast<Size>(integer(a[9]));
                 return [=]() -> json::value {
                     auto p = process(makeDate(reference), spot, r, q, vols);
                     if (type == "ExtendedJarrowRudd")
                         return describe(ext::make_shared<ExtendedJarrowRudd>(p, end, steps, strike), columns);
                     if (type == "ExtendedCoxRossRubinstein")
                         return describe(ext::make_shared<ExtendedCoxRossRubinstein>(p, end, steps, strike), columns);
                     if (type == "ExtendedAdditiveEQPBinomialTree")
                         return describe(ext::make_shared<ExtendedAdditiveEQPBinomialTree>(p, end, steps, strike), columns);
                     if (type == "ExtendedTrigeorgis")
                         return describe(ext::make_shared<ExtendedTrigeorgis>(p, end, steps, strike), columns);
                     if (type == "ExtendedTian")
                         return describe(ext::make_shared<ExtendedTian>(p, end, steps, strike), columns);
                     if (type == "ExtendedLeisenReimer")
                         return describe(ext::make_shared<ExtendedLeisenReimer>(p, end, steps, strike), columns);
                     if (type == "ExtendedJoshi4")
                         return describe(ext::make_shared<ExtendedJoshi4>(p, end, steps, strike), columns);
                     throw InvalidParams("unknown extended binomial tree " + type);
                 };
             }},
        });

    }

}
