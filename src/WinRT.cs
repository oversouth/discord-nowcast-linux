using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Nowcast
{
    // Windows Runtime calls without compile-time metadata. The compiler that
    // ships with .NET Framework cannot read the Windows SDK's Windows.winmd,
    // so types are looked up when the program runs and called by reflection,
    // which projects the objects the same way typed code would.
    static class WinRT
    {
        static readonly MethodInfo asTask = typeof(WindowsRuntimeSystemExtensions).GetMethods()
            .First(m => m.Name == "AsTask" && m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType.Name == "IAsyncOperation`1");

        public static Type Type(string name, string assembly)
        {
            return System.Type.GetType(name + ", " + assembly + ", ContentType=WindowsRuntime", true);
        }

        // Waits for an IAsyncOperation<result> at most timeoutMs: an app that
        // never answers costs its own session, not the whole program.
        public static object Await(object operation, Type result, int timeoutMs)
        {
            var task = (Task)asTask.MakeGenericMethod(result).Invoke(null, new[] { operation });
            if (!task.Wait(timeoutMs)) throw new TimeoutException("no answer in " + timeoutMs + " ms");
            return task.GetType().GetProperty("Result").GetValue(task);
        }

        public static object Get(object target, Type type, string property)
        {
            return type.GetProperty(property).GetValue(target);
        }

        public static object Call(object target, Type type, string method)
        {
            return type.GetMethod(method, System.Type.EmptyTypes).Invoke(target, null);
        }

        // An IInputStream as a .NET stream.
        public static Stream AsStream(object inputStream)
        {
            var inputType = Type("Windows.Storage.Streams.IInputStream", "Windows.Storage.Streams");
            var method = typeof(WindowsRuntimeStreamExtensions).GetMethod("AsStreamForRead", new[] { inputType });
            return (Stream)method.Invoke(null, new[] { inputStream });
        }
    }
}
