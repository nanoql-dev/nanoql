// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// MSVC's C4714, a __forceinline function left out of line, which Boost's gamma code raises at /O2: a note on
// inlining, not on the code, so it is off here and /WX still stops on every other warning.
#pragma warning(disable : 4714)

#include "experimental/credit/portfoliospec.hpp"
#include "termstructures/credit/creditspec.hpp"

#include <ql/currencies/america.hpp>
#include <ql/currencies/europe.hpp>
#include <ql/experimental/credit/constantlosslatentmodel.hpp>
#include <ql/experimental/math/gaussiancopulapolicy.hpp>
#include <ql/experimental/math/tcopulapolicy.hpp>
#include <ql/quotes/simplequote.hpp>

#include <map>

using namespace QuantLib;

namespace harness::credit::portfolio {

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

    Period period(const json::value& v) {
        if (!v.is_array() || v.get_array().size() != 2)
            throw InvalidParams("expected a period as [length, units], got " + json::serialize(v));
        return Period(integer(v.get_array()[0]), enumeration<TimeUnit>(v.get_array()[1]));
    }

    Currency makeCurrency(const json::value& v) {
        if (v.is_null())
            return Currency();
        std::string code = text(v);
        if (code == "EUR")
            return EURCurrency();
        if (code == "USD")
            return USDCurrency();
        if (code == "GBP")
            return GBPCurrency();
        throw InvalidParams("unknown currency " + code);
    }

    ext::shared_ptr<DefaultType> makeType(const json::value& v) {
        const json::object& o = object(v, "a default type");
        if (const json::value* g = o.if_contains("failureToPay"); g != nullptr)
            return ext::make_shared<FailureToPay>(period(*g), real(key(o, "amount", "a failure to pay")));
        return ext::make_shared<DefaultType>(enumeration<AtomicDefault::Type>(key(o, "atomic", "a default type")),
                                             enumeration<Restructuring::Type>(key(o, "restructuring", "a default type")));
    }

    DefaultProbKey makeKey(const json::value& v) {
        const std::string what = "a default key";
        const json::object& o = object(v, what);
        if (o.empty())
            return DefaultProbKey();
        Currency c = makeCurrency(key(o, "currency", what));
        auto sen = enumeration<Seniority>(key(o, "seniority", what));
        if (o.if_contains("northAmericaCorp") != nullptr) {
            Period grace(30, Days);
            Real amount = 1.e6;
            Restructuring::Type r = Restructuring::CR;
            if (const json::value* g = o.if_contains("grace"); g != nullptr)
                grace = period(*g);
            if (const json::value* a = o.if_contains("amount"); a != nullptr)
                amount = real(*a);
            if (const json::value* x = o.if_contains("restructuring"); x != nullptr)
                r = enumeration<Restructuring::Type>(*x);
            return NorthAmericaCorpDefaultKey(c, sen, grace, amount, r);
        }
        std::vector<ext::shared_ptr<DefaultType>> types;
        const json::value& ts = key(o, "eventTypes", what);
        if (!ts.is_array())
            throw InvalidParams("expected the key's event types as an array");
        for (const json::value& t : ts.get_array())
            types.push_back(makeType(t));
        return DefaultProbKey(types, c, sen);
    }

    ext::shared_ptr<DefaultEvent> makeEvent(const json::value& v) {
        const std::string what = "a default event";
        const json::object& o = object(v, what);
        std::string kind = text(key(o, "kind", what));
        Date d = makeDate(date(key(o, "date", what)));
        Currency c = makeCurrency(key(o, "currency", what));
        auto sen = enumeration<Seniority>(key(o, "seniority", what));
        Date settle = makeDate(date(key(o, "settlementDate", what)));
        const json::value* single = o.if_contains("recovery");
        std::map<Seniority, Real> rates;
        if (const json::value* m = o.if_contains("recoveries"); m != nullptr) {
            if (!m->is_array())
                throw InvalidParams("expected the recoveries as [[seniority, rate]...]");
            for (const json::value& p : m->get_array()) {
                if (!p.is_array() || p.get_array().size() != 2)
                    throw InvalidParams("expected a recovery as [seniority, rate]");
                rates[enumeration<Seniority>(p.get_array()[0])] = real(p.get_array()[1]);
            }
        }
        if (kind == "DefaultEvent") {
            ext::shared_ptr<DefaultType> t = makeType(key(o, "type", what));
            if (single != nullptr)
                return ext::make_shared<DefaultEvent>(d, *t, c, sen, settle, real(*single));
            return ext::make_shared<DefaultEvent>(d, *t, c, sen, settle, rates);
        }
        if (kind == "FailureToPayEvent") {
            Real amount = real(key(o, "amount", what));
            if (single != nullptr)
                return ext::make_shared<FailureToPayEvent>(d, c, sen, amount, settle, real(*single));
            return ext::make_shared<FailureToPayEvent>(d, c, sen, amount, settle, rates);
        }
        if (kind == "BankruptcyEvent") {
            if (single != nullptr)
                return ext::make_shared<BankruptcyEvent>(d, c, sen, settle, real(*single));
            return ext::make_shared<BankruptcyEvent>(d, c, sen, settle, rates);
        }
        throw InvalidParams("unknown default event " + kind);
    }

    Issuer makeIssuer(const json::value& v) {
        const std::string what = "an issuer";
        const json::object& o = object(v, what);
        std::vector<Issuer::key_curve_pair> curves;
        if (const json::value* cs = o.if_contains("curves"); cs != nullptr) {
            if (!cs->is_array())
                throw InvalidParams("expected the issuer's curves as an array");
            for (const json::value& kc : cs->get_array()) {
                const json::object& p = object(kc, "a key and curve");
                curves.emplace_back(makeKey(key(p, "key", "a key and curve")),
                                    Handle<DefaultProbabilityTermStructure>(
                                        credit::makeCurve(credit::curve(key(p, "curve", "a key and curve")))));
            }
        }
        DefaultEventSet events;
        if (const json::value* es = o.if_contains("events"); es != nullptr) {
            if (!es->is_array())
                throw InvalidParams("expected the issuer's events as an array");
            for (const json::value& e : es->get_array())
                events.insert(makeEvent(e));
        }
        return Issuer(curves, events);
    }

    ext::shared_ptr<Pool> makePool(const json::value& v) {
        if (!v.is_array())
            throw InvalidParams("expected a pool as an array of names");
        auto pool = ext::make_shared<Pool>();
        for (const json::value& n : v.get_array()) {
            const json::object& o = object(n, "a pool name");
            std::string name = text(key(o, "name", "a pool name"));
            Issuer issuer = makeIssuer(key(o, "issuer", "a pool name"));
            const json::value& k = key(o, "key", "a pool name");
            if (k.is_null())
                pool->add(name, issuer);
            else
                pool->add(name, issuer, makeKey(k));
        }
        return pool;
    }

    ext::shared_ptr<Basket> makeBasket(const json::value& v) {
        const std::string what = "a basket";
        const json::object& o = object(v, what);
        ext::shared_ptr<Pool> pool = makePool(key(o, "pool", what));
        return ext::make_shared<Basket>(makeDate(date(key(o, "refDate", what))), pool->names(),
                                        reals(key(o, "notionals", what)), pool, real(key(o, "attachment", what)),
                                        real(key(o, "detachment", what)));
    }

    namespace {

        std::vector<std::vector<Real>> matrix(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected a matrix as an array of rows");
            std::vector<std::vector<Real>> out;
            for (const json::value& r : v.get_array())
                out.push_back(reals(r));
            return out;
        }

        // QuantLib's DefaultLossModel with every default kept: each statistic refuses.
        class BaseLossModel : public DefaultLossModel {
            void resetModel() override {}
        };

        template <class CP>
        ext::shared_ptr<DefaultLossModel> constantLoss(const json::object& o, const typename CP::initTraits& ini) {
            const std::string what = "a constant-loss model";
            std::vector<Real> recoveries = reals(key(o, "recoveries", what));
            auto type = enumeration<LatentModelIntegrationType::LatentModelIntegrationType>(key(o, "integration", what));
            if (const json::value* c = o.if_contains("correlation"); c != nullptr) {
                auto n = static_cast<Size>(integer(key(o, "nVariables", what)));
                return ext::make_shared<ConstantLossModel<CP>>(
                    Handle<Quote>(ext::make_shared<SimpleQuote>(real(*c))), recoveries, type, n, ini);
            }
            return ext::make_shared<ConstantLossModel<CP>>(matrix(key(o, "factorWeights", what)), recoveries, type, ini);
        }

    }

    ext::shared_ptr<DefaultLossModel> makeLossModel(const json::value& v) {
        const std::string what = "a loss model";
        const json::object& o = object(v, what);
        std::string kind = text(key(o, "kind", what));
        if (kind == "DefaultLossModel")
            return ext::make_shared<BaseLossModel>();
        std::string copula = text(key(o, "copula", what));
        if (kind != "ConstantLossModel")
            throw InvalidParams("unknown loss model " + kind);
        if (copula == "Gaussian")
            return constantLoss<GaussianCopulaPolicy>(o, GaussianCopulaPolicy::initTraits());
        if (copula == "T") {
            TCopulaPolicy::initTraits ini;
            ini.tOrders = integers(key(o, "tOrders", what));
            return constantLoss<TCopulaPolicy>(o, ini);
        }
        throw InvalidParams("unknown copula " + copula);
    }

    ext::shared_ptr<Basket> makeBoundBasket(const json::value& basket, const json::value& model) {
        ext::shared_ptr<Basket> b = makeBasket(basket);
        b->setLossModel(makeLossModel(model));
        return b;
    }

    json::value currencyCode(const Currency& c) {
        return c.empty() ? json::value(nullptr) : json::value(c.code());
    }

    json::value typeSummary(const DefaultType& t) {
        json::array out;
        out.emplace_back(static_cast<std::int64_t>(t.defaultType()));
        out.emplace_back(static_cast<std::int64_t>(t.restructuringType()));
        return json::value(std::move(out));
    }

    json::value keySummary(const DefaultProbKey& k) {
        json::array types;
        for (const auto& t : k.eventTypes())
            types.push_back(typeSummary(*t));
        json::array out;
        out.emplace_back(static_cast<std::int64_t>(k.seniority()));
        out.push_back(currencyCode(k.currency()));
        out.emplace_back(std::move(types));
        return json::value(std::move(out));
    }

    json::value array(const std::vector<Real>& x) {
        json::array out;
        for (Real r : x)
            out.emplace_back(r);
        return json::value(std::move(out));
    }

}
