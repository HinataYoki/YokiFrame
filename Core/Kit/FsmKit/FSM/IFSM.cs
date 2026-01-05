using System;

namespace YokiFrame
{
    public interface IFSM : IState
    {
        MachineState MachineState { get; }
    }

    public interface IFSM<TEnum> : IFSM where TEnum : Enum
    {
        void Get(TEnum id, out IState state);
        void Start(TEnum id);
        void Add(TEnum id, IState state);
        void Remove(TEnum id);
        void Change(TEnum id);
        void Change<TArgs>(TEnum id, TArgs args);
        void Clear();
    }

    public interface IFSM<TEnum, TArgs> : IFSM<TEnum>, IState<TArgs> where TEnum : Enum
    {
        void Start(TEnum id, TArgs args);
    }
}