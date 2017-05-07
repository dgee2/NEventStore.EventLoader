using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyModel;

namespace NEventStore.EventLoader
{
    public class EventLoader
    {
        private readonly IDictionary<Tuple<Type, Type>, Tuple<Type, MethodInfo, dynamic>> _knownReflections;

        public EventLoader(Func<AssemblyName, bool> assembliesFunc, Func<Type,bool> typeFunc = null)
        {
            var type = typeof(ICanProcessEvent<,>).GetGenericTypeDefinition();
            var types = DependencyContext.Default.GetDefaultAssemblyNames()
                .Where(assembliesFunc)
                .Select(Assembly.Load)
                .SelectMany(a => a.GetExportedTypes())
                .Where(x => x.GetInterfaces()
                    .Any(y => y.GetTypeInfo().IsGenericType && type.IsAssignableFrom(y.GetGenericTypeDefinition())));

            if (typeFunc != null)
                types = types.Where(typeFunc);

            _knownReflections = types.SelectMany(x => x.GetInterfaces().Where(y => y.Name == "ICanProcessEvent`2")
                .Select(z => new KeyValuePair<Tuple<Type, Type>, Tuple<Type, MethodInfo, dynamic>>(
                    new Tuple<Type, Type>(z.GenericTypeArguments[0], z.GenericTypeArguments[1]),
                    new Tuple<Type, MethodInfo, dynamic>(x, x.GetMethod("ProcessEvent", z.GenericTypeArguments), null)
                ))
            ).ToDictionary(x => x.Key, x => x.Value);
        }

        public T ProcessEvents<T>(IEnumerable<EventMessage> events)
        {
            var dataType = typeof(T);
            var data = default(T);
            foreach (var eventData in events)
            {
                var @event = eventData.Body;
                var eventType = @event.GetType();
                var key = new Tuple<Type, Type>(dataType, eventType);
                if (_knownReflections.ContainsKey(key))
                {
                    var instance = _knownReflections[key].Item1.GetConstructors()[0].Invoke(new object[0]);
                    data = (T)_knownReflections[key].Item2.Invoke(instance, new[] { data, @event });
                }
            }
            return data;
        }

        public T ProcessEvents<T>(IStoreEvents store, Guid id)
        {
            using (var stream = store.OpenStream(id))
            {
                return ProcessEvents<T>(stream.CommittedEvents);
            }
        }
    }
}
