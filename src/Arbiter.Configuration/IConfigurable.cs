namespace Arbiter.Configuration;

public interface IConfigurable<T>
{
    void Reconfigure(T configuration);
}

public interface IConfigurable<T1, T2>
{
    void Reconfigure(T1 val1, T2 val2);
}

public interface IConfigurable<T1, T2, T3>
{
    void Reconfigure(T1 val1, T2 val2, T3 val3);
}

public interface IConfigurable<T1, T2, T3, T4>
{
    void Reconfigure(T1 val1, T2 val2, T3 val3, T4 val4);
}

public interface IConfigurable<T1, T2, T3, T4, T5>
{
    void Reconfigure(T1 val1, T2 val2, T3 val3, T4 val4, T5 val5);
}

public interface IConfigurable<T1, T2, T3, T4, T5, T6>
{
    void Reconfigure(T1 val1, T2 val2, T3 val3, T4 val4, T5 val5, T6 val6);
}

public interface IConfigurable<T1, T2, T3, T4, T5, T6, T7>
{
    void Reconfigure(T1 val1, T2 val2, T3 val3, T4 val4, T5 val5, T6 val6, T7 val7);
}

public interface IConfigurable<T1, T2, T3, T4, T5, T6, T7, T8>
{
    void Reconfigure(T1 val1, T2 val2, T3 val3, T4 val4, T5 val5, T6 val6, T7 val7, T8 val8);
}
