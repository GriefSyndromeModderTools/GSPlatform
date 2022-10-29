using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace GSPlatformClient
{
    internal class Unsafe
    {
        private static readonly MethodInfo _genericAsMethod = MakeAsMethod();

        private static MethodInfo MakeAsMethod()
        {
            var asmName = new AssemblyName("GSPlatformClient.Unsafe");
            var asmBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndCollect);
            var moduleBuilder = asmBuilder.DefineDynamicModule("<Module>");
            var typeBuilder = moduleBuilder.DefineType("UnsafeHelper");
            var methodBuilder = typeBuilder.DefineMethod("As",
                MethodAttributes.Static | MethodAttributes.Public);
            var genericTypeList = methodBuilder.DefineGenericParameters("TFrom", "TTo");
            methodBuilder.SetReturnType(genericTypeList[1].MakeByRefType());
            methodBuilder.SetParameters(genericTypeList[0].MakeByRefType());

            var ilGen = methodBuilder.GetILGenerator();
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Ret);

            var type = typeBuilder.CreateType();
            return type.GetMethod(methodBuilder.Name);
        }

        private static class UnsafeRefConv<TFrom, TTo>
        {
            public delegate ref TTo ConvDelegate(ref TFrom r);
            public static readonly ConvDelegate _delegate = CreateDelegate();

            private static ConvDelegate CreateDelegate()
            {
                var m = _genericAsMethod.MakeGenericMethod(typeof(TFrom), typeof(TTo));
                return (ConvDelegate)m.CreateDelegate(typeof(ConvDelegate));
            }
        }

        public static ref TTo As<TFrom, TTo>(ref TFrom r)
            where TFrom : unmanaged
            where TTo : unmanaged
        {
            return ref UnsafeRefConv<TFrom, TTo>._delegate(ref r);
        }

        public static int SizeOf<T>()
            where T : unmanaged
        {
            return Marshal.SizeOf(typeof(T));
        }
    }
}
