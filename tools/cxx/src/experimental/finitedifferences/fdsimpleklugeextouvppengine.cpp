// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The energy engines and the VPP step condition the wheel doesn't expose (ql/experimental/finitedifferences/
// fdextoujumpvanillaengine, fdsimpleextoustorageengine, fdklugeextouspreadengine, fdsimpleklugeextouvppengine,
// dynprogvppintrinsicvalueengine, fdmvppstepconditionfactory and fdmvppstartlimitstepcondition, .hpp and .cpp each):
//
//     FdExtOUJumpVanillaEngine::npv          (jumps, type, strike, american, months, rate, [t, x, y], shape)
//     FdSimpleExtOUStorageEngine::npv        (ou, rate, capacity, load, changeRate, days, [t, x], yGrid, shape)
//     FdKlugeExtOUSpreadEngine::npv          (klugeOU, heatRate, rate, months, [t, x, y, u], gasShape, powerShape)
//     FdSimpleKlugeExtOUVPPEngine::npv       (klugeOU, vpp, days, rate, fuelShape, powerShape, fuelCostAddon,
//                                             [t, x, y, g])
//     DynProgVPPIntrinsicValueEngine::npv    (vpp, days, fuelPrices, powerPrices, fuelCostAddon)
//     FdmVPPStepConditionFactory::stateMesher (vpp, days): the state mesher's locations
//     FdmVPPStartLimitStepCondition::rollback (vpp, days, fuelPrices, powerPrices, fuelCostAddon): the states after
//                                             each step of the intrinsic engine's rollback, last step first, and their
//                                             maxValue at the end
//
// The processes are extouspec.hpp's; every curve flat at its rate from the evaluation date on Actual/Actual (ISDA); a
// shape a list of [time, value] or null for none; a vpp {"heatRate", "pMin", "pMax", "tMinUp", "tMinDown",
// "startUpFuel", "startUpFixCost", "nStarts" (null for no limit)} on SwingExercise(today, today + days, 3600); the
// jump vanilla option European or American (from today) at today + months, its payoff plain vanilla; the storage
// option's exercises daily from today + 1 to today + days, its storage mesher uniform with yGrid points or the elevator
// for null; the spread option a basket option on power and gas weighted 1 and -heatRate, European at today + months.
// The intrinsic engine's and the condition's prices are read at the step t names, as QuantLib's engine reads them.

#include "experimental/processes/extouspec.hpp"

#include <ql/exercise.hpp>
#include <ql/experimental/finitedifferences/dynprogvppintrinsicvalueengine.hpp>
#include <ql/experimental/finitedifferences/fdextoujumpvanillaengine.hpp>
#include <ql/experimental/finitedifferences/fdklugeextouspreadengine.hpp>
#include <ql/experimental/finitedifferences/fdmvppstartlimitstepcondition.hpp>
#include <ql/experimental/finitedifferences/fdmvppstepconditionfactory.hpp>
#include <ql/experimental/finitedifferences/fdsimpleextoustorageengine.hpp>
#include <ql/experimental/finitedifferences/fdsimpleklugeextouvppengine.hpp>
#include <ql/experimental/finitedifferences/vanillavppoption.hpp>
#include <ql/instruments/basketoption.hpp>
#include <ql/instruments/vanillaoption.hpp>
#include <ql/instruments/vanillastorageoption.hpp>
#include <ql/instruments/vanillaswingoption.hpp>
#include <ql/methods/finitedifferences/meshers/fdmmeshercomposite.hpp>
#include <ql/methods/finitedifferences/operators/fdmlinearoplayout.hpp>
#include <ql/settings.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/daycounters/actualactual.hpp>

#include <utility>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        using Shape = std::vector<std::pair<Time, Real>>;

        ext::shared_ptr<Shape> shapeOf(const json::value& v) {
            if (v.is_null())
                return {};
            if (!v.is_array())
                throw InvalidParams("expected a shape of [time, value], got " + json::serialize(v));
            auto out = ext::make_shared<Shape>();
            for (const json::value& p : v.get_array()) {
                std::vector<Real> x = reals(p);
                if (x.size() != 2)
                    throw InvalidParams("a shape point is [time, value]");
                out->emplace_back(x[0], x[1]);
            }
            return out;
        }

        Date today() { return Settings::instance().evaluationDate(); }

        ext::shared_ptr<YieldTermStructure> curve(Real rate) {
            return ext::make_shared<FlatForward>(today(), rate, ActualActual(ActualActual::ISDA));
        }

        struct VppSpec {
            Real heatRate, pMin, pMax, startUpFuel, startUpFixCost;
            Integer tMinUp, tMinDown;
            Size nStarts;
        };

        VppSpec vppOf(const json::value& v) {
            const char* what = "a vpp";
            VppSpec s{};
            s.heatRate = real(processes::field(v, "heatRate", what));
            s.pMin = real(processes::field(v, "pMin", what));
            s.pMax = real(processes::field(v, "pMax", what));
            s.tMinUp = integer(processes::field(v, "tMinUp", what));
            s.tMinDown = integer(processes::field(v, "tMinDown", what));
            s.startUpFuel = real(processes::field(v, "startUpFuel", what));
            s.startUpFixCost = real(processes::field(v, "startUpFixCost", what));
            const json::value& n = processes::field(v, "nStarts", what);
            s.nStarts = n.is_null() ? Null<Size>() : static_cast<Size>(integer(n));
            if (s.tMinUp < 0 || s.tMinDown < 0)
                throw InvalidParams("the minimum times are a Size");
            return s;
        }

        ext::shared_ptr<VanillaVPPOption> vppOption(const VppSpec& s, Integer days) {
            return ext::make_shared<VanillaVPPOption>(
                s.heatRate, s.pMin, s.pMax, static_cast<Size>(s.tMinUp), static_cast<Size>(s.tMinDown),
                s.startUpFuel, s.startUpFixCost, ext::make_shared<SwingExercise>(today(), today() + days, 3600U),
                s.nStarts);
        }

        // the intrinsic engine's calculators: the prices at the step t names
        class StepFuel : public FdmInnerValueCalculator {
          public:
            explicit StepFuel(std::vector<Real> p) : p_(std::move(p)) {}
            Real innerValue(const FdmLinearOpIterator&, Time t) override { return p_.at(static_cast<Size>(t)); }
            Real avgInnerValue(const FdmLinearOpIterator& iter, Time t) override { return innerValue(iter, t); }

          private:
            std::vector<Real> p_;
        };

        class StepSpark : public FdmInnerValueCalculator {
          public:
            StepSpark(Real heatRate, std::vector<Real> fuel, std::vector<Real> power)
            : heatRate_(heatRate), fuel_(std::move(fuel)), power_(std::move(power)) {}
            Real innerValue(const FdmLinearOpIterator&, Time t) override {
                const Size i = static_cast<Size>(t);
                return power_.at(i) - heatRate_ * fuel_.at(i);
            }
            Real avgInnerValue(const FdmLinearOpIterator& iter, Time t) override { return innerValue(iter, t); }

          private:
            Real heatRate_;
            std::vector<Real> fuel_, power_;
        };

        json::array array(const Array& x) {
            json::array out;
            for (Real xi : x)
                out.emplace_back(xi);
            return out;
        }

        const Register unit({
            {"FdExtOUJumpVanillaEngine::npv",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 const json::value spec = a[0];
                 processes::jumps(spec);
                 const auto type = enumeration<Option::Type>(a[1]);
                 const Real strike = real(a[2]);
                 const bool american = boolean(a[3]);
                 const Integer months = integer(a[4]);
                 const Real rate = real(a[5]);
                 const std::vector<Integer> g = integers(a[6]);
                 if (g.size() != 3)
                     throw InvalidParams("the grids are [t, x, y]");
                 const json::value shape = a[7];
                 shapeOf(shape);
                 return [=] {
                     const Date maturity = today() + months * Months;
                     ext::shared_ptr<Exercise> exercise =
                         american ? ext::shared_ptr<Exercise>(ext::make_shared<AmericanExercise>(today(), maturity))
                                  : ext::shared_ptr<Exercise>(ext::make_shared<EuropeanExercise>(maturity));
                     VanillaOption option(ext::make_shared<PlainVanillaPayoff>(type, strike), exercise);
                     option.setPricingEngine(ext::make_shared<FdExtOUJumpVanillaEngine>(
                         processes::jumps(spec), curve(rate), g[0], g[1], g[2], shapeOf(shape)));
                     return json::value(option.NPV());
                 };
             }},
            {"FdSimpleExtOUStorageEngine::npv",
             [](const Args& a) -> Call {
                 arity(a, 9);
                 const json::value spec = a[0];
                 processes::extendedOU(spec);
                 const Real rate = real(a[1]), capacity = real(a[2]), load = real(a[3]), changeRate = real(a[4]);
                 const Integer days = integer(a[5]);
                 const std::vector<Integer> g = integers(a[6]);
                 if (g.size() != 2)
                     throw InvalidParams("the grids are [t, x]");
                 const json::value yGrid = a[7];
                 const json::value shape = a[8];
                 shapeOf(shape);
                 return [=] {
                     std::vector<Date> dates;
                     for (Integer d = 1; d <= days; ++d)
                         dates.push_back(today() + d);
                     VanillaStorageOption option(ext::make_shared<BermudanExercise>(dates), capacity, load,
                                                 changeRate);
                     option.setPricingEngine(ext::make_shared<FdSimpleExtOUStorageEngine>(
                         processes::extendedOU(spec), curve(rate), g[0], g[1],
                         yGrid.is_null() ? Null<Size>() : static_cast<Size>(integer(yGrid)), shapeOf(shape)));
                     return json::value(option.NPV());
                 };
             }},
            {"FdKlugeExtOUSpreadEngine::npv",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 const json::value spec = a[0];
                 processes::klugeOU(spec);
                 const Real heatRate = real(a[1]), rate = real(a[2]);
                 const Integer months = integer(a[3]);
                 const std::vector<Integer> g = integers(a[4]);
                 if (g.size() != 4)
                     throw InvalidParams("the grids are [t, x, y, u]");
                 const json::value gasShape = a[5], powerShape = a[6];
                 shapeOf(gasShape);
                 shapeOf(powerShape);
                 return [=] {
                     Array weights(2);
                     weights[0] = 1.0;
                     weights[1] = -heatRate;
                     BasketOption option(ext::make_shared<AverageBasketPayoff>(
                                             ext::make_shared<PlainVanillaPayoff>(Option::Call, 0.0), weights),
                                         ext::make_shared<EuropeanExercise>(today() + months * Months));
                     option.setPricingEngine(ext::make_shared<FdKlugeExtOUSpreadEngine>(
                         processes::klugeOU(spec), curve(rate), g[0], g[1], g[2], g[3], shapeOf(gasShape),
                         shapeOf(powerShape)));
                     return json::value(option.NPV());
                 };
             }},
            {"FdSimpleKlugeExtOUVPPEngine::npv",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 const json::value spec = a[0];
                 processes::klugeOU(spec);
                 const VppSpec vpp = vppOf(a[1]);
                 const Integer days = integer(a[2]);
                 const Real rate = real(a[3]);
                 const json::value fuelShape = a[4], powerShape = a[5];
                 shapeOf(fuelShape);
                 shapeOf(powerShape);
                 const Real addon = real(a[6]);
                 const std::vector<Integer> g = integers(a[7]);
                 if (g.size() != 4)
                     throw InvalidParams("the grids are [t, x, y, g]");
                 return [=] {
                     const auto option = vppOption(vpp, days);
                     option->setPricingEngine(ext::make_shared<FdSimpleKlugeExtOUVPPEngine>(
                         processes::klugeOU(spec), curve(rate), shapeOf(fuelShape), shapeOf(powerShape), addon, g[0],
                         g[1], g[2], g[3]));
                     return json::value(option->NPV());
                 };
             }},
            {"DynProgVPPIntrinsicValueEngine::npv",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 const VppSpec vpp = vppOf(a[0]);
                 const Integer days = integer(a[1]);
                 const std::vector<Real> fuel = reals(a[2]), power = reals(a[3]);
                 const Real addon = real(a[4]);
                 // QuantLib reads the fuel price past a fuel list shorter than the power list
                 if (fuel.size() < power.size())
                     throw InvalidParams("a fuel price per power price");
                 return [=] {
                     const auto option = vppOption(vpp, days);
                     option->setPricingEngine(
                         ext::make_shared<DynProgVPPIntrinsicValueEngine>(fuel, power, addon, curve(0.0)));
                     return json::value(option->NPV());
                 };
             }},
            {"FdmVPPStepConditionFactory::stateMesher",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 const VppSpec vpp = vppOf(a[0]);
                 const Integer days = integer(a[1]);
                 return [=] {
                     const auto option = vppOption(vpp, days);
                     VanillaVPPOption::arguments args;
                     option->setupArguments(&args);
                     const FdmVPPStepConditionFactory factory(args);
                     const auto mesher = factory.stateMesher();
                     json::array out;
                     for (Real x : mesher->locations())
                         out.emplace_back(x);
                     return json::value(out);
                 };
             }},
            {"FdmVPPStartLimitStepCondition::rollback",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 const VppSpec vpp = vppOf(a[0]);
                 const Integer days = integer(a[1]);
                 const std::vector<Real> fuel = reals(a[2]), power = reals(a[3]);
                 const Real addon = real(a[4]);
                 if (fuel.size() < power.size())
                     throw InvalidParams("a fuel price per power price");
                 return [=] {
                     const auto option = vppOption(vpp, days);
                     VanillaVPPOption::arguments args;
                     option->setupArguments(&args);
                     const FdmVPPStepConditionFactory factory(args);
                     const auto mesher = ext::make_shared<FdmMesherComposite>(factory.stateMesher());
                     const FdmVPPStepConditionMesher mesh = {0U, mesher};
                     const auto condition = factory.build(mesh, addon, ext::make_shared<StepFuel>(fuel),
                                                          ext::make_shared<StepSpark>(vpp.heatRate, fuel, power));
                     Array state(mesher->layout()->dim()[0], 0.0);
                     json::array states;
                     for (Size j = power.size(); j > 0; --j) {
                         condition->applyTo(state, static_cast<Time>(j - 1));
                         states.emplace_back(array(state));
                     }
                     json::object out;
                     out["states"] = states;
                     out["maxValue"] = json::value(condition->maxValue(state));
                     return json::value(out);
                 };
             }},
        });

    }

}
