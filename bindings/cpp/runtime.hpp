// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The hand-written part of nanoql.hpp (docs/bindings.md, "C++"): errors, the owning base class, the views a call takes
// collections through, matrices and loss distributions, and the conversions the generated members call. nanoql-bind
// writes it into the header after the enums and before the classes it generates from the surface.

// A deprecated member's definition, which calls its deprecated C function, warns in no user's build.
#if defined(__clang__) || defined(__GNUC__)
#define NANOQL_ALLOW_DEPRECATED_BEGIN_ _Pragma("GCC diagnostic push") _Pragma("GCC diagnostic ignored \"-Wdeprecated-declarations\"")
#define NANOQL_ALLOW_DEPRECATED_END_ _Pragma("GCC diagnostic pop")
#elif defined(_MSC_VER)
#define NANOQL_ALLOW_DEPRECATED_BEGIN_ __pragma(warning(push)) __pragma(warning(disable : 4996))
#define NANOQL_ALLOW_DEPRECATED_END_ __pragma(warning(pop))
#else
#define NANOQL_ALLOW_DEPRECATED_BEGIN_
#define NANOQL_ALLOW_DEPRECATED_END_
#endif

// ---- errors: one exception class per refusal kind ----

// A refusal: its status code, the parameter as .NET names it, and the .NET exception's type.
class error : public std::runtime_error {
public:
    error(int32_t code, const std::string& message, std::string param, std::string type_name)
        : std::runtime_error(message), code_(code), param_(std::move(param)), type_name_(std::move(type_name)) {}
    int32_t code() const noexcept { return code_; }
    const std::string& param() const noexcept { return param_; }
    const std::string& type_name() const noexcept { return type_name_; }

private:
    int32_t code_;
    std::string param_;
    std::string type_name_;
};

// NQL_ERROR_ARGUMENT, and the two kinds under it.
class argument_error : public error {
public:
    using error::error;
};
class argument_null_error : public argument_error {
public:
    using argument_error::argument_error;
};
class argument_out_of_range_error : public argument_error {
public:
    using argument_error::argument_error;
};

// A refused handle: NQL_ERROR_HANDLE and NQL_ERROR_TYPE.
class handle_error : public error {
public:
    using error::error;
};

namespace detail {
struct adopt_t {
    explicit adopt_t() = default;
};
inline constexpr adopt_t adopt{};

inline std::string text(const char* p) { return p ? std::string(p) : std::string(); }

[[noreturn]] inline void raise(nql_status s)
{
    std::string m = text(nql_last_error_message()), p = text(nql_last_error_param()), t = text(nql_last_error_type());
    switch (s) {
    case NQL_ERROR_ARGUMENT: throw argument_error(s, m, p, t);
    case NQL_ERROR_ARGUMENT_NULL: throw argument_null_error(s, m, p, t);
    case NQL_ERROR_ARGUMENT_RANGE: throw argument_out_of_range_error(s, m, p, t);
    case NQL_ERROR_HANDLE:
    case NQL_ERROR_TYPE: throw handle_error(s, m, p, t);
    default: throw error(s, m, p, t);
    }
}

inline void check(nql_status s)
{
    if (s != NQL_OK) raise(s);
}

// A string nanoql returned, freed; a null string is empty.
inline std::string take(char* p)
{
    std::string s = text(p);
    nql_free(p);
    return s;
}

// A new object's handle, or a value's C form, from a C function's out-parameter.
template <class F>
nql_handle make(F f)
{
    nql_handle h = nullptr;
    check(f(&h));
    return h;
}
template <class T, class F>
T value(F f)
{
    T v{};
    check(f(&v));
    return v;
}

template <class T>
struct is_optional : std::false_type {};
template <class T>
struct is_optional<std::optional<T>> : std::true_type {};

// A null object of a class, for a parameter's default: defined after every class, since a default may name a class
// the header defines later.
template <class C>
const C& null_of();
}  // namespace detail

// ---- objects ----

// Every nanoql object: immutable, behind a handle the wrapper owns, or null (nanoql's null reference). A copy takes a
// new handle to the same object (nql_retain), a move takes the handle, and the destructor releases it.
class Object {
public:
    Object() noexcept = default;
    Object(std::nullptr_t) noexcept {}
    Object(detail::adopt_t, nql_handle h) noexcept : h_(h) {}
    Object(const Object& o) : h_(nullptr)
    {
        if (o.h_) detail::check(nql_retain(o.h_, &h_));
    }
    Object(Object&& o) noexcept : h_(std::exchange(o.h_, nullptr)) {}
    Object& operator=(const Object& o)
    {
        if (this != &o) {
            Object copy(o);
            std::swap(h_, copy.h_);
        }
        return *this;
    }
    Object& operator=(Object&& o) noexcept
    {
        if (this != &o) {
            Object taken(std::move(o));
            std::swap(h_, taken.h_);
        }
        return *this;
    }
    virtual ~Object()
    {
        if (h_) nql_release(h_);
    }

    // The handle, still the wrapper's.
    nql_handle handle() const noexcept { return h_; }
    explicit operator bool() const noexcept { return h_ != nullptr; }

    // The object's .NET type, its ToString and its hash code.
    std::string typeName() const
    {
        char* r = nullptr;
        detail::check(nql_type_name(h_, &r));
        return detail::take(r);
    }
    std::string toString() const
    {
        char* r = nullptr;
        detail::check(nql_to_string(h_, &r));
        return detail::take(r);
    }
    int32_t hashCode() const
    {
        int32_t r = 0;
        detail::check(nql_hash(h_, &r));
        return r;
    }

    // nanoql's equality (Equals); two nulls are equal.
    friend bool operator==(const Object& a, const Object& b)
    {
        if (!a.h_ || !b.h_) return a.h_ == b.h_;
        bool r = false;
        detail::check(nql_equals(a.h_, b.h_, &r));
        return r;
    }
    friend bool operator!=(const Object& a, const Object& b) { return !(a == b); }

protected:
    nql_handle h_ = nullptr;
};

// An object as another class: a new handle to it, unchecked until a member is called (a member of a class the object
// isn't refuses with handle_error, NQL_ERROR_TYPE).
template <class T>
T object_cast(const Object& o)
{
    nql_handle h = nullptr;
    if (o.handle()) detail::check(nql_retain(o.handle(), &h));
    return T(detail::adopt, h);
}

// An engine bound with its inputs (docs/bindings.md, "Bound engines"), for the members that take an engine.
class BoundEngine : public virtual Object {
public:
    BoundEngine(std::nullptr_t) noexcept {}
    BoundEngine(detail::adopt_t, nql_handle h) noexcept : Object(detail::adopt, h) {}
    BoundEngine(const BoundEngine&) = default;
    BoundEngine(BoundEngine&&) noexcept = default;
    BoundEngine& operator=(const BoundEngine& o)
    {
        Object::operator=(o);
        return *this;
    }
    BoundEngine& operator=(BoundEngine&& o) noexcept
    {
        Object::operator=(std::move(o));
        return *this;
    }

protected:
    BoundEngine() = default;
};

// ---- values ----

// A read-only view of values a call takes, or null: a vector, a braced list (living to the end of the call's
// expression), or nullptr. Empty and null differ, as nanoql's empty collection and null do.
template <class T>
class array_view {
public:
    array_view(std::nullptr_t) noexcept {}
    array_view(const std::vector<T>& v) noexcept : p_(v.data()), n_(v.size()), null_(false) {}
    array_view(std::initializer_list<T> l) noexcept : p_(l.begin()), n_(l.size()), null_(false) {}
    const T* begin() const noexcept { return p_; }
    const T* end() const noexcept { return p_ + n_; }
    std::size_t size() const noexcept { return n_; }
    bool is_null() const noexcept { return null_; }

private:
    const T* p_ = nullptr;
    std::size_t n_ = 0;
    bool null_ = true;
};

// std::vector<bool> holds bits, so a view of bools holds a copy, a byte each.
template <>
class array_view<bool> {
public:
    array_view(std::nullptr_t) noexcept {}
    array_view(const std::vector<bool>& v) : v_(v.begin(), v.end()), null_(false) {}
    array_view(std::initializer_list<bool> l) : v_(l.begin(), l.end()), null_(false) {}
    const bool* data() const noexcept { return reinterpret_cast<const bool*>(v_.data()); }
    std::size_t size() const noexcept { return v_.size(); }
    bool is_null() const noexcept { return null_; }

private:
    std::vector<unsigned char> v_;
    bool null_ = true;
};

// A matrix by rows, as nanoql's Matrix crosses.
class Matrix {
public:
    Matrix() = default;
    Matrix(int32_t rows, int32_t columns, double value = 0.0) : rows_(rows), columns_(columns), v_(static_cast<std::size_t>(rows) * columns, value) {}
    Matrix(int32_t rows, int32_t columns, std::vector<double> values) : rows_(rows), columns_(columns), v_(std::move(values))
    {
        if (v_.size() != static_cast<std::size_t>(rows) * columns) throw std::invalid_argument("a matrix's values are its rows times its columns");
    }
    Matrix(std::initializer_list<std::initializer_list<double>> rows) : rows_(static_cast<int32_t>(rows.size())), columns_(rows.size() ? static_cast<int32_t>(rows.begin()->size()) : 0)
    {
        for (auto& r : rows) {
            if (static_cast<int32_t>(r.size()) != columns_) throw std::invalid_argument("a matrix's rows have one length");
            v_.insert(v_.end(), r.begin(), r.end());
        }
    }
    int32_t rows() const noexcept { return rows_; }
    int32_t columns() const noexcept { return columns_; }
    double& operator()(int32_t i, int32_t j) { return v_[static_cast<std::size_t>(i) * columns_ + j]; }
    double operator()(int32_t i, int32_t j) const { return v_[static_cast<std::size_t>(i) * columns_ + j]; }
    double* data() noexcept { return v_.data(); }
    const double* data() const noexcept { return v_.data(); }
    const std::vector<double>& values() const noexcept { return v_; }
    friend bool operator==(const Matrix& a, const Matrix& b) { return a.rows_ == b.rows_ && a.columns_ == b.columns_ && a.v_ == b.v_; }
    friend bool operator!=(const Matrix& a, const Matrix& b) { return !(a == b); }

private:
    int32_t rows_ = 0;
    int32_t columns_ = 0;
    std::vector<double> v_;
};

// A loss model's distribution: its normalized buckets' edges, widths, densities, cumulative and excess probabilities
// and averages.
struct Distribution {
    std::vector<double> x, dx, density, cumulative, excess, average;
};

namespace detail {
// A view's values in their C form, kept for the call: a pointer (NULL for null, never NULL for empty), flags for
// nullable values, and the count.
template <class C>
struct packed {
    std::vector<C> v;
    std::vector<unsigned char> has;
    bool null = true;
    const C* ptr() const noexcept
    {
        static const C empty{};
        return null ? nullptr : v.empty() ? &empty : v.data();
    }
    const bool* flags() const noexcept { return null ? nullptr : reinterpret_cast<const bool*>(has.empty() ? &empty_flag : has.data()); }
    int32_t count() const noexcept { return static_cast<int32_t>(v.size()); }
    static inline const unsigned char empty_flag = 0;
};

template <class C, class T, class F>
packed<C> pack(const array_view<T>& a, F f)
{
    packed<C> r;
    r.null = a.is_null();
    r.v.reserve(a.size());
    for (const T& x : a) r.v.push_back(f(x));
    return r;
}

// Numbers cross in place.
template <class T>
struct numbers {
    const T* p;
    int32_t n;
    bool null;
    const T* ptr() const noexcept
    {
        static const T empty{};
        return null ? nullptr : n == 0 ? &empty : p;
    }
    int32_t count() const noexcept { return n; }
};
template <class T>
numbers<T> pack_numbers(const array_view<T>& a) { return {a.begin(), static_cast<int32_t>(a.size()), a.is_null()}; }
inline numbers<bool> pack_bools(const array_view<bool>& a) { return {a.data(), static_cast<int32_t>(a.size()), a.is_null()}; }
inline packed<const char*> pack_strings(const array_view<std::string>& a) { return pack<const char*>(a, [](const std::string& s) { return s.c_str(); }); }
template <class E>
packed<int32_t> pack_enums(const array_view<E>& a) { return pack<int32_t>(a, [](E e) { return static_cast<int32_t>(e); }); }
inline packed<nql_complex> pack_complexes(const array_view<std::complex<double>>& a) { return pack<nql_complex>(a, [](const std::complex<double>& c) { return nql_complex{c.real(), c.imag()}; }); }
template <class C>
packed<nql_handle> pack_handles(const array_view<C>& a) { return pack<nql_handle>(a, [](const C& o) { return o.handle(); }); }
template <class T>
packed<T> pack_optionals(const array_view<std::optional<T>>& a)
{
    packed<T> r;
    r.null = a.is_null();
    for (const auto& x : a) {
        r.v.push_back(x ? *x : T{});
        r.has.push_back(x ? 1 : 0);
    }
    return r;
}

// A jagged array: its values flat by rows, their count, the rows' lengths and the rows' count.
struct jagged {
    std::vector<double> v;
    std::vector<int32_t> lengths;
    bool null = true;
    const double* ptr() const noexcept
    {
        static const double empty = 0;
        return null ? nullptr : v.empty() ? &empty : v.data();
    }
    const int32_t* row_lengths() const noexcept
    {
        static const int32_t empty = 0;
        return null ? nullptr : lengths.empty() ? &empty : lengths.data();
    }
    int32_t count() const noexcept { return static_cast<int32_t>(v.size()); }
    int32_t rows() const noexcept { return static_cast<int32_t>(lengths.size()); }
};
inline jagged pack_jagged(const array_view<std::vector<double>>& a)
{
    jagged r;
    r.null = a.is_null();
    for (const auto& row : a) {
        r.v.insert(r.v.end(), row.begin(), row.end());
        r.lengths.push_back(static_cast<int32_t>(row.size()));
    }
    return r;
}

// A map's values in their C form, with flags where they are nullable.
template <class C, class K, class V, class F>
packed<C> pack_values(const array_view<std::pair<K, V>>& a, F f)
{
    packed<C> r;
    r.null = a.is_null();
    for (const auto& e : a) {
        r.v.push_back(f(e));
        if constexpr (is_optional<V>::value) r.has.push_back(e.second ? 1 : 0);
    }
    return r;
}

// A matrix's values by rows (never NULL for an empty one, NULL for none) and its shape.
inline const double* in_matrix(const Matrix& m) noexcept
{
    static const double empty = 0;
    return m.values().empty() ? &empty : m.data();
}
inline const double* in_matrix(const std::optional<Matrix>& m) noexcept { return m ? in_matrix(*m) : nullptr; }
inline int32_t rows(const Matrix& m) noexcept { return m.rows(); }
inline int32_t rows(const std::optional<Matrix>& m) noexcept { return m ? m->rows() : 0; }
inline int32_t columns(const Matrix& m) noexcept { return m.columns(); }
inline int32_t columns(const std::optional<Matrix>& m) noexcept { return m ? m->columns() : 0; }

// A nullable value's pointer: NULL for none.
template <class T>
const T* opt(const std::optional<T>& o) noexcept { return o ? &*o : nullptr; }
template <class E>
struct opt_enum {
    int32_t v;
    bool has;
    explicit opt_enum(const std::optional<E>& e) noexcept : v(e ? static_cast<int32_t>(*e) : 0), has(e.has_value()) {}
    const int32_t* ptr() const noexcept { return has ? &v : nullptr; }
};
struct opt_complex {
    nql_complex v;
    bool has;
    explicit opt_complex(const std::optional<std::complex<double>>& c) noexcept : v{c ? c->real() : 0.0, c ? c->imag() : 0.0}, has(c.has_value()) {}
    const nql_complex* ptr() const noexcept { return has ? &v : nullptr; }
};
struct opt_period {
    nql_period v;
    bool has;
    template <class P>
    explicit opt_period(const std::optional<P>& p) noexcept : v(p ? p->raw() : nql_period{0, 0}), has(p.has_value()) {}
    const nql_period* ptr() const noexcept { return has ? &v : nullptr; }
};
inline const char* opt_string(const std::optional<std::string>& s) noexcept { return s ? s->c_str() : nullptr; }
inline nql_complex complex_in(const std::complex<double>& c) noexcept { return {c.real(), c.imag()}; }
inline std::complex<double> complex_out(const nql_complex& c) { return {c.re, c.im}; }

// Results: arrays nanoql allocated, copied and freed.
template <class T, class C, class F>
std::vector<T> take_array(C* p, int32_t n, F f)
{
    std::vector<T> v;
    v.reserve(n > 0 ? n : 0);
    for (int32_t i = 0; i < n; i++) v.push_back(f(p[i]));
    nql_free(p);
    return v;
}
template <class T>
std::vector<T> take_numbers(T* p, int32_t n)
{
    std::vector<T> v(p, p + (n > 0 ? n : 0));
    nql_free(p);
    return v;
}
inline std::vector<bool> take_bools(bool* p, int32_t n) { return take_array<bool>(p, n, [](bool b) { return b; }); }
inline std::vector<std::string> take_strings(char** p, int32_t n) { return take_array<std::string>(p, n, [](char* s) { return take(s); }); }
template <class T>
std::vector<std::optional<T>> take_optionals(T* p, bool* has, int32_t n)
{
    std::vector<std::optional<T>> v;
    for (int32_t i = 0; i < n; i++) v.push_back(has[i] ? std::optional<T>(p[i]) : std::nullopt);
    nql_free(p);
    nql_free(has);
    return v;
}
inline Matrix take_matrix(double* p, int32_t rows, int32_t columns)
{
    std::vector<double> v(p, p + static_cast<std::size_t>(rows) * columns);
    nql_free(p);
    return Matrix(rows, columns, std::move(v));
}
inline std::vector<std::vector<double>> take_jagged(double* p, int32_t* lengths, int32_t rows)
{
    std::vector<std::vector<double>> v;
    int32_t at = 0;
    for (int32_t i = 0; i < rows; i++) {
        v.emplace_back(p + at, p + at + lengths[i]);
        at += lengths[i];
    }
    nql_free(p);
    nql_free(lengths);
    return v;
}
inline Distribution take_distribution(double* x, double* dx, double* density, double* cumulative, double* excess, double* average, int32_t n)
{
    return {take_numbers(x, n), take_numbers(dx, n), take_numbers(density, n), take_numbers(cumulative, n), take_numbers(excess, n), take_numbers(average, n)};
}

// A map nanoql returned: its keys and values, each taken, freed.
template <class K, class V, class CK, class CV, class FK, class FV>
std::vector<std::pair<K, V>> take_map(CK* keys, CV* values, bool* has, int32_t n, FK fk, FV fv)
{
    std::vector<std::pair<K, V>> out;
    out.reserve(n > 0 ? n : 0);
    for (int32_t i = 0; i < n; i++) out.emplace_back(fk(keys[i]), fv(values[i], has ? has[i] : true));
    nql_free(keys);
    nql_free(values);
    nql_free(has);
    return out;
}

// An in-out vector or matrix (one the member writes into), or a span: written in place, its size fixed.
template <class T>
T* inout(std::vector<T>& v) noexcept
{
    static T empty{};
    return v.empty() ? &empty : v.data();
}
inline double* inout(Matrix& m) noexcept
{
    static double empty = 0;
    return m.values().empty() ? &empty : m.data();
}
}  // namespace detail

// The handles issued and not released, for diagnostics.
inline int64_t liveHandles()
{
    int64_t n = 0;
    detail::check(nql_live_handles(&n));
    return n;
}

// The library's version ("major.minor", or "major.minor.patch" for a fix) and its ABI's, which a program checks
// against the header's NQL_ABI_VERSION.
inline std::string version() { return detail::text(nql_version()); }
inline int32_t abiVersion() { return nql_abi_version(); }
