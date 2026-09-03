// T-SYS-001 (rendering/Core 镜像) — 伤害计算纯函数。
// 设计来源: templates/system_design/rendering_system.md（Core 纯函数：同输入同输出确定性）。
// 镜像规则: 与 Assets/Scripts/Core/DamageCalculator.cs 内容一一对应。
// 公式: 最终伤害 = 基础伤害 × 部位加成 − 护甲减法，下限 0；strict fail-fast（非法参数抛 ArgumentException）。

using System;

namespace Contra3D.Core
{
    /// <summary>
    /// 伤害计算器 — 纯函数集合，无状态、无副作用。
    /// </summary>
    public static class DamageCalculator
    {
        /// <summary>
        /// 计算最终伤害：baseDamage × partMultiplier − armor，结果下限 0。
        /// </summary>
        /// <param name="baseDamage">基础伤害。必须 &gt;= 0。</param>
        /// <param name="partMultiplier">部位加成系数（如 头 2.0 / 身 1.0 / 四肢 0.7）。必须 &gt;= 0。</param>
        /// <param name="armor">护甲减法值。必须 &gt;= 0。</param>
        /// <returns>非负的最终伤害。</returns>
        /// <exception cref="ArgumentException">任一参数为 NaN 或负值。</exception>
        public static float Calculate(float baseDamage, float partMultiplier, float armor)
        {
            if (IsNegativeOrNaN(baseDamage))
            {
                throw new ArgumentException($"baseDamage must be >= 0 and finite, got {baseDamage}.", nameof(baseDamage));
            }

            if (IsNegativeOrNaN(partMultiplier))
            {
                throw new ArgumentException($"partMultiplier must be >= 0 and finite, got {partMultiplier}.", nameof(partMultiplier));
            }

            if (IsNegativeOrNaN(armor))
            {
                throw new ArgumentException($"armor must be >= 0 and finite, got {armor}.", nameof(armor));
            }

            float result = baseDamage * partMultiplier - armor;
            return result < 0f ? 0f : result;
        }

        private static bool IsNegativeOrNaN(float value)
        {
            return float.IsNaN(value) || value < 0f;
        }
    }
}
