namespace NZCore
{
    public interface IGenericValueCalculator<T>
    {
        T Add(T a, T b);
        T Subtract(T a, T b);
        T Multiply(T a, T b);
        T Divide(T a, T b);
        T PowerAtoB(T a, T b);
        T PowerBtoA(T a, T b);
        T Min(T a, T b);
        T Max(T a, T b);
    }
}