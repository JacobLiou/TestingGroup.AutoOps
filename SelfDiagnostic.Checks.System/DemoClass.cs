using System;

namespace SelfDiagnostic
{
    /// <summary>
    /// 演示类 — 用于验证 GenericMethodInvoker 能否绑定到不含 [CheckExecutor] 特性的任意方法。
    /// 在 RunBook 中通过 BindDll + BindMethod 指定即可调用。
    /// </summary>
    internal class DemoClass
    {
        /// <summary>无参演示方法 — 仅输出一行日志</summary>
        public static void DemoMethod()
        {
            Console.WriteLine("This is a demo method.");
        }

        /// <summary>带参演示方法 — 计算两个整数之和并返回</summary>
        public static int DemoMethodAdd(int a, int b)
        {
            return a + b;
        }
    }
}
