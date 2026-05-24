using System;

namespace WorldWeaver.Systems
{
    /// <summary>
    /// 标记全局 System。被标记的 IGlobalSystem 或 ISaveSystem 实现类会通过反射自动发现并注册到容器。
    /// <para>香草与社区模组共享此标记，统一由反射扫描。</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class GlobalSystemAttribute : Attribute
    {
    }
}
