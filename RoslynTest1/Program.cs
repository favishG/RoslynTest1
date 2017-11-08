using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;


/*
#######################################################################################################################################

RoslynTest1

Combining roslyn assemblies

NOTE: This project is based on the code posted by "Nick Polyak" at stackoverflow "https://stackoverflow.com/questions/37213781/compiling-classes-separately-using-roslyn-and-combining-them-together-in-an-asse/37239671" and also available at the roslyn site "https://github.com/dotnet/roslyn/issues/11297".

I did not make this code, I only did a visual stuio 2017 project to contain the original code submmited by "Nick Polyak" at stackoverflow in order to test it, and to share with him in order to ask for help because I can not make it work.

#######################################################################################################################################
*/



namespace RoslynTest1
{
    public static class Program
    {
        // NOTE: I could not include the namespace "Microsoft.CodeAnalysis.CSharp.Test.Utilities", so I did a copy of some definitions required to thest this code.
        // See http://source.roslyn.io/#Roslyn.Compilers.CSharp.Test.Utilities/TestOptions.cs,0e91807b8f0500de
        public static class TestOptions
        {
            public static readonly CSharpCompilationOptions ReleaseExe = new CSharpCompilationOptions(OutputKind.ConsoleApplication, optimizationLevel: OptimizationLevel.Release);
            public static readonly CSharpCompilationOptions ReleaseModule = new CSharpCompilationOptions(OutputKind.NetModule, optimizationLevel: OptimizationLevel.Release);
        }


        public static void Main()
        {
            try
            {
                var s1 = @"public class A {internal object o1 = new { hello = 1, world = 2 }; public static string M1() {    return ""Hello, "";}}";
                var s2 = @"public class B : A{internal object o2 = new { hello = 1, world = 2 };public static string M2(){    return ""world!"";}}";
                var s3 = @"public class Program{public static void Main(){    System.Console.Write(A.M1());    System.Console.WriteLine(B.M2());}}";
                var comp1 = CreateCompilationWithMscorlib("a1", s1, compilerOptions: TestOptions.ReleaseModule);
                var ref1 = comp1.EmitToImageReference();

                var comp2 = CreateCompilationWithMscorlib("a2", s2, compilerOptions: TestOptions.ReleaseModule, references: new[] { ref1 });
                var ref2 = comp2.EmitToImageReference();

                var comp3 = CreateCompilationWithMscorlib("a3", s3, compilerOptions: TestOptions.ReleaseExe.WithModuleName("C"), references: new[] { ref1, ref2 });

                var ref3 = comp3.EmitToImageReference();

                IEnumerable<byte> result = comp3.EmitToArray();

                Assembly assembly = Assembly.Load(result.ToArray());






                // NOTE: This snippet is added by me an is not in the original code.
                // If this snippet is commented the program fails with the error:
                //
                //
                // System.Reflection.TargetInvocationException: Se produjo una excepción en el destino de la invocación. --->System.IO.FileNotFoundException: No se puede cargar el archivo o ensamblado 'a1.netmodule' ni una de sus dependencias.El sistema no puede encontrar el archivo especificado.
                // en Program.Main()
                // --- Fin del seguimiento de la pila de la excepción interna-- -
                // en System.RuntimeMethodHandle.InvokeMethod(Object target, Object[] arguments, Signature sig, Boolean constructor)
                // en System.Reflection.RuntimeMethodInfo.UnsafeInvokeInternal(Object obj, Object[] parameters, Object[] arguments)
                // en System.Reflection.RuntimeMethodInfo.Invoke(Object obj, BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)
                // en System.Reflection.MethodBase.Invoke(Object obj, Object[] parameters)
                // en RoslynTest1.Program.Main() en P:\Test\RoslynTest1\RoslynTest1\Program.cs:línea 116
                //
                //
                // So as it says at stackoverflow, I tried to load the modules "a1" and "a2", but this snippet does not work and the program fails with the error:
                //
                //
                // System.IO.FileLoadException: No se puede cargar el archivo o ensamblado '3072 bytes loaded from a3, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null' ni una de sus dependencias.Error de comprobación del hash del módulo. (Excepción de HRESULT: 0x80131039)
                // Nombre de archivo: '3072 bytes loaded from a3, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null'--->System.IO.FileLoadException: Error de comprobación del hash del módulo. (Excepción de HRESULT: 0x80131039)
                // en System.Reflection.RuntimeAssembly.LoadModule(RuntimeAssembly assembly, String moduleName, Byte[] rawModule, Int32 cbModule, Byte[] rawSymbolStore, Int32 cbSymbolStore, ObjectHandleOnStack retModule)
                // en System.Reflection.RuntimeAssembly.LoadModule(String moduleName, Byte[] rawModule, Byte[] rawSymbolStore)
                // en System.Reflection.Assembly.LoadModule(String moduleName, Byte[] rawModule)
                // en RoslynTest1.Program.Main() en P:\Test\RoslynTest1\RoslynTest1\Program.cs:línea 97
                //
                /*
                {
                    var a1_netmodule_bytes = comp1.EmitToArray().ToArray();
                    assembly.LoadModule("a1.netmodule", a1_netmodule_bytes);

                    var a2_netmodule_bytes = comp2.EmitToArray().ToArray();
                    assembly.LoadModule("a2.netmodule", a2_netmodule_bytes);
                }
                */
                




                Module module = assembly.GetModule("C");

                Type prog = module.GetType("Program");

                object instance = Activator.CreateInstance(prog);

                MethodInfo method = prog.GetMethod("Main");

                method.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public static ImmutableArray<byte> ToImmutable(this MemoryStream stream)
        {
            return ImmutableArray.Create<byte>(stream.ToArray());
        }

        internal static ImmutableArray<byte> EmitToArray
        (
            this Compilation compilation,
            EmitOptions options = null,
            Stream pdbStream = null
        )
        {
            var stream = new MemoryStream();

            if (pdbStream == null && compilation.Options.OptimizationLevel == OptimizationLevel.Debug)
            {
                pdbStream = new MemoryStream();
            }

            var emitResult = compilation.Emit(
                peStream: stream,
                pdbStream: pdbStream,
                xmlDocumentationStream: null,
                win32Resources: null,
                manifestResources: null,
                options: options);

            return stream.ToImmutable();
        }

        public static MetadataReference EmitToImageReference(
            this Compilation comp
        )
        {
            var image = comp.EmitToArray();
            if (comp.Options.OutputKind == OutputKind.NetModule)
            {
                return ModuleMetadata.CreateFromImage(image).GetReference(display: comp.AssemblyName);
            }
            else
            {
                return AssemblyMetadata.CreateFromImage(image).GetReference(display: comp.AssemblyName);
            }
        }

        private static CSharpCompilation CreateCompilationWithMscorlib(string assemblyName, string code, CSharpCompilationOptions compilerOptions = null, IEnumerable<MetadataReference> references = null)
        {
            SourceText sourceText = SourceText.From(code, Encoding.UTF8);
            SyntaxTree syntaxTree = SyntaxFactory.ParseSyntaxTree(sourceText, null, "");

            MetadataReference mscoreLibReference = AssemblyMetadata.CreateFromFile(typeof(string).Assembly.Location).GetReference();

            IEnumerable<MetadataReference> allReferences = new MetadataReference[] { mscoreLibReference };

            if (references != null)
            {
                allReferences = allReferences.Concat(references);
            }

            CSharpCompilation compilation = CSharpCompilation.Create
            (
                assemblyName,
                new[] { syntaxTree },
                options: compilerOptions,
                references: allReferences
            );

            return compilation;
        }

    }
}
