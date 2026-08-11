using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace Game.Expressions;

public readonly record struct ExpressionTypeDescriptor(
    ExpressionValueKind? Kind,
    ExpressionValueKind? ListElementKind = null)
{
    public static ExpressionTypeDescriptor Any => new(null);
}

public sealed record ExpressionParameter(
    string Name,
    ExpressionTypeDescriptor Type,
    bool IsOptional = false,
    ExpressionValue DefaultValue = default,
    bool IsVariadic = false)
{
    public ExpressionValueKind? Kind => Type.Kind;
    public ExpressionValueKind? ListElementKind => Type.ListElementKind;
}

public sealed record ExpressionCallDescriptor(
    string Name,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<ExpressionParameter> Parameters,
    ExpressionTypeDescriptor ReturnType)
{
    public ExpressionValueKind? ReturnKind => ReturnType.Kind;
}

public interface IExpressionCallCatalog
{
    bool TryGetDescriptor(string name, out ExpressionCallDescriptor descriptor);
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public abstract class ExpressionSymbolAttribute : Attribute
{
    protected ExpressionSymbolAttribute(string name, params string[] aliases)
    {
        ExpressionSymbol.Validate(name);
        ArgumentNullException.ThrowIfNull(aliases);
        var names = new HashSet<string>(StringComparer.Ordinal) { name };
        foreach (var alias in aliases)
        {
            ExpressionSymbol.Validate(alias);
            if (!names.Add(alias))
            {
                throw new ArgumentException($"Expression symbol '{alias}' is declared more than once.", nameof(aliases));
            }
        }

        Name = name;
        Aliases = aliases.ToArray();
    }

    public string Name { get; }
    public IReadOnlyList<string> Aliases { get; }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ExpressionFunctionAttribute : ExpressionSymbolAttribute
{
    public ExpressionFunctionAttribute(string name, params string[] aliases) : base(name, aliases) { }
}

public static class ExpressionSymbol
{
    public static bool IsIdentifierStart(char character)
    {
        if (character == '_')
        {
            return true;
        }

        return character is >= 'a' and <= 'z' or
            '\u3007' or
            >= '\u3400' and <= '\u4dbf' or
            >= '\u4e00' and <= '\u9fff' or
            >= '\uf900' and <= '\ufaff';
    }

    public static bool IsIdentifierPart(char character) =>
        IsIdentifierStart(character) || char.IsDigit(character);

    public static void Validate(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!IsIdentifierStart(name[0]) || name.Skip(1).Any(character => !IsIdentifierPart(character)))
        {
            throw new ArgumentException($"Invalid expression symbol name '{name}'.", nameof(name));
        }
    }
}

public sealed class ExpressionFunctionRegistry : IExpressionCallCatalog
{
    private readonly Dictionary<string, Entry> _entries;

    internal ExpressionFunctionRegistry(Dictionary<string, Entry> entries) => _entries = entries;

    public bool TryGetDescriptor(string name, out ExpressionCallDescriptor descriptor) =>
        RegistryLookup.TryGetDescriptor(_entries, name, out descriptor);

    public ExpressionValue Invoke(string name, IReadOnlyList<ExpressionValue> arguments)
    {
        if (!_entries.TryGetValue(name, out var entry))
        {
            throw new ExpressionBindingException($"Unknown expression function '{name}'.");
        }

        return entry.Handler(arguments);
    }

    internal sealed record Entry(
        ExpressionCallDescriptor Descriptor,
        Func<IReadOnlyList<ExpressionValue>, ExpressionValue> Handler) : IRegistryEntry;
}

public sealed class ExpressionFunctionRegistryBuilder
{
    private readonly Dictionary<string, ExpressionFunctionRegistry.Entry> _entries = new(StringComparer.Ordinal);

    public ExpressionFunctionRegistryBuilder AddLibrary(object library)
    {
        ArgumentNullException.ThrowIfNull(library);
        foreach (var method in ExpressionLibraryScanner.FindMethods<ExpressionFunctionAttribute>(library))
        {
            var descriptor = ExpressionLibraryScanner.CreateDescriptor(method.Attribute, method.Method, includeReturnType: true, allowCancellationToken: false);
            ExpressionLibraryScanner.ValidateFunctionReturn(method.Method);
            var entry = new ExpressionFunctionRegistry.Entry(
                descriptor,
                arguments => ExpressionClrBinder.InvokeFunction(method.Target, method.Method, descriptor, arguments));
            RegistryLookup.AddEntry(_entries, descriptor, entry, "Expression function");
        }

        return this;
    }

    public ExpressionFunctionRegistry Build() =>
        new(new Dictionary<string, ExpressionFunctionRegistry.Entry>(_entries, StringComparer.Ordinal));
}

public sealed class ExpressionCallRegistry<TResult> : IExpressionCallCatalog
{
    private readonly Dictionary<string, Entry> _entries;

    internal ExpressionCallRegistry(Dictionary<string, Entry> entries) => _entries = entries;

    public bool TryGetDescriptor(string name, out ExpressionCallDescriptor descriptor) =>
        RegistryLookup.TryGetDescriptor(_entries, name, out descriptor);

    public TResult Invoke(string name, IReadOnlyList<ExpressionValue> arguments)
    {
        if (!_entries.TryGetValue(name, out var entry))
        {
            throw new ExpressionBindingException($"Unknown call '{name}'.");
        }

        return entry.Handler(arguments);
    }

    internal sealed record Entry(ExpressionCallDescriptor Descriptor, Func<IReadOnlyList<ExpressionValue>, TResult> Handler) : IRegistryEntry;
}

public sealed class ExpressionCallRegistryBuilder<TResult>
{
    private readonly Dictionary<string, ExpressionCallRegistry<TResult>.Entry> _entries = new(StringComparer.Ordinal);

    public ExpressionCallRegistryBuilder<TResult> AddLibrary<TAttribute>(object library)
        where TAttribute : ExpressionSymbolAttribute
    {
        ArgumentNullException.ThrowIfNull(library);
        foreach (var method in ExpressionLibraryScanner.FindMethods<TAttribute>(library))
        {
            var descriptor = ExpressionLibraryScanner.CreateDescriptor(method.Attribute, method.Method, includeReturnType: false, allowCancellationToken: false);
            ExpressionLibraryScanner.ValidateSynchronousCallReturn<TResult>(method.Method);
            var entry = new ExpressionCallRegistry<TResult>.Entry(
                descriptor,
                arguments => ExpressionClrBinder.InvokeCall<TResult>(method.Target, method.Method, descriptor, arguments));
            RegistryLookup.AddEntry(_entries, descriptor, entry, "Call");
        }

        return this;
    }

    public ExpressionCallRegistry<TResult> Build() =>
        new(new Dictionary<string, ExpressionCallRegistry<TResult>.Entry>(_entries, StringComparer.Ordinal));
}

public sealed class AsyncExpressionCallRegistry<TResult> : IExpressionCallCatalog
{
    private readonly Dictionary<string, Entry> _entries;

    internal AsyncExpressionCallRegistry(Dictionary<string, Entry> entries) => _entries = entries;

    public bool TryGetDescriptor(string name, out ExpressionCallDescriptor descriptor) =>
        RegistryLookup.TryGetDescriptor(_entries, name, out descriptor);

    public ValueTask<TResult> InvokeAsync(
        string name,
        IReadOnlyList<ExpressionValue> arguments,
        CancellationToken cancellationToken = default)
    {
        if (!_entries.TryGetValue(name, out var entry))
        {
            return ValueTask.FromException<TResult>(new ExpressionBindingException($"Unknown async call '{name}'."));
        }

        try
        {
            return entry.Handler(arguments, cancellationToken);
        }
        catch (Exception exception)
        {
            return ValueTask.FromException<TResult>(exception);
        }
    }

    internal sealed record Entry(
        ExpressionCallDescriptor Descriptor,
        Func<IReadOnlyList<ExpressionValue>, CancellationToken, ValueTask<TResult>> Handler) : IRegistryEntry;
}

public sealed class AsyncExpressionCallRegistryBuilder<TResult>
{
    private readonly TResult _defaultResult;
    private readonly Dictionary<string, AsyncExpressionCallRegistry<TResult>.Entry> _entries = new(StringComparer.Ordinal);

    public AsyncExpressionCallRegistryBuilder(TResult defaultResult) => _defaultResult = defaultResult;

    public AsyncExpressionCallRegistryBuilder<TResult> AddLibrary<TAttribute>(object library)
        where TAttribute : ExpressionSymbolAttribute
    {
        ArgumentNullException.ThrowIfNull(library);
        foreach (var method in ExpressionLibraryScanner.FindMethods<TAttribute>(library))
        {
            var descriptor = ExpressionLibraryScanner.CreateDescriptor(method.Attribute, method.Method, includeReturnType: false, allowCancellationToken: true);
            ExpressionLibraryScanner.ValidateAsyncCallReturn<TResult>(method.Method);
            var entry = new AsyncExpressionCallRegistry<TResult>.Entry(
                descriptor,
                (arguments, cancellationToken) => ExpressionClrBinder.InvokeAsyncCall(
                    method.Target, method.Method, descriptor, arguments, cancellationToken, _defaultResult));
            RegistryLookup.AddEntry(_entries, descriptor, entry, "Async call");
        }

        return this;
    }

    public AsyncExpressionCallRegistry<TResult> Build() =>
        new(new Dictionary<string, AsyncExpressionCallRegistry<TResult>.Entry>(_entries, StringComparer.Ordinal));
}

internal interface IRegistryEntry
{
    ExpressionCallDescriptor Descriptor { get; }
}

internal static class RegistryLookup
{
    public static bool TryGetDescriptor<TEntry>(
        Dictionary<string, TEntry> entries,
        string name,
        out ExpressionCallDescriptor descriptor)
        where TEntry : IRegistryEntry
    {
        if (entries.TryGetValue(name, out var entry))
        {
            descriptor = entry.Descriptor;
            return true;
        }

        descriptor = null!;
        return false;
    }

    public static void AddEntry<TEntry>(
        Dictionary<string, TEntry> entries,
        ExpressionCallDescriptor descriptor,
        TEntry entry,
        string category)
    {
        foreach (var name in new[] { descriptor.Name }.Concat(descriptor.Aliases))
        {
            if (entries.ContainsKey(name))
            {
                throw new InvalidOperationException($"{category} symbol '{name}' is registered more than once.");
            }
        }

        entries.Add(descriptor.Name, entry);
        foreach (var alias in descriptor.Aliases)
        {
            entries.Add(alias, entry);
        }
    }
}

internal static class ExpressionLibraryScanner
{
    public static IEnumerable<ScannedMethod<TAttribute>> FindMethods<TAttribute>(object library)
        where TAttribute : ExpressionSymbolAttribute
    {
        return library.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .OrderBy(static method => method.MetadataToken)
            .Select(method => new { Method = method, Attribute = method.GetCustomAttribute<TAttribute>(inherit: false) })
            .Where(static item => item.Attribute is not null)
            .Select(item => new ScannedMethod<TAttribute>(item.Method.IsStatic ? null : library, item.Method, item.Attribute!));
    }

    public static ExpressionCallDescriptor CreateDescriptor(
        ExpressionSymbolAttribute attribute,
        MethodInfo method,
        bool includeReturnType,
        bool allowCancellationToken)
    {
        if (method.ContainsGenericParameters)
        {
            throw SignatureError(attribute.Name, method, "generic methods are not supported");
        }

        var parameters = new List<ExpressionParameter>();
        var methodParameters = method.GetParameters();
        for (var index = 0; index < methodParameters.Length; index++)
        {
            var parameter = methodParameters[index];
            if (parameter.ParameterType == typeof(CancellationToken))
            {
                if (!allowCancellationToken || index != methodParameters.Length - 1)
                {
                    throw SignatureError(attribute.Name, method, "CancellationToken is only allowed as the final parameter of an async call");
                }

                continue;
            }

            ValidateNonNullable(attribute.Name, method, parameter);
            var isVariadic = parameter.GetCustomAttribute<ParamArrayAttribute>() is not null;
            if (isVariadic && (index != methodParameters.Length - 1 || !parameter.ParameterType.IsArray))
            {
                throw SignatureError(attribute.Name, method, "a params array must be the final parameter");
            }

            var type = isVariadic
                ? ExpressionClrTypes.Describe(parameter.ParameterType.GetElementType()!, list: false)
                : ExpressionClrTypes.Describe(parameter.ParameterType, list: true);
            var optional = parameter.HasDefaultValue;
            var defaultValue = optional && parameter.DefaultValue is not null
                ? ExpressionClrTypes.FromClrDefault(parameter.DefaultValue, parameter.ParameterType, attribute.Name, parameter.Name)
                : default;
            parameters.Add(new ExpressionParameter(
                parameter.Name ?? $"arg{index}",
                type,
                optional,
                defaultValue,
                isVariadic));
        }

        var returnType = includeReturnType
            ? ExpressionClrTypes.Describe(method.ReturnType, list: true)
            : ExpressionTypeDescriptor.Any;
        return new ExpressionCallDescriptor(attribute.Name, attribute.Aliases.ToArray(), parameters, returnType);
    }

    public static void ValidateFunctionReturn(MethodInfo method)
    {
        ValidateReturnNullability(method);
        if (method.ReturnType == typeof(void) || typeof(Task).IsAssignableFrom(method.ReturnType) || method.ReturnType == typeof(ValueTask))
        {
            throw SignatureError(method.Name, method, "expression functions must return a synchronous expression value");
        }
    }

    public static void ValidateSynchronousCallReturn<TResult>(MethodInfo method)
    {
        ValidateReturnNullability(method);
        if (!typeof(TResult).IsAssignableFrom(method.ReturnType))
        {
            throw SignatureError(method.Name, method, $"the return type must be assignable to {typeof(TResult).Name}");
        }
    }

    public static void ValidateAsyncCallReturn<TResult>(MethodInfo method)
    {
        ValidateReturnNullability(method);
        var returnType = method.ReturnType;
        var valid = returnType == typeof(void) || returnType == typeof(TResult) ||
            returnType == typeof(Task) || returnType == typeof(ValueTask) ||
            returnType == typeof(Task<TResult>) || returnType == typeof(ValueTask<TResult>);
        if (!valid)
        {
            throw SignatureError(method.Name, method,
                $"the return type must be void, {typeof(TResult).Name}, Task, ValueTask, Task<{typeof(TResult).Name}> or ValueTask<{typeof(TResult).Name}>");
        }

        if (returnType == typeof(void) && method.GetCustomAttribute<AsyncStateMachineAttribute>() is not null)
        {
            throw SignatureError(method.Name, method, "async void is not supported");
        }
    }

    private static void ValidateNonNullable(string symbol, MethodInfo method, ParameterInfo parameter)
    {
        var isNullable = Nullable.GetUnderlyingType(parameter.ParameterType) is not null ||
            !parameter.ParameterType.IsValueType && new NullabilityInfoContext().Create(parameter).ReadState == NullabilityState.Nullable;
        var isOmittedNullDefault = parameter.HasDefaultValue && parameter.DefaultValue is null && !parameter.ParameterType.IsValueType;
        if (isNullable && !isOmittedNullDefault)
        {
            throw SignatureError(symbol, method, $"parameter '{parameter.Name}' cannot be nullable");
        }
    }

    private static void ValidateReturnNullability(MethodInfo method)
    {
        if (!method.ReturnType.IsValueType && method.ReturnType != typeof(void) &&
            new NullabilityInfoContext().Create(method.ReturnParameter).ReadState == NullabilityState.Nullable)
        {
            throw SignatureError(method.Name, method, "the return type cannot be nullable");
        }
    }

    private static InvalidOperationException SignatureError(string symbol, MethodInfo method, string message) =>
        new($"Expression symbol '{symbol}' on '{method.DeclaringType?.FullName}.{method.Name}' has an invalid signature: {message}.");

    internal sealed record ScannedMethod<TAttribute>(object? Target, MethodInfo Method, TAttribute Attribute)
        where TAttribute : ExpressionSymbolAttribute;
}

internal static class ExpressionClrTypes
{
    public static ExpressionTypeDescriptor Describe(Type clrType, bool list)
    {
        if (clrType == typeof(ExpressionValue)) return ExpressionTypeDescriptor.Any;
        if (clrType == typeof(bool)) return new(ExpressionValueKind.Boolean);
        if (clrType == typeof(int) || clrType == typeof(double)) return new(ExpressionValueKind.Number);
        if (clrType == typeof(string)) return new(ExpressionValueKind.String);
        if (list && TryGetListElementType(clrType, out var elementType))
        {
            var element = Describe(elementType, list: false);
            return new(ExpressionValueKind.List, element.Kind);
        }

        throw new InvalidOperationException($"CLR type '{clrType.FullName}' is not supported by expression binding.");
    }

    public static object ToClr(ExpressionValue value, Type targetType, string context)
    {
        try
        {
            if (targetType == typeof(ExpressionValue)) return value;
            if (targetType == typeof(bool)) return value.AsBoolean(context);
            if (targetType == typeof(int)) return value.AsInt32(context);
            if (targetType == typeof(double)) return value.AsNumber(context);
            if (targetType == typeof(string)) return value.AsString(context);
            if (TryGetListElementType(targetType, out var elementType))
            {
                var source = value.AsList(context);
                var array = Array.CreateInstance(elementType, source.Count);
                for (var index = 0; index < source.Count; index++)
                {
                    array.SetValue(ToClr(source[index], elementType, $"{context}[{index}]"), index);
                }

                return targetType.IsArray ? array : CreateReadOnlyList(elementType, array);
            }

            throw new ExpressionBindingException($"{context} cannot bind to CLR type '{targetType.Name}'.");
        }
        catch (ExpressionEvaluationException exception)
        {
            throw new ExpressionBindingException(exception.Message, exception);
        }
    }

    public static ExpressionValue FromClr(object? value, Type declaredType, string context)
    {
        if (value is null) throw new ExpressionBindingException($"{context} returned null.");
        if (value is ExpressionValue expressionValue) return expressionValue;
        if (value is bool boolean) return ExpressionValue.FromBoolean(boolean);
        if (value is int integer) return ExpressionValue.FromNumber(integer);
        if (value is double number) return ExpressionValue.FromNumber(number);
        if (value is string text) return ExpressionValue.FromString(text);
        if (TryGetListElementType(declaredType, out var elementType) && value is System.Collections.IEnumerable items)
        {
            var converted = new List<ExpressionValue>();
            foreach (var item in items)
            {
                converted.Add(FromClr(item, elementType, context));
            }

            return ExpressionValue.FromList(converted);
        }

        throw new ExpressionBindingException($"{context} returned unsupported CLR type '{value.GetType().Name}'.");
    }

    public static ExpressionValue FromClrDefault(object? value, Type type, string symbol, string? parameterName)
    {
        if (value is null)
        {
            throw new InvalidOperationException($"Expression symbol '{symbol}' optional parameter '{parameterName}' cannot default to null.");
        }

        return FromClr(value, type, $"Expression symbol '{symbol}' optional parameter '{parameterName}'");
    }

    public static bool TryGetListElementType(Type type, out Type elementType)
    {
        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        if (type.IsGenericType && type.GetGenericArguments().Length == 1 &&
            type.GetGenericTypeDefinition() is var definition &&
            (definition == typeof(IReadOnlyList<>) || definition == typeof(IReadOnlyCollection<>) || definition == typeof(IEnumerable<>)))
        {
            elementType = type.GetGenericArguments()[0];
            return true;
        }

        elementType = null!;
        return false;
    }

    private static object CreateReadOnlyList(Type elementType, Array array) =>
        typeof(Array).GetMethod(nameof(Array.AsReadOnly))!
            .MakeGenericMethod(elementType)
            .Invoke(null, [array])!;
}

internal static class ExpressionClrBinder
{
    public static ExpressionValue InvokeFunction(
        object? target,
        MethodInfo method,
        ExpressionCallDescriptor descriptor,
        IReadOnlyList<ExpressionValue> arguments)
    {
        var result = InvokeRaw(target, method, descriptor, arguments, default);
        return ExpressionClrTypes.FromClr(result, method.ReturnType, $"Function '{descriptor.Name}'");
    }

    public static TResult InvokeCall<TResult>(
        object? target,
        MethodInfo method,
        ExpressionCallDescriptor descriptor,
        IReadOnlyList<ExpressionValue> arguments)
    {
        var result = InvokeRaw(target, method, descriptor, arguments, default);
        return result is TResult typed
            ? typed
            : throw new ExpressionBindingException($"Call '{descriptor.Name}' returned an invalid result.");
    }

    public static async ValueTask<TResult> InvokeAsyncCall<TResult>(
        object? target,
        MethodInfo method,
        ExpressionCallDescriptor descriptor,
        IReadOnlyList<ExpressionValue> arguments,
        CancellationToken cancellationToken,
        TResult defaultResult)
    {
        var result = InvokeRaw(target, method, descriptor, arguments, cancellationToken);
        switch (result)
        {
            case null:
                return defaultResult;
            case TResult typed:
                return typed;
            case Task<TResult> taskOfResult:
                return await taskOfResult.ConfigureAwait(false);
            case ValueTask<TResult> valueTaskOfResult:
                return await valueTaskOfResult.ConfigureAwait(false);
            case Task task:
                await task.ConfigureAwait(false);
                return defaultResult;
            case ValueTask valueTask:
                await valueTask.ConfigureAwait(false);
                return defaultResult;
            default:
                throw new ExpressionBindingException($"Call '{descriptor.Name}' returned an invalid result.");
        }
    }

    private static object? InvokeRaw(
        object? target,
        MethodInfo method,
        ExpressionCallDescriptor descriptor,
        IReadOnlyList<ExpressionValue> arguments,
        CancellationToken cancellationToken)
    {
        var values = BindArguments(method, descriptor, arguments, cancellationToken);
        try
        {
            return method.Invoke(target, values);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static object?[] BindArguments(
        MethodInfo method,
        ExpressionCallDescriptor descriptor,
        IReadOnlyList<ExpressionValue> arguments,
        CancellationToken cancellationToken)
    {
        var required = descriptor.Parameters.Count(parameter => !parameter.IsOptional && !parameter.IsVariadic);
        var variadic = descriptor.Parameters.LastOrDefault()?.IsVariadic == true;
        if (arguments.Count < required || !variadic && arguments.Count > descriptor.Parameters.Count)
        {
            throw new ExpressionBindingException($"Call '{descriptor.Name}' received {arguments.Count} arguments; its signature does not allow that count.");
        }

        var result = new object?[method.GetParameters().Length];
        var expressionIndex = 0;
        var descriptorIndex = 0;
        for (var methodIndex = 0; methodIndex < method.GetParameters().Length; methodIndex++)
        {
            var parameter = method.GetParameters()[methodIndex];
            if (parameter.ParameterType == typeof(CancellationToken))
            {
                result[methodIndex] = cancellationToken;
                continue;
            }

            var expressionParameter = descriptor.Parameters[descriptorIndex++];
            if (expressionParameter.IsVariadic)
            {
                var elementType = parameter.ParameterType.GetElementType()!;
                var remaining = arguments.Count - expressionIndex;
                var array = Array.CreateInstance(elementType, remaining);
                for (var index = 0; index < remaining; index++)
                {
                    array.SetValue(ExpressionClrTypes.ToClr(
                        arguments[expressionIndex++], elementType,
                        $"Call '{descriptor.Name}' argument '{expressionParameter.Name}'"), index);
                }

                result[methodIndex] = array;
                continue;
            }

            if (expressionIndex < arguments.Count)
            {
                result[methodIndex] = ExpressionClrTypes.ToClr(
                    arguments[expressionIndex++], parameter.ParameterType,
                    $"Call '{descriptor.Name}' argument '{expressionParameter.Name}'");
            }
            else
            {
                result[methodIndex] = parameter.DefaultValue;
            }
        }

        return result;
    }
}
