using System;
#if UNITY_EDITOR || (GODOT && TOOLS)
using System.Collections.Generic;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 定义所有 FsmKit 状态机共用的业务生命周期；Editor/Tools 构建额外提供诊断读取契约。
    /// </summary>
    public interface IFSM : IState
    {
        /// <summary>获取状态机生命周期阶段。</summary>
        MachineState MachineState { get; }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>获取状态机诊断名称。</summary>
        string Name { get; }

        /// <summary>获取状态标识使用的枚举类型。</summary>
        Type EnumType { get; }

        /// <summary>获取当前或最近聚焦的状态；没有状态时为空。</summary>
        IState CurrentState { get; }

        /// <summary>获取当前或最近聚焦状态的整数标识；没有状态时为 -1。</summary>
        int CurrentStateId { get; }

        /// <summary>
        /// 获取按整数状态标识索引的独立快照；修改返回字典不会影响状态机。
        /// </summary>
        /// <returns>状态字典快照。</returns>
        IReadOnlyDictionary<int, IState> GetAllStates();

        /// <summary>
        /// 获取状态最初加入状态机时的稳定顺序。
        /// </summary>
        /// <param name="stateId">状态整数标识。</param>
        /// <returns>加入顺序；状态不存在时返回 stateId。</returns>
        int GetStateOrderIndex(int stateId);
#endif
    }

    /// <summary>
    /// 定义由枚举标识状态的状态机入口。
    /// </summary>
    /// <typeparam name="TEnum">状态枚举类型。</typeparam>
    public interface IFSM<TEnum> : IFSM where TEnum : Enum
    {
        /// <summary>获取当前或最近聚焦的状态枚举值。</summary>
        TEnum CurEnum { get; }

        /// <summary>
        /// 获取指定状态；状态不存在时通过 out 空值返回。
        /// </summary>
        /// <param name="id">状态标识。</param>
        /// <param name="state">找到的状态。</param>
        void Get(TEnum id, out IState state);

        /// <summary>从指定状态启动状态机。</summary>
        /// <param name="id">状态标识。</param>
        void Start(TEnum id);

        /// <summary>添加或替换指定状态。</summary>
        /// <param name="id">状态标识。</param>
        /// <param name="state">状态实例。</param>
        void Add(TEnum id, IState state);

        /// <summary>移除并释放指定状态。</summary>
        /// <param name="id">状态标识。</param>
        void Remove(TEnum id);

        /// <summary>切换或启动指定状态。</summary>
        /// <param name="id">状态标识。</param>
        void Change(TEnum id);

        /// <summary>使用参数切换或启动指定状态。</summary>
        /// <typeparam name="TArgs">进入参数类型。</typeparam>
        /// <param name="id">状态标识。</param>
        /// <param name="args">进入参数。</param>
        void Change<TArgs>(TEnum id, TArgs args);

        /// <summary>结束并释放全部状态，使状态机回到空 End 状态。</summary>
        void Clear();
    }

    /// <summary>
    /// 定义状态机自身启动时需要参数的入口。
    /// </summary>
    /// <typeparam name="TEnum">状态枚举类型。</typeparam>
    /// <typeparam name="TArgs">启动参数类型。</typeparam>
    public interface IFSM<TEnum, TArgs> : IFSM<TEnum>, IState<TArgs> where TEnum : Enum
    {
        /// <summary>从指定状态使用参数启动状态机。</summary>
        /// <param name="id">状态标识。</param>
        /// <param name="args">启动参数。</param>
        void Start(TEnum id, TArgs args);
    }
}
