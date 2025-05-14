using System;

namespace YokiFrame
{
    public interface IFSM<TEnum> : IState where TEnum : Enum
    {
        MachineState MachineState { get; }
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