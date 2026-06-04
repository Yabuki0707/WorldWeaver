using System.Reflection;

namespace WorldWeaver.ModCore
{
    /// <summary>
    /// Mod 身份标识接口。
    /// <para>System 的发现与注册统一走 <c>[GlobalSystem]</c> 反射——此接口标记 Mod 身份，并提供反射扫描所需的程序集。</para>
    /// <para>香草不实现此接口。</para>
    /// </summary>
    public interface IMod
    {
        /// <summary>
        /// Mod 唯一名称，与 mod.json 中的 name 字段一致。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Mod 的已加载程序集，用于 <c>[GlobalSystem]</c> 反射扫描。
        /// </summary>
        Assembly Assembly { get; }
    }
}
