using HarmonyLib;
using NitroxPatcher.Patches;

namespace Nitrox.Test.Patcher.Patches;

/// <summary>
///     Harmony binds a prefix's, postfix's and finalizer's parameters to the patched method by name, when the patch is
///     applied in the game. A parameter that names nothing in the target (a renamed argument, a field that is not there,
///     an instance of another type) only fails then, so it is checked here against the game's code.
/// </summary>
[TestClass]
public sealed class PatchSignaturesTest
{
    private static readonly string[] patchMethodPrefixes = ["Prefix", "Postfix", "Finalizer"];
    private static readonly HashSet<string> freeParameters = ["__state", "__args", "__originalMethod", "__runOriginal", "__exception"];

    [TestMethod]
    public void EveryInjectedParameterExistsInThePatchedMethod()
    {
        List<string> problems = [];
        int checkedMethods = 0;

        foreach (Type patchType in typeof(NitroxPatcher.Main).Assembly.GetTypes().Where(type => typeof(NitroxPatch).IsAssignableFrom(type) && type is { IsClass: true, IsAbstract: false }))
        {
            MethodBase[] targets = GetTargets(patchType);
            MethodInfo[] patchMethods = patchType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                                                 .Where(method => patchMethodPrefixes.Any(prefix => method.Name.StartsWith(prefix, StringComparison.Ordinal)))
                                                 .ToArray();
            if (targets.Length == 0)
            {
                continue;
            }

            foreach (MethodInfo patchMethod in patchMethods)
            {
                checkedMethods++;
                // A patch class with several targets pairs them with its methods in its own Patch(); a method fitting one of them is taken as meant for it
                List<string>[] perTarget = targets.Select(target => Check(patchMethod, target)).ToArray();
                if (perTarget.All(found => found.Count > 0))
                {
                    problems.Add($"{patchType.Name}.{patchMethod.Name}: {string.Join("; ", perTarget.OrderBy(found => found.Count).First())}");
                }
            }
        }

        checkedMethods.Should().BeGreaterThan(300, "the patch classes and their target fields are found by convention");
        problems.Should().BeEmpty();
    }

    [TestMethod]
    public void AParameterThatNamesNothingIsReported()
    {
        MethodInfo target = typeof(Sample).GetMethod(nameof(Sample.Target))!;

        Check(typeof(Sample).GetMethod(nameof(Sample.Fits))!, target).Should().BeEmpty();
        Check(typeof(Sample).GetMethod(nameof(Sample.WrongName))!, target).Should().ContainSingle().Which.Should().Contain("no such parameter");
        Check(typeof(Sample).GetMethod(nameof(Sample.WrongInstance))!, target).Should().ContainSingle().Which.Should().Contain("__instance is String");
        Check(typeof(Sample).GetMethod(nameof(Sample.WrongField))!, target).Should().ContainSingle().Which.Should().Contain("no field missing");
        Check(typeof(Sample).GetMethod(nameof(Sample.ResultOfNothing))!, target).Should().ContainSingle().Which.Should().Contain("returns nothing");
    }

    private static MethodBase[] GetTargets(Type patchType)
    {
        try
        {
            return patchType.GetRuntimeFields()
                            .Where(field => field.IsStatic && typeof(MethodBase).IsAssignableFrom(field.FieldType))
                            .Where(field => field.Name.Replace("_", "").StartsWith("targetMethod", StringComparison.OrdinalIgnoreCase))
                            .Select(field => field.GetValue(null) as MethodBase)
                            .Where(target => target != null)
                            .ToArray()!;
        }
        catch (Exception ex) when (ex is TargetInvocationException or TypeInitializationException)
        {
            // A few patch classes resolve services in their static fields, which only works in the game
            return [];
        }
    }

    private static List<string> Check(MethodInfo patchMethod, MethodBase target)
    {
        List<string> problems = [];
        ParameterInfo[] targetParameters = target.GetParameters();
        Type declaringType = target.DeclaringType!;

        foreach (ParameterInfo parameter in patchMethod.GetParameters())
        {
            string name = parameter.Name!;
            Type type = Strip(parameter.ParameterType);

            if (freeParameters.Contains(name))
            {
                continue;
            }
            if (name == "__instance")
            {
                if (target.IsStatic)
                {
                    problems.Add($"__instance on static {Describe(target)}");
                }
                else if (!type.IsAssignableFrom(declaringType))
                {
                    problems.Add($"__instance is {type.Name} but {Describe(target)} is declared in {declaringType.Name}");
                }
                continue;
            }
            if (name == "__result")
            {
                Type returnType = target is MethodInfo method ? method.ReturnType : typeof(void);
                if (returnType == typeof(void))
                {
                    problems.Add($"__result on {Describe(target)}, which returns nothing");
                }
                else if (!type.IsAssignableFrom(returnType))
                {
                    problems.Add($"__result is {type.Name} but {Describe(target)} returns {returnType.Name}");
                }
                continue;
            }
            if (name.StartsWith("___", StringComparison.Ordinal))
            {
                FieldInfo? field = AccessTools.Field(declaringType, name[3..]);
                if (field == null)
                {
                    problems.Add($"{name}: {declaringType.Name} has no field {name[3..]}");
                }
                else if (!type.IsAssignableFrom(field.FieldType))
                {
                    problems.Add($"{name} is {type.Name} but the field is {field.FieldType.Name}");
                }
                continue;
            }
            if (name.StartsWith("__", StringComparison.Ordinal) && int.TryParse(name[2..], out int index))
            {
                if (index >= targetParameters.Length)
                {
                    problems.Add($"{name}: {Describe(target)} has {targetParameters.Length} parameters");
                }
                continue;
            }

            ParameterInfo? targetParameter = targetParameters.FirstOrDefault(candidate => candidate.Name == name);
            if (targetParameter == null)
            {
                problems.Add($"{name}: {Describe(target)} has no such parameter ({string.Join(", ", targetParameters.Select(candidate => candidate.Name))})");
            }
            else if (!type.IsAssignableFrom(Strip(targetParameter.ParameterType)))
            {
                problems.Add($"{name} is {type.Name} but the parameter is {Strip(targetParameter.ParameterType).Name}");
            }
        }
        return problems;
    }

    private sealed class Sample
    {
#pragma warning disable CS0169, IDE0051 // Only read through reflection, as Harmony does
        private int counter;
#pragma warning restore CS0169, IDE0051

        public void Target(int amount)
        {
        }

        public static void Fits(Sample __instance, ref int amount, int ___counter, object __state)
        {
        }

        public static void WrongName(int count)
        {
        }

        public static void WrongInstance(string __instance)
        {
        }

        public static void WrongField(int ___missing)
        {
        }

        public static void ResultOfNothing(int __result)
        {
        }
    }

    private static Type Strip(Type type) => type.IsByRef ? type.GetElementType()! : type;

    private static string Describe(MethodBase method) => $"{method.DeclaringType?.Name}.{method.Name}";
}
