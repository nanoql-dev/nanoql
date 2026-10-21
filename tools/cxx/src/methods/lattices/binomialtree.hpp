// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The binomial trees' spec, shared by binomialtree.cpp and bsmlattice.cpp: {"type": "JarrowRudd" | "CoxRossRubinstein"
// | "AdditiveEQPBinomialTree" | "Trigeorgis" | "Tian" | "LeisenReimer" | "Joshi4", "referenceDate", "spot", "r", "q",
// "volatility", "end", "steps", "strike"}, the tree built on a Black-Scholes-Merton process of flat forwards and a
// constant volatility from the reference date on Actual/365 (Fixed). withTree calls f with the built tree as its
// shared pointer, so that a constructor's refusal is QuantLib's.

#ifndef nanoql_harness_binomialtree_hpp
#define nanoql_harness_binomialtree_hpp

#include "convert.hpp"

#include <ql/methods/lattices/binomialtree.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <climits>
#include <string>

namespace harness {

    struct BinomialTreeSpec {
        std::string type;
        DateSpec referenceDate;
        QuantLib::Real spot, r, q, volatility, end, strike;
        QuantLib::Size steps;
    };

    inline BinomialTreeSpec binomialTreeSpec(const json::value& v) {
        if (!v.is_object())
            throw InvalidParams("expected a binomial tree spec, got " + json::serialize(v));
        const json::object& o = v.get_object();
        const char* keys[] = {"type", "referenceDate", "spot", "r", "q", "volatility", "end", "steps", "strike"};
        for (const auto& kv : o) {
            bool known = false;
            for (const char* k : keys)
                known = known || kv.key() == k;
            if (!known)
                throw InvalidParams("a binomial tree takes no key " + std::string(kv.key()));
        }
        for (const char* k : keys) {
            if (o.if_contains(k) == nullptr)
                throw InvalidParams(std::string("a binomial tree needs ") + k);
        }
        const json::value& steps = o.at("steps");
        if (!steps.is_int64() || steps.get_int64() < 0 || steps.get_int64() > INT_MAX)
            throw InvalidParams("expected a step count, got " + json::serialize(steps));
        return {text(o.at("type")), date(o.at("referenceDate")), real(o.at("spot")), real(o.at("r")), real(o.at("q")),
                real(o.at("volatility")), real(o.at("end")), real(o.at("strike")),
                static_cast<QuantLib::Size>(steps.get_int64())};
    }

    inline QuantLib::ext::shared_ptr<QuantLib::StochasticProcess1D> binomialProcess(const BinomialTreeSpec& s) {
        using namespace QuantLib;
        Date reference = makeDate(s.referenceDate);
        DayCounter dc = Actual365Fixed();
        return ext::make_shared<BlackScholesMertonProcess>(
            Handle<Quote>(ext::make_shared<SimpleQuote>(s.spot)),
            Handle<YieldTermStructure>(ext::make_shared<FlatForward>(reference, s.q, dc)),
            Handle<YieldTermStructure>(ext::make_shared<FlatForward>(reference, s.r, dc)),
            Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(reference, NullCalendar(), s.volatility, dc)));
    }

    template <class F>
    json::value withTree(const BinomialTreeSpec& s, F f) {
        using namespace QuantLib;
        auto p = binomialProcess(s);
        if (s.type == "JarrowRudd")
            return f(ext::make_shared<JarrowRudd>(p, s.end, s.steps, s.strike));
        if (s.type == "CoxRossRubinstein")
            return f(ext::make_shared<CoxRossRubinstein>(p, s.end, s.steps, s.strike));
        if (s.type == "AdditiveEQPBinomialTree")
            return f(ext::make_shared<AdditiveEQPBinomialTree>(p, s.end, s.steps, s.strike));
        if (s.type == "Trigeorgis")
            return f(ext::make_shared<Trigeorgis>(p, s.end, s.steps, s.strike));
        if (s.type == "Tian")
            return f(ext::make_shared<Tian>(p, s.end, s.steps, s.strike));
        if (s.type == "LeisenReimer")
            return f(ext::make_shared<LeisenReimer>(p, s.end, s.steps, s.strike));
        if (s.type == "Joshi4")
            return f(ext::make_shared<Joshi4>(p, s.end, s.steps, s.strike));
        throw InvalidParams("unknown binomial tree " + s.type);
    }

    inline QuantLib::Size latticeIndex(const json::value& v) {
        if (!v.is_int64() || v.get_int64() < 0 || v.get_int64() > INT_MAX)
            throw InvalidParams("expected an index, got " + json::serialize(v));
        return static_cast<QuantLib::Size>(v.get_int64());
    }

}

#endif
