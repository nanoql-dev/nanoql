// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/instruments/bonds/btp.hpp: the Italian CCTEU and BTP, the Rendistato basket and calculator and its two quotes,
// which the wheel doesn't bind, each built inside the call under the case's settings:
//
//     CCTEU                 (maturity, spread, curve, start, issue, dates): [settlement date, cash-flow dates and
//                           amounts, accrued amounts at the dates, clean and dirty prices on DiscountingBondEngine]
//     BTP                   (btp, curve, clean price, dates): [settlement date, cash-flow dates and amounts, accrued
//                           amounts at the dates, clean and dirty prices on DiscountingBondEngine, the yield of the
//                           clean price, and at the last date]
//     RendistatoBasket      (btps, outstandings, clean prices): [size, outstanding, outstandings, weights]
//     RendistatoCalculator  (btps, outstandings, clean prices, Euribor tenor, Euribor curve, discount curve or null):
//                           [yield, duration, yields, durations, swap lengths, swap rates, swap yields, swap durations,
//                           equivalent swap rate, yield, duration, length and spread, the equivalent swap's fixed rate
//                           and maturity, the length quote's and the spread quote's values]
//
// A BTP is {"maturity", "rate", "redemption" (null for the par constructor), "start", "issue"}, a null start or issue
// QuantLib's null date; a curve an indexes/indexspec.hpp curve spec, the CCTEU's its Euribor 6M forwarding curve; a
// null date in the dates the bond's settlement date. A cash-flow amount, accrued amount or price QuantLib refuses is
// null; a swap rate, yield or duration the calculator hasn't computed is null where QuantLib's is Null<Real>() and its
// starting guess where QuantLib's is (the yields).

#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/indexes/ibor/euribor.hpp>
#include <ql/instruments/bonds/btp.hpp>
#include <ql/pricingengines/bond/discountingbondengine.hpp>
#include <ql/quotes/simplequote.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " needs " + name);
            return *v;
        }

        struct BtpSpec {
            DateSpec maturity, start, issue;
            Real rate = 0.0, redemption = Null<Real>();
        };

        BtpSpec btp(const json::value& v) {
            const std::string what = "a BTP";
            if (!v.is_object())
                throw InvalidParams("expected " + what + ", got " + json::serialize(v));
            const json::object& o = v.get_object();
            BtpSpec s;
            s.maturity = date(key(o, "maturity", what));
            s.rate = real(key(o, "rate", what));
            s.redemption = real(key(o, "redemption", what));
            s.start = date(key(o, "start", what));
            s.issue = date(key(o, "issue", what));
            return s;
        }

        std::vector<BtpSpec> btps(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected an array of BTPs, got " + json::serialize(v));
            std::vector<BtpSpec> out;
            for (const auto& x : v.get_array())
                out.push_back(btp(x));
            return out;
        }

        ext::shared_ptr<BTP> makeBtp(const BtpSpec& s) {
            if (s.redemption == Null<Real>())
                return ext::make_shared<BTP>(makeDate(s.maturity), s.rate, makeDate(s.start), makeDate(s.issue));
            return ext::make_shared<BTP>(makeDate(s.maturity), s.rate, s.redemption, makeDate(s.start),
                                         makeDate(s.issue));
        }

        json::value number(Real x) {
            return x == Null<Real>() ? json::value(nullptr) : json::value(x);
        }

        template <class F>
        json::value attempt(F f) {
            try {
                return number(f());
            } catch (const Error&) {
                return nullptr;
            }
        }

        json::array numbers(const std::vector<Real>& xs) {
            json::array out;
            for (Real x : xs)
                out.emplace_back(number(x));
            return out;
        }

        // the bond's settlement date, cash flows, accrued amounts and prices on the curve
        json::array bondOutputs(const ext::shared_ptr<Bond>& bond, const indexes::CurveSpec& curve,
                                const std::vector<DateSpec>& dates) {
            bond->setPricingEngine(ext::make_shared<DiscountingBondEngine>(indexes::makeCurve(curve)));
            json::array flowDates, amounts, accrued;
            for (const auto& cf : bond->cashflows()) {
                flowDates.emplace_back(result(cf->date()));
                amounts.emplace_back(attempt([&] { return cf->amount(); }));
            }
            for (const auto& d : dates)
                accrued.emplace_back(attempt([&] { return bond->accruedAmount(makeDate(d)); }));
            json::array out;
            out.emplace_back(result(bond->settlementDate()));
            out.emplace_back(flowDates);
            out.emplace_back(amounts);
            out.emplace_back(accrued);
            out.emplace_back(attempt([&] { return bond->cleanPrice(); }));
            out.emplace_back(attempt([&] { return bond->dirtyPrice(); }));
            return out;
        }

        std::vector<DateSpec> dateList(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected an array of dates, got " + json::serialize(v));
            std::vector<DateSpec> out;
            for (const auto& x : v.get_array())
                out.push_back(date(x));
            return out;
        }

        ext::shared_ptr<RendistatoBasket> makeBasket(const std::vector<BtpSpec>& specs,
                                                     const std::vector<Real>& outstandings,
                                                     const std::vector<Real>& prices) {
            std::vector<ext::shared_ptr<BTP>> bonds;
            for (const auto& s : specs)
                bonds.push_back(makeBtp(s));
            std::vector<Handle<Quote>> quotes;
            for (Real p : prices)
                quotes.emplace_back(ext::make_shared<SimpleQuote>(p));
            return ext::make_shared<RendistatoBasket>(bonds, outstandings, quotes);
        }

        const Register unit({
            {"CCTEU",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 DateSpec maturity = date(a[0]), start = date(a[3]), issue = date(a[4]);
                 Real spread = real(a[1]);
                 indexes::CurveSpec curve = indexes::curve(a[2]);
                 std::vector<DateSpec> dates = dateList(a[5]);
                 return [=] {
                     auto bond = ext::make_shared<CCTEU>(makeDate(maturity), spread, indexes::makeCurve(curve),
                                                         makeDate(start), makeDate(issue));
                     return json::value(bondOutputs(bond, curve, dates));
                 };
             }},
            {"BTP",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 BtpSpec spec = btp(a[0]);
                 indexes::CurveSpec curve = indexes::curve(a[1]);
                 Real clean = real(a[2]);
                 std::vector<DateSpec> dates = dateList(a[3]);
                 return [=] {
                     auto bond = makeBtp(spec);
                     json::array out = bondOutputs(bond, curve, dates);
                     out.emplace_back(attempt([&] { return bond->yield(clean); }));
                     out.emplace_back(attempt([&] { return bond->yield(clean, makeDate(dates.back())); }));
                     return json::value(out);
                 };
             }},
            {"RendistatoBasket",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 std::vector<BtpSpec> specs = btps(a[0]);
                 std::vector<Real> outstandings = reals(a[1]), prices = reals(a[2]);
                 return [=] {
                     auto basket = makeBasket(specs, outstandings, prices);
                     json::array out;
                     out.emplace_back(static_cast<std::int64_t>(basket->size()));
                     out.emplace_back(basket->outstanding());
                     out.emplace_back(numbers(basket->outstandings()));
                     out.emplace_back(numbers(basket->weights()));
                     return json::value(out);
                 };
             }},
            {"RendistatoCalculator",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 std::vector<BtpSpec> specs = btps(a[0]);
                 std::vector<Real> outstandings = reals(a[1]), prices = reals(a[2]);
                 std::vector<Integer> tenor = integers(a[3]);
                 if (tenor.size() != 2)
                     throw InvalidParams("expected a tenor [length, units], got " + json::serialize(a[3]));
                 indexes::CurveSpec euriborCurve = indexes::curve(a[4]);
                 bool noDiscount = a[5].is_null();
                 indexes::CurveSpec discount = noDiscount ? indexes::CurveSpec() : indexes::curve(a[5]);
                 return [=] {
                     auto basket = makeBasket(specs, outstandings, prices);
                     auto euribor = ext::make_shared<Euribor>(Period(tenor[0], static_cast<TimeUnit>(tenor[1])),
                                                              indexes::makeCurve(euriborCurve));
                     Handle<YieldTermStructure> discountCurve =
                         noDiscount ? Handle<YieldTermStructure>() : indexes::makeCurve(discount);
                     auto r = ext::make_shared<RendistatoCalculator>(basket, euribor, discountCurve);
                     json::array out;
                     out.emplace_back(r->yield());
                     out.emplace_back(r->duration());
                     out.emplace_back(numbers(r->yields()));
                     out.emplace_back(numbers(r->durations()));
                     out.emplace_back(numbers(r->swapLengths()));
                     out.emplace_back(numbers(r->swapRates()));
                     out.emplace_back(numbers(r->swapYields()));
                     out.emplace_back(numbers(r->swapDurations()));
                     out.emplace_back(r->equivalentSwapRate());
                     out.emplace_back(r->equivalentSwapYield());
                     out.emplace_back(r->equivalentSwapDuration());
                     out.emplace_back(r->equivalentSwapLength());
                     out.emplace_back(r->equivalentSwapSpread());
                     out.emplace_back(r->equivalentSwap()->fixedRate());
                     out.emplace_back(result(r->equivalentSwap()->maturityDate()));
                     out.emplace_back(RendistatoEquivalentSwapLengthQuote(r).value());
                     out.emplace_back(RendistatoEquivalentSwapSpreadQuote(r).value());
                     return json::value(out);
                 };
             }},
        });

    }

}
