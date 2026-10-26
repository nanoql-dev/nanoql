// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/basket/spreadblackscholesvanillaengine.hpp, .cpp: the spread engines' abstract base, which the
// wheel doesn't expose, read through a derived engine of the harness's own (tests/NanoQL.Tests mirrors it): its
// formula records the forwards, strike, type, variances, discount factor and correlation the base hands it, and
// returns df·(f1 − f2 − strike) + rho.
//
// "SpreadBlackScholesVanillaEngine::inputs" takes (process1, process2, correlation, payoff, exercise): each process
// [spot, r, q, volatility], a Black-Scholes-Merton process on flat Act/365 (Fixed) forwards from the evaluation date
// and a volatility that is a number (a constant volatility) or [dates, strikes, rows] (a BlackVarianceSurface on a null
// calendar, a row of volatilities per strike, one per date, the default extrapolation); the payoff [kind, base,
// optionType, strike], a "Spread" or "Max" basket of a "PlainVanilla" or "CashOrNothing" (paying 1) payoff; the
// exercise [kind, date], "European" or "American" (from the evaluation date). It returns {"NPV", "f1", "f2", "strike",
// "optionType", "variance1", "variance2", "df", "rho"}; the base's refusal is QuantLib's, its message the error's. The
// evaluation date is the case's Settings.

#include "convert.hpp"

#include <ql/exercise.hpp>
#include <ql/instruments/basketoption.hpp>
#include <ql/instruments/payoffs.hpp>
#include <ql/pricingengines/basket/spreadblackscholesvanillaengine.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/settings.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/volatility/equityfx/blackvariancesurface.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <cstdint>
#include <string>
#include <utility>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct Recorded {
            Real f1, f2, strike, variance1, variance2, df, rho;
            Option::Type type;
        };

        class RecordingSpreadEngine : public SpreadBlackScholesVanillaEngine {
          public:
            RecordingSpreadEngine(ext::shared_ptr<GeneralizedBlackScholesProcess> process1,
                                  ext::shared_ptr<GeneralizedBlackScholesProcess> process2, Real correlation,
                                  Recorded& recorded)
            : SpreadBlackScholesVanillaEngine(std::move(process1), std::move(process2), correlation),
              recorded_(recorded) {}

          protected:
            Real calculate(Real f1, Real f2, Real strike, Option::Type optionType, Real variance1, Real variance2,
                           DiscountFactor df) const override {
                recorded_ = Recorded{f1, f2, strike, variance1, variance2, df, rho_, optionType};
                return df * (f1 - f2 - strike) + rho_;
            }

          private:
            Recorded& recorded_;
        };

        struct VolSpec {
            bool constant;
            Real vol;
            std::vector<DateSpec> dates;
            std::vector<Real> strikes;
            std::vector<std::vector<Real>> rows;
        };

        struct ProcessSpec {
            Real spot, r, q;
            VolSpec vol;
        };

        ProcessSpec processSpec(const json::value& v) {
            if (!v.is_array() || v.get_array().size() != 4)
                throw InvalidParams("expected [spot, r, q, volatility], got " + json::serialize(v));
            const json::array& s = v.get_array();
            ProcessSpec out{real(s[0]), real(s[1]), real(s[2]), {true, 0.0, {}, {}, {}}};
            if (s[3].is_number()) {
                out.vol.vol = real(s[3]);
                return out;
            }
            if (!s[3].is_array() || s[3].get_array().size() != 3)
                throw InvalidParams("expected [dates, strikes, rows], got " + json::serialize(s[3]));
            const json::array& surface = s[3].get_array();
            out.vol.constant = false;
            if (!surface[0].is_array() || !surface[2].is_array())
                throw InvalidParams("expected lists of dates and rows");
            for (const json::value& d : surface[0].get_array())
                out.vol.dates.push_back(date(d));
            out.vol.strikes = reals(surface[1]);
            for (const json::value& row : surface[2].get_array())
                out.vol.rows.push_back(reals(row));
            if (out.vol.rows.size() != out.vol.strikes.size())
                throw InvalidParams("a row per strike");
            for (const std::vector<Real>& row : out.vol.rows)
                if (row.size() != out.vol.dates.size())
                    throw InvalidParams("a volatility per date in each row");
            return out;
        }

        ext::shared_ptr<GeneralizedBlackScholesProcess> makeProcess(const ProcessSpec& p) {
            const Date today = Settings::instance().evaluationDate();
            const DayCounter dc = Actual365Fixed();
            ext::shared_ptr<BlackVolTermStructure> vol;
            if (p.vol.constant) {
                vol = ext::make_shared<BlackConstantVol>(today, NullCalendar(), p.vol.vol, dc);
            } else {
                std::vector<Date> dates;
                for (const DateSpec& d : p.vol.dates)
                    dates.push_back(makeDate(d));
                Matrix m(p.vol.strikes.size(), dates.size());
                for (Size i = 0; i < m.rows(); ++i)
                    for (Size j = 0; j < m.columns(); ++j)
                        m[i][j] = p.vol.rows[i][j];
                vol = ext::make_shared<BlackVarianceSurface>(today, NullCalendar(), dates, p.vol.strikes, m, dc);
            }
            return ext::make_shared<BlackScholesMertonProcess>(
                Handle<Quote>(ext::make_shared<SimpleQuote>(p.spot)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, p.q, dc)),
                Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, p.r, dc)),
                Handle<BlackVolTermStructure>(vol));
        }

        const Register unit({
            {"SpreadBlackScholesVanillaEngine::inputs",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 const ProcessSpec p1 = processSpec(a[0]), p2 = processSpec(a[1]);
                 const Real correlation = real(a[2]);
                 if (!a[3].is_array() || a[3].get_array().size() != 4)
                     throw InvalidParams("expected [kind, base, optionType, strike], got " + json::serialize(a[3]));
                 const json::array& payoff = a[3].get_array();
                 const std::string kind = text(payoff[0]), base = text(payoff[1]);
                 if (kind != "Spread" && kind != "Max")
                     throw InvalidParams("unknown basket " + kind);
                 if (base != "PlainVanilla" && base != "CashOrNothing")
                     throw InvalidParams("unknown payoff " + base);
                 const auto type = enumeration<Option::Type>(payoff[2]);
                 const Real strike = real(payoff[3]);
                 if (!a[4].is_array() || a[4].get_array().size() != 2)
                     throw InvalidParams("expected [kind, date], got " + json::serialize(a[4]));
                 const std::string exercise = text(a[4].get_array()[0]);
                 if (exercise != "European" && exercise != "American")
                     throw InvalidParams("unknown exercise " + exercise);
                 const DateSpec exerciseDate = date(a[4].get_array()[1]);
                 return [=] {
                     ext::shared_ptr<StrikedTypePayoff> basePayoff;
                     if (base == "PlainVanilla")
                         basePayoff = ext::make_shared<PlainVanillaPayoff>(type, strike);
                     else
                         basePayoff = ext::make_shared<CashOrNothingPayoff>(type, strike, 1.0);
                     ext::shared_ptr<BasketPayoff> basketPayoff;
                     if (kind == "Spread")
                         basketPayoff = ext::make_shared<SpreadBasketPayoff>(basePayoff);
                     else
                         basketPayoff = ext::make_shared<MaxBasketPayoff>(basePayoff);
                     ext::shared_ptr<Exercise> ex;
                     if (exercise == "European")
                         ex = ext::make_shared<EuropeanExercise>(makeDate(exerciseDate));
                     else
                         ex = ext::make_shared<AmericanExercise>(Settings::instance().evaluationDate(),
                                                                 makeDate(exerciseDate));
                     BasketOption option(basketPayoff, ex);
                     Recorded r{};
                     option.setPricingEngine(
                         ext::make_shared<RecordingSpreadEngine>(makeProcess(p1), makeProcess(p2), correlation, r));
                     const Real npv = option.NPV();
                     return json::value(json::object{{"NPV", npv}, {"f1", r.f1}, {"f2", r.f2},
                                                     {"strike", r.strike},
                                                     {"optionType", static_cast<std::int64_t>(r.type)},
                                                     {"variance1", r.variance1}, {"variance2", r.variance2},
                                                     {"df", r.df}, {"rho", r.rho}});
                 };
             }},
        });

    }

}
