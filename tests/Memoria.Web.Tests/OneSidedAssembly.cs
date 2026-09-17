using System;
using System.Reflection;
using System.Reflection.Emit;
using Memoria.EventSourcing.Dcb;
using Memoria.EventSourcing.Domain;

namespace Memoria.Web.Tests;

/// <summary>
/// An assembly declaring a type under one consistency model and nothing under the other.
/// </summary>
/// <remarks>
/// Emitted rather than compiled, because this test assembly carries both models' samples and the
/// registry scans an assembly whole: there is no way to hand it half of this one. Each holds a
/// single identifier — a stream on one side, a DCB projection id on the other — which is the least
/// a domain can declare and still be registered under a model. The getters answer nothing useful;
/// no page these are used with reads them.
/// </remarks>
internal static class OneSidedAssembly
{
    /// <summary>One stream id, and nothing of the DCB model.</summary>
    public static readonly Assembly Streamed = Declaring("StreamedOnly", "OnlyStreamId", typeof(IStreamId));

    /// <summary>One DCB projection id, and nothing of the streamed model.</summary>
    public static readonly Assembly Dcb = Declaring("DcbOnly", "OnlyProjectionId", typeof(IDcbProjectionId));

    /// <summary>No domain types at all: a service declared over an assembly that registers nothing.</summary>
    public static readonly Assembly Empty = DeclaringAggregates("EmptyOnly");

    /// <summary>
    /// Two streamed aggregates declared under one namespace and nothing else: a list with nothing
    /// to fold by. The test assembly itself cannot be that list, because its own samples sit under
    /// two namespaces.
    /// </summary>
    public static readonly Assembly OneNamespace = DeclaringAggregates("OneNamespace", "FirstAggregate", "SecondAggregate");

    /// <summary>
    /// Two events and one streamed aggregate that applies only the first: what a domain looks like
    /// when an event is bound and written but no model of the service folds it. The service
    /// registers two events and its model applies one.
    /// </summary>
    public static readonly Assembly PartlyApplied = DeclaringPartlyApplied("PartlyApplied");

    /// <summary>
    /// An assembly declaring one event under the given binding name and nothing else: what two
    /// services that both claim <c>ProductCreated:1</c> look like, each in an assembly of its own.
    /// A record with no state, so a stored payload of <c>{}</c> opens into it.
    /// </summary>
    public static Assembly DeclaringEvent(string assemblyName, string eventName)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule(assemblyName);

        DefineEvent(module, $"{assemblyName}.{eventName}", eventName);
        return assembly;
    }

    private static Type DefineEvent(ModuleBuilder module, string fullName, string eventName)
    {
        var type = module.DefineType(fullName, TypeAttributes.Public | TypeAttributes.Class, typeof(object), [typeof(IEvent)]);

        type.DefineDefaultConstructor(MethodAttributes.Public);
        type.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(EventType).GetConstructor([typeof(string), typeof(byte)])!, [eventName, (byte)1]));

        return type.CreateType();
    }

    private static Assembly DeclaringPartlyApplied(string assemblyName)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule(assemblyName);

        var applied = DefineEvent(module, $"{assemblyName}.Applied", "Applied");
        DefineEvent(module, $"{assemblyName}.Loose", "Loose");
        DefineAggregate(module, $"{assemblyName}.Folding", applies: applied);

        return assembly;
    }

    private static Assembly DeclaringAggregates(string assemblyName, params string[] typeNames)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule(assemblyName);

        foreach (var typeName in typeNames)
        {
            DefineAggregate(module, $"{assemblyName}.{typeName}", applies: null);
        }

        return assembly;
    }

    /// <summary>
    /// A streamed aggregate answering the two members the base leaves abstract: a filter naming
    /// the one event given, or none as the samples answer, and no event applied.
    /// </summary>
    private static void DefineAggregate(ModuleBuilder module, string fullName, Type? applies)
    {
        var type = module.DefineType(fullName, TypeAttributes.Public | TypeAttributes.Class, typeof(AggregateRoot));

        var filter = type.DefineMethod(
            "get_EventTypeFilter",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            typeof(Type[]),
            Type.EmptyTypes);
        var filterIl = filter.GetILGenerator();
        if (applies is null)
        {
            filterIl.Emit(OpCodes.Ldnull);
        }
        else
        {
            // new[] { typeof(applies) }
            filterIl.Emit(OpCodes.Ldc_I4_1);
            filterIl.Emit(OpCodes.Newarr, typeof(Type));
            filterIl.Emit(OpCodes.Dup);
            filterIl.Emit(OpCodes.Ldc_I4_0);
            filterIl.Emit(OpCodes.Ldtoken, applies);
            filterIl.Emit(OpCodes.Call, typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!);
            filterIl.Emit(OpCodes.Stelem_Ref);
        }

        filterIl.Emit(OpCodes.Ret);
        type.DefineMethodOverride(filter, typeof(EventSourcedModel).GetProperty(nameof(EventSourcedModel.EventTypeFilter))!.GetGetMethod()!);
        type.DefineProperty(nameof(EventSourcedModel.EventTypeFilter), PropertyAttributes.None, typeof(Type[]), null).SetGetMethod(filter);

        var apply = type.DefineMethod(
            "Apply",
            MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig,
            typeof(bool),
            Type.EmptyTypes);
        var eventType = apply.DefineGenericParameters("T")[0];
        eventType.SetInterfaceConstraints(typeof(IEvent));
        apply.SetParameters(eventType);
        var applyIl = apply.GetILGenerator();
        applyIl.Emit(OpCodes.Ldc_I4_0);
        applyIl.Emit(OpCodes.Ret);
        type.DefineMethodOverride(
            apply,
            typeof(EventSourcedModel)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(method => method.Name == "Apply" && method.IsGenericMethodDefinition));

        type.CreateType();
    }

    private static Assembly Declaring(string assemblyName, string typeName, Type contract)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule(assemblyName);
        var type = module.DefineType(
            $"{assemblyName}.{typeName}", TypeAttributes.Public | TypeAttributes.Class, typeof(object), [contract]);

        foreach (var property in contract.GetProperties())
        {
            var getter = type.DefineMethod(
                $"get_{property.Name}",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                property.PropertyType,
                Type.EmptyTypes);

            var il = getter.GetILGenerator();
            if (property.PropertyType == typeof(string))
            {
                il.Emit(OpCodes.Ldstr, "only");
            }
            else
            {
                il.Emit(OpCodes.Ldnull);
            }

            il.Emit(OpCodes.Ret);

            type.DefineMethodOverride(getter, property.GetGetMethod()!);
            type.DefineProperty(property.Name, PropertyAttributes.None, property.PropertyType, null).SetGetMethod(getter);
        }

        type.CreateType();
        return assembly;
    }
}
