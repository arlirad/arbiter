namespace Arbiter.Configuration;

public interface IAsyncConfigurable<T>
{
    ValueTask ReconfigureAsync(T configuration);
}
public interface IAsyncConfigurable<T1, T2>
{
    ValueTask ReconfigureAsync(T1 val1, T2 val2);
}
public interface IAsyncConfigurable<T1, T2, T3>
{
    ValueTask ReconfigureAsync(T1 val1, T2 val2, T3 val3);
}
public interface IAsyncConfigurable<T1, T2, T3, T4>
{
    ValueTask ReconfigureAsync(T1 val1, T2 val2, T3 val3, T4 val4);
}
public interface IAsyncConfigurable<T1, T2, T3, T4, T5>
{
    ValueTask ReconfigureAsync(T1 val1, T2 val2, T3 val3, T4 val4, T5 val5);
}
public interface IAsyncConfigurable<T1, T2, T3, T4, T5, T6>
{
    ValueTask ReconfigureAsync(T1 val1, T2 val2, T3 val3, T4 val4, T5 val5, T6 val6);
}
public interface IAsyncConfigurable<T1, T2, T3, T4, T5, T6, T7>
{
    ValueTask ReconfigureAsync(T1 val1, T2 val2, T3 val3, T4 val4, T5 val5, T6 val6, T7 val7);
}
public interface IAsyncConfigurable<T1, T2, T3, T4, T5, T6, T7, T8>
{
    ValueTask ReconfigureAsync(T1 val1, T2 val2, T3 val3, T4 val4, T5 val5, T6 val6, T7 val7, T8 val8);
}