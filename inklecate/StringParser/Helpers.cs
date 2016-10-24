using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ink
{
    public class Option<T> where T : class
    {
        bool _empty = true;
        public bool empty { get { return _empty; } }
        T val = null;
        public Option() { }
        public Option(T x) { val = x; _empty = false; }

        internal T getValue()
        {
            if (_empty)
                throw new NullReferenceException("attempted to access the content of an empty OptionalType value");
            return val;
        }

        public Option<T2> lift<T2>(Func<T, T2> f) where T2 : class
        {
            if (_empty)
                return Option<T2>.parseSuccess();
            else
                return new Option<T2>(f(val));
        }
        static Option<object> _parseSuccess = new Option<object>();
        public static Option<T> parseSuccess()
        { return new Option<T>(); }

        public static Option<T> flatten(Option<Option<T>> x)
        {
            if (x == null)
                return null;
            else if (x._empty)
                return Option<T>.parseSuccess();
            else
                return x.val;
        }
    }

    public class Either<T, K> where T : class where K : class
    {
        object val;
        public Either(T a)
        {
            val = a;
        }
        public Either(K b)
        {
            val = b;
        }
        public T2 FromEither<T2>(Func<T, T2> f, Func<K, T2> g)
        {
            if (val is T)
                return f(val as T);
            else
                return g(val as K);
        }

        public K GetRight()
        {
            if (val is K)
                return val as K;
            else
                return null;
        }

        public T GetLeft()
        {
            if (val is T)
                return val as T;
            else
                return null;
        }

        public bool IsLeft() { return val is T; }

        public static Either<T, K> Left(T a) { return new Either<T, K>(a); }
        public static Either<T, K> Right(K a) { return new Either<T, K>(a); }
    }

    public class Empty
    { static public Empty empty = new Empty(); }


    static class Helpers
    {
        public static T id<T>(T x) { return x; }
        public static void doNothing<T, K>(T x, K list) { }
    }
}
