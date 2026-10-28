// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/credit/recoveryratequote.hpp, .cpp and recoveryratemodel.hpp, .cpp, which the wheel doesn't expose:
//
//     RecoveryRateQuote::conventionalRecovery (seniority)
//     RecoveryRateQuote::makeIsdaConvMap      (): [[seniority, rate]...]
//     RecoveryRateQuote::inspect              (value, seniority): [value, seniority]
//     ConstantRecoveryModel::recoveryValue    (recovery, seniority, date, key): from the (Real, Seniority) constructor
//     ConstantRecoveryModel::appliesToSeniority (recovery, seniority, asked)

#include "experimental/credit/portfoliospec.hpp"

#include <ql/experimental/credit/recoveryratemodel.hpp>
#include <ql/experimental/credit/recoveryratequote.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"RecoveryRateQuote::conventionalRecovery",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 auto s = enumeration<Seniority>(a[0]);
                 return [=] { return json::value(RecoveryRateQuote::conventionalRecovery(s)); };
             }},
            {"RecoveryRateQuote::makeIsdaConvMap",
             [](const Args& a) -> Call {
                 arity(a, 0);
                 return [] {
                     json::array out;
                     for (const auto& kv : makeIsdaConvMap()) {
                         json::array p;
                         p.emplace_back(static_cast<std::int64_t>(kv.first));
                         p.emplace_back(kv.second);
                         out.emplace_back(std::move(p));
                     }
                     return json::value(std::move(out));
                 };
             }},
            {"RecoveryRateQuote::inspect",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 Real v = real(a[0]);
                 auto s = enumeration<Seniority>(a[1]);
                 return [=] {
                     RecoveryRateQuote q(v, s);
                     json::array out;
                     out.emplace_back(q.value());
                     out.emplace_back(static_cast<std::int64_t>(q.seniority()));
                     return json::value(std::move(out));
                 };
             }},
            {"ConstantRecoveryModel::recoveryValue",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Real v = real(a[0]);
                 auto s = enumeration<Seniority>(a[1]);
                 DateSpec d = date(a[2]);
                 json::value k = a[3];
                 return [=] {
                     ConstantRecoveryModel m(v, s);
                     return json::value(m.recoveryValue(makeDate(d), credit::portfolio::makeKey(k)));
                 };
             }},
            {"ConstantRecoveryModel::appliesToSeniority",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 Real v = real(a[0]);
                 auto s = enumeration<Seniority>(a[1]), asked = enumeration<Seniority>(a[2]);
                 return [=] { return json::value(ConstantRecoveryModel(v, s).appliesToSeniority(asked)); };
             }},
        });

    }

}
