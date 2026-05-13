using System;

namespace WorldWeaver.Systems
{
    /// <summary>
    /// 标记香草全局 System。被标记的 IGlobalSystem 实现类会通过反射自动注册到容器。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class VanillaGlobalSystemAttribute : Attribute
    {
    }
}
