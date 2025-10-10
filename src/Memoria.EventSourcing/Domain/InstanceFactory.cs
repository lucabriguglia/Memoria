using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace Memoria.EventSourcing.Domain;

public static class InstanceFactory
{
    private static readonly ConcurrentDictionary<Type, Func<object>> FactoryCache = new();

    public static object CreateInstance(Type type)
    {
        var factory = FactoryCache.GetOrAdd(type, CreateFactory);
        return factory();
    }

    public static T CreateInstance<T>()
    {
        var factory = FactoryCache.GetOrAdd(typeof(T), CreateFactory);
        return (T)factory();
    }

    public static T CreateInstance<T>(Type type)
    {
        var factory = FactoryCache.GetOrAdd(type, CreateFactory);
        return (T)factory();
    }

    private static Func<object> CreateFactory(Type type)
    {
        var constructor = type.GetConstructor(Type.EmptyTypes);
        if (constructor == null)
        {
            throw new InvalidOperationException($"Type {type.Name} must have a parameterless constructor");
        }

        var newExpression = Expression.New(constructor);
        var lambda = Expression.Lambda<Func<object>>(newExpression);
        return lambda.Compile();
    }
}
