// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "models/equity/hestonspec.hpp"

#include <ql/handle.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>

#include <initializer_list>
#include <string>

using namespace QuantLib;

namespace harness::equity {

    namespace {

        const json::object& object(const json::value& v, const std::string& what) {
            if (!v.is_object())
                throw InvalidParams("expected " + what + " spec, got " + json::serialize(v));
            return v.get_object();
        }

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " spec needs " + name);
            return *v;
        }

        void onlyKeys(const json::object& o, std::initializer_list<const char*> keys, const std::string& what) {
            for (const auto& kv : o) {
                bool known = false;
                for (const char* k : keys)
                    known = known || kv.key() == k;
                if (!known)
                    throw InvalidParams(what + " takes no key " + std::string(kv.key()));
            }
        }

        Handle<YieldTermStructure> flat(const ProcessSpec& s, Real rate) {
            return Handle<YieldTermStructure>(
                ext::make_shared<FlatForward>(makeDate(s.referenceDate), rate, makeDayCounter(s.dayCounter)));
        }

    }

    ProcessSpec process(const json::value& v) {
        const std::string what = "a process";
        const json::object& o = object(v, what);
        ProcessSpec s;
        s.jumps = o.contains("lambda");
        if (s.jumps)
            onlyKeys(o, {"referenceDate", "dayCounter", "riskFree", "dividend", "s0", "v0", "kappa", "theta", "sigma",
                         "rho", "lambda", "nu", "delta"}, what);
        else
            onlyKeys(o, {"referenceDate", "dayCounter", "riskFree", "dividend", "s0", "v0", "kappa", "theta", "sigma",
                         "rho"}, what);
        s.referenceDate = date(key(o, "referenceDate", what));
        s.dayCounter = dayCounter(key(o, "dayCounter", what));
        s.riskFree = real(key(o, "riskFree", what));
        s.dividend = real(key(o, "dividend", what));
        s.s0 = real(key(o, "s0", what));
        s.v0 = real(key(o, "v0", what));
        s.kappa = real(key(o, "kappa", what));
        s.theta = real(key(o, "theta", what));
        s.sigma = real(key(o, "sigma", what));
        s.rho = real(key(o, "rho", what));
        if (s.jumps) {
            s.lambda = real(key(o, "lambda", what));
            s.nu = real(key(o, "nu", what));
            s.delta = real(key(o, "delta", what));
        }
        return s;
    }

    ext::shared_ptr<HestonProcess> makeProcess(const ProcessSpec& s) {
        if (s.jumps)
            return makeBatesProcess(s);
        return ext::make_shared<HestonProcess>(flat(s, s.riskFree), flat(s, s.dividend),
                                               Handle<Quote>(ext::make_shared<SimpleQuote>(s.s0)), s.v0, s.kappa,
                                               s.theta, s.sigma, s.rho);
    }

    ext::shared_ptr<BatesProcess> makeBatesProcess(const ProcessSpec& s) {
        if (!s.jumps)
            throw InvalidParams("a Bates process needs lambda, nu and delta");
        return ext::make_shared<BatesProcess>(flat(s, s.riskFree), flat(s, s.dividend),
                                              Handle<Quote>(ext::make_shared<SimpleQuote>(s.s0)), s.v0, s.kappa,
                                              s.theta, s.sigma, s.rho, s.lambda, s.nu, s.delta);
    }

    std::optional<QuantoSpec> quanto(const json::value& v) {
        if (v.is_null())
            return std::nullopt;
        const std::string what = "a quanto helper";
        const json::object& o = object(v, what);
        onlyKeys(o, {"foreign", "fxVolatility", "correlation", "atmLevel"}, what);
        QuantoSpec q;
        q.foreign = real(key(o, "foreign", what));
        q.fxVolatility = real(key(o, "fxVolatility", what));
        q.correlation = real(key(o, "correlation", what));
        q.atmLevel = real(key(o, "atmLevel", what));
        return q;
    }

    ext::shared_ptr<FdmQuantoHelper> makeQuanto(const std::optional<QuantoSpec>& q, const ProcessSpec& s) {
        if (!q)
            return {};
        Date d = makeDate(s.referenceDate);
        DayCounter c = makeDayCounter(s.dayCounter);
        return ext::make_shared<FdmQuantoHelper>(
            ext::make_shared<FlatForward>(d, s.riskFree, c), ext::make_shared<FlatForward>(d, q->foreign, c),
            ext::make_shared<BlackConstantVol>(d, NullCalendar(), q->fxVolatility, c), q->correlation, q->atmLevel);
    }

    json::value vanillaResults(Real npv, Real delta, Real gamma, Real theta) {
        json::object out;
        out["NPV"] = npv;
        out["delta"] = delta;
        out["gamma"] = gamma;
        out["theta"] = theta;
        return json::value(out);
    }

}
