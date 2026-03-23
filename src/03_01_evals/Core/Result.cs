namespace FourthDevs.Evals.Core
{
    /// <summary>
    /// Discriminated-union style Result type for C# 7.3.
    /// </summary>
    internal sealed class Result<T, E>
    {
        public bool Ok { get; }
        public T Value { get; }
        public E Error { get; }

        private Result(bool ok, T value, E error)
        {
            Ok = ok;
            Value = value;
            Error = error;
        }

        public static Result<T, E> Success(T value)
        {
            return new Result<T, E>(true, value, default(E));
        }

        public static Result<T, E> Failure(E error)
        {
            return new Result<T, E>(false, default(T), error);
        }
    }
}
