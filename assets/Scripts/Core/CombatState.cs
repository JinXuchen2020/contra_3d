// T-SYS-001 (rendering/Core 镜像) — 战斗状态机（简单显式状态机）。
// 设计来源: templates/system_design/rendering_system.md（Core 状态机 tick：显式状态输入输出）。
// 镜像规则: 与 Assets/Scripts/Core/CombatState.cs 内容一一对应。
// 转换表（其余一律抛 InvalidOperationException — strict fail-fast）:
//   Idle      → Shooting（开火）
//   Shooting  → Idle（停火）
//   Shooting  → Reloading（弹尽换弹）
//   Reloading → Idle（换弹完成）

using System;

namespace Contra3D.Core
{
    /// <summary>战斗阶段。</summary>
    public enum CombatPhase
    {
        /// <summary>待机（未开火、未换弹）。</summary>
        Idle,

        /// <summary>射击中。</summary>
        Shooting,

        /// <summary>换弹中。</summary>
        Reloading
    }

    /// <summary>
    /// 战斗状态机 — 仅接受合法转换，非法转换抛 <see cref="InvalidOperationException"/>。
    /// 纯逻辑、零 UnityEngine 依赖，可在 dotnet/EditMode 下独立测试。
    /// </summary>
    public sealed class CombatState
    {
        /// <summary>当前阶段（初始恒为 Idle）。</summary>
        public CombatPhase Current { get; private set; }

        /// <summary>创建状态机，初始状态 Idle。</summary>
        public CombatState()
        {
            Current = CombatPhase.Idle;
        }

        /// <summary>判断 from → to 是否为合法转换。</summary>
        public static bool IsLegalTransition(CombatPhase from, CombatPhase to)
        {
            switch (from)
            {
                case CombatPhase.Idle:
                    return to == CombatPhase.Shooting;
                case CombatPhase.Shooting:
                    return to == CombatPhase.Idle || to == CombatPhase.Reloading;
                case CombatPhase.Reloading:
                    return to == CombatPhase.Idle;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 尝试转换到目标阶段；非法转换抛异常。
        /// </summary>
        /// <exception cref="InvalidOperationException">当前阶段不允许转换到目标阶段。</exception>
        public void TransitionTo(CombatPhase next)
        {
            if (!IsLegalTransition(Current, next))
            {
                throw new InvalidOperationException(
                    $"Illegal combat state transition: {Current} -> {next}.");
            }

            Current = next;
        }
    }
}
