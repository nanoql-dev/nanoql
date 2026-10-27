// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/globalbootstrapvars.hpp, .cpp, and the global bootstrap's additional variables
// (ql/termstructures/globalbootstrap.hpp), which the wheel doesn't expose.
//
// "SimpleQuoteVariables" takes (count, initial guesses, lower bounds, x): the guesses and bounds as QuantLib takes them,
// a null bound Null<Real>(); it builds count SimpleQuotes and the variables on them, and returns {"initialize", the
// guesses initialize(false) returns; "values", the quotes' values after update(x)}.
//
// "GlobalBootstrap::variables" builds QuantLib's testGlobalBootstrapVariables' curve (test-suite/piecewiseyieldcurve.cpp:
// 1491-1548) from specs: (curve, helpers, additional helpers, futures, variable, penalty weight, accuracy), the curve
// {"traits": "Discount", "interpolator": "LogLinear", "referenceDate", "dayCounter"}, the helpers and the additional
// helpers specs of termstructures/yield/ratehelperspec.hpp, the futures {"index", "dates", "prices", "meanReversion"}
// (an IborIndex spec of indexes/indexspec.hpp, each futures' IMM date and price), the variable {"guess", "lowerBound"}
// (the volatility's initial guess and lower bound, null for none). The futures helpers follow the helpers, each with
// a FuturesConvAdjustmentQuote on the volatility's quote as its convexity adjustment, not registered as an observer;
// the penalties are the weight times each additional helper's quote error. It returns {"dates", "data", "variable"
// (the volatility at the optimum), "quoteErrors" (the helpers', then the futures'), "additionalQuoteErrors"}. A
// refusal of QuantLib's is the call's.

#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "termstructures/yield/ratehelperspec.hpp"

#include <ql/math/interpolations/loginterpolation.hpp>
#include <ql/quotes/futuresconvadjustmentquote.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/globalbootstrap.hpp>
#include <ql/termstructures/globalbootstrapvars.hpp>
#include <ql/termstructures/yield/bootstraptraits.hpp>
#include <ql/termstructures/yield/piecewiseyieldcurve.hpp>
#include <ql/termstructures/yield/ratehelpers.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        using termstructures::RateHelperSpec;

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " spec needs " + name);
            return *v;
        }

        std::vector<RateHelperSpec> helpers(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected an array of rate helpers, got " + json::serialize(v));
            std::vector<RateHelperSpec> out;
            for (const json::value& h : v.get_array())
                out.push_back(termstructures::rateHelper(h));
            return out;
        }

        json::array quoteErrors(const std::vector<ext::shared_ptr<RateHelper>>& hs) {
            json::array out;
            for (const auto& h : hs)
                out.push_back(json::value(h->quoteError()));
            return out;
        }

        const Register unit({
            {"SimpleQuoteVariables",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Integer count = integer(a[0]);
                 if (count == Null<Integer>() || count < 0)
                     throw InvalidParams("a count is a Size");
                 std::vector<Real> guesses = reals(a[1]), bounds = reals(a[2]), x = reals(a[3]);
                 return [=] {
                     std::vector<ext::shared_ptr<SimpleQuote>> quotes;
                     for (Integer i = 0; i < count; ++i)
                         quotes.push_back(ext::make_shared<SimpleQuote>());
                     SimpleQuoteVariables variables(quotes, guesses, bounds);
                     Array initial = variables.initialize(false);
                     variables.update(Array(x.begin(), x.end()));
                     json::array init, values;
                     for (Real g : initial)
                         init.push_back(json::value(g));
                     for (Size i = 0; i < x.size(); ++i)
                         values.push_back(json::value(quotes[i]->value()));
                     json::object out;
                     out["initialize"] = init;
                     out["values"] = values;
                     return json::value(out);
                 };
             }},
            {"GlobalBootstrap::variables",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 if (!a[0].is_object())
                     throw InvalidParams("expected a curve spec, got " + json::serialize(a[0]));
                 const json::object& c = a[0].get_object();
                 if (text(key(c, "traits", "a curve")) != "Discount" ||
                     text(key(c, "interpolator", "a curve")) != "LogLinear")
                     throw InvalidParams("the variables' curve is on Discount and LogLinear");
                 DateSpec referenceDate = date(key(c, "referenceDate", "a curve"));
                 DayCounterSpec dc = dayCounter(key(c, "dayCounter", "a curve"));
                 std::vector<RateHelperSpec> h = helpers(a[1]), additional = helpers(a[2]);
                 if (!a[3].is_object())
                     throw InvalidParams("expected a futures spec, got " + json::serialize(a[3]));
                 const json::object& f = a[3].get_object();
                 indexes::IndexSpec index = indexes::iborIndex(key(f, "index", "a futures"));
                 std::vector<Integer> dates = integers(key(f, "dates", "a futures"));
                 std::vector<Real> prices = reals(key(f, "prices", "a futures"));
                 Real meanReversion = real(key(f, "meanReversion", "a futures"));
                 if (dates.size() != prices.size())
                     throw InvalidParams("a futures spec has a price per date");
                 if (!a[4].is_object())
                     throw InvalidParams("expected a variable spec, got " + json::serialize(a[4]));
                 const json::object& vs = a[4].get_object();
                 Real guess = real(key(vs, "guess", "a variable")), lowerBound = real(key(vs, "lowerBound", "a variable"));
                 Real weight = real(a[5]), accuracy = real(a[6]);
                 return [=] {
                     typedef PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap> Curve;
                     std::vector<ext::shared_ptr<RateHelper>> instruments, additionalHelpers;
                     for (const RateHelperSpec& s : h)
                         instruments.push_back(termstructures::makeRateHelper(s));
                     for (const RateHelperSpec& s : additional)
                         additionalHelpers.push_back(termstructures::makeRateHelper(s));
                     auto ibor = indexes::makeIborIndex(index);
                     auto vol = ext::make_shared<SimpleQuote>();
                     Handle<Quote> mr(ext::make_shared<SimpleQuote>(meanReversion));
                     for (Size i = 0; i < dates.size(); ++i) {
                         Handle<Quote> r(ext::make_shared<SimpleQuote>(prices[i]));
                         auto convAdj = ext::make_shared<FuturesConvAdjustmentQuote>(ibor, Date(dates[i]), r,
                                                                                     Handle<Quote>(vol), mr);
                         bool registerAsObserver = false;
                         instruments.push_back(ext::make_shared<FuturesRateHelper>(
                             r, Date(dates[i]), ibor, Handle<Quote>(convAdj, registerAsObserver), Futures::IMM));
                     }
                     auto penalties = [additionalHelpers, weight]() {
                         Array errors(additionalHelpers.size());
                         for (Size i = 0; i < additionalHelpers.size(); ++i)
                             errors[i] = weight * additionalHelpers[i]->quoteError();
                         return errors;
                     };
                     std::vector<Real> bounds;
                     if (lowerBound != Null<Real>())
                         bounds.push_back(lowerBound);
                     auto curve = ext::make_shared<Curve>(
                         makeDate(referenceDate), instruments, makeDayCounter(dc), LogLinear(),
                         Curve::bootstrap_type(additionalHelpers, nullptr, penalties, accuracy, nullptr, nullptr,
                                               ext::make_shared<SimpleQuoteVariables>(
                                                   std::vector<ext::shared_ptr<SimpleQuote>>{vol},
                                                   std::vector<Real>{guess}, bounds)));
                     json::array nodeDates, nodeData;
                     for (const auto& n : curve->nodes()) {
                         nodeDates.push_back(result(n.first));
                         nodeData.push_back(json::value(n.second));
                     }
                     json::object out;
                     out["dates"] = nodeDates;
                     out["data"] = nodeData;
                     out["variable"] = vol->value();
                     out["quoteErrors"] = quoteErrors(instruments);
                     out["additionalQuoteErrors"] = quoteErrors(additionalHelpers);
                     return json::value(out);
                 };
             }},
        });

    }

}
