// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/zabr.hpp, .cpp: ZabrModel, which no wheel class exposes, built inside each call from a
// spec {"expiryTime", "forward", "alpha", "beta", "nu", "rho", "gamma"}, QuantLib's constructor's arguments.
//
//     ZabrModel::ZabrModel                        (model)            its inspectors, or QuantLib's refusal
//     ZabrModel::lognormalVolatility(Real)        (model, strike)    a real
//     ZabrModel::lognormalVolatility(vector)      (model, strikes)   a list of reals
//     ZabrModel::normalVolatility(Real)           (model, strike)
//     ZabrModel::normalVolatility(vector)         (model, strikes)
//     ZabrModel::localVolatility(Real)            (model, f)
//     ZabrModel::localVolatility(vector)          (model, f)
//     ZabrModel::fdPrice(Real)                    (model, strike)    a real: a call by the Dupire equation
//     ZabrModel::fdPrice(vector)                  (model, strikes)   a list of reals
//
// The vector forms refuse an empty list as the harness's own failure: QuantLib reads its first element. The inspectors
// are {"forward", "expiryTime", "alpha", "beta", "nu", "rho", "gamma"}, nu the model's transformed one.

#include "convert.hpp"

#include <ql/termstructures/volatility/zabr.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct ModelSpec {
            Real expiryTime, forward, alpha, beta, nu, rho, gamma;
        };

        const json::value& key(const json::object& o, const char* name) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(std::string("a ZABR model spec needs ") + name);
            return *v;
        }

        ModelSpec modelSpec(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a ZABR model spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            for (const auto& kv : o) {
                const std::string k(kv.key());
                if (k != "expiryTime" && k != "forward" && k != "alpha" && k != "beta" && k != "nu" && k != "rho" &&
                    k != "gamma")
                    throw InvalidParams("a ZABR model spec takes no key " + k);
            }
            return {real(key(o, "expiryTime")), real(key(o, "forward")), real(key(o, "alpha")), real(key(o, "beta")),
                    real(key(o, "nu")),         real(key(o, "rho")),     real(key(o, "gamma"))};
        }

        ZabrModel makeModel(const ModelSpec& s) {
            return ZabrModel(s.expiryTime, s.forward, s.alpha, s.beta, s.nu, s.rho, s.gamma);
        }

        json::value realArray(const std::vector<Real>& v) {
            json::array out;
            for (Real x : v)
                out.emplace_back(x);
            return json::value(std::move(out));
        }

        // A scalar member: (model, x).
        Call scalar(const Args& a, Real (ZabrModel::*f)(Real) const) {
            arity(a, 2);
            ModelSpec s = modelSpec(a[0]);
            Real x = real(a[1]);
            return [=] { return json::value((makeModel(s).*f)(x)); };
        }

        // A vector member: (model, xs), xs not empty.
        Call vector(const Args& a, std::vector<Real> (ZabrModel::*f)(const std::vector<Real>&) const) {
            arity(a, 2);
            ModelSpec s = modelSpec(a[0]);
            std::vector<Real> x = reals(a[1]);
            if (x.empty())
                throw InvalidParams("ZabrModel's vector members read the first element of an empty vector");
            return [=] { return realArray((makeModel(s).*f)(x)); };
        }

        const Register unit({
            {"ZabrModel::ZabrModel",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 ModelSpec s = modelSpec(a[0]);
                 return [=] {
                     ZabrModel m = makeModel(s);
                     return json::value(json::object{{"forward", m.forward()},
                                                     {"expiryTime", m.expiryTime()},
                                                     {"alpha", m.alpha()},
                                                     {"beta", m.beta()},
                                                     {"nu", m.nu()},
                                                     {"rho", m.rho()},
                                                     {"gamma", m.gamma()}});
                 };
             }},
            {"ZabrModel::lognormalVolatility(Real)",
             [](const Args& a) { return scalar(a, &ZabrModel::lognormalVolatility); }},
            {"ZabrModel::lognormalVolatility(vector)",
             [](const Args& a) { return vector(a, &ZabrModel::lognormalVolatility); }},
            {"ZabrModel::normalVolatility(Real)", [](const Args& a) { return scalar(a, &ZabrModel::normalVolatility); }},
            {"ZabrModel::normalVolatility(vector)",
             [](const Args& a) { return vector(a, &ZabrModel::normalVolatility); }},
            {"ZabrModel::localVolatility(Real)", [](const Args& a) { return scalar(a, &ZabrModel::localVolatility); }},
            {"ZabrModel::localVolatility(vector)", [](const Args& a) { return vector(a, &ZabrModel::localVolatility); }},
            {"ZabrModel::fdPrice(Real)", [](const Args& a) { return scalar(a, &ZabrModel::fdPrice); }},
            {"ZabrModel::fdPrice(vector)", [](const Args& a) { return vector(a, &ZabrModel::fdPrice); }},
        });

    }

}
