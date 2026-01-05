using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public interface IFSM : IState
    {
        MachineState MachineState { get; }
#if UNITY_EDITOR
        string Name { get; }
        Type EnumType { get; }
        IState CurrentState { get; }
        int CurrentStateId { get; }
        IReadOnlyDictionary<int, IState> GetAllStates();
#endif
    }

    public interface IFSM<TEnum> : IFSM where TEnum : Enum
    {
        TEnum CurEnum { get; }
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