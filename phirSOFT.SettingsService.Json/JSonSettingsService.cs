using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace phirSOFT.SettingsService.Json
{
    public class JsonSettingsService : CachedSettingsService
    {
        private readonly IDictionary<string, object> _defaultValues;
        private readonly string _filename;
        private readonly IDictionary<string, Type> _types;

        private readonly IDictionary<string, object> _values;

        private JsonSettingsService(string filename)
        {
            _filename = filename;
            _values = new ConcurrentDictionary<string, object>();
            _types = new ConcurrentDictionary<string, Type>();
            _defaultValues = new ConcurrentDictionary<string, object>();
        }

        protected override bool SupportConcurrentRegister => false;
        protected override bool SupportConcurrentUnregister => true;
        protected override bool SupportConcurrentUpdate => false;

        public static async Task<JsonSettingsService> Create(string filename)
        {
            var service = new JsonSettingsService(filename);
            await service.Initialize().ConfigureAwait(false);
            return service;
        }

        protected override Task<object> GetSettingInternalAsync(string key, Type type)
        {
            return RunSynchronous(() => GetSettingInternal(key, type));
        }

        protected override Task<bool> IsRegisteredInternalAsync(string key)
        {
            return Task.FromResult(_values.ContainsKey(key));
        }

        protected override Task RegisterSettingInternalAsync(
            string key,
            object defaultValue,
            object initialValue,
            Type type)
        {
            return RunSynchronous(() => RegisterSettingsInternal(key, defaultValue, initialValue, type));
        }

        protected override Task SetSettingInternalAsync(string key, object value, Type type)
        {
            return RunSynchronous(() => SetSettingsInternal(key, value, type));
        }

        protected override async Task StoreInternalAsync()
        {
            var serializer = new JsonSerializer();
            using (var fs = new StreamWriter(new FileStream(_filename, FileMode.Create, FileAccess.ReadWrite)))
            using (var writer = new JsonTextWriter(fs) {Formatting = Formatting.Indented})
            {
                await writer.WriteStartObjectAsync().ConfigureAwait(false);

                await WriteDictionaryAsync(writer, serializer, _types, "types", key => typeof(Type))
                    .ConfigureAwait(false);
                await WriteDictionaryAsync(writer, serializer, _values, "values", key => _types[key])
                    .ConfigureAwait(false);
                await WriteDictionaryAsync(writer, serializer, _defaultValues, "defaults", key => _types[key])
                    .ConfigureAwait(false);

                await writer.WriteEndObjectAsync().ConfigureAwait(false);
            }
        }

        protected override Task UnregisterSettingInternalAsync(string key)
        {
            return RunSynchronous(() => UnregisterSettingInternal(key));
        }

        private object GetSettingInternal(string key, Type type)
        {
            if (type != _types[key])
                throw new ArgumentException(
                    $"Type mismatch. The requested type was {type}, but the stored value was of type {_types[key]}");

            return _values[key];
        }

        private async Task Initialize()
        {
            if (!File.Exists(_filename))
                return;

            using (var fs = new StreamReader(new FileStream(_filename, FileMode.Open, FileAccess.Read)))
            using (var reader = new JsonTextReader(fs))
            {
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    if (reader.TokenType != JsonToken.PropertyName) continue;

                    switch ((string) reader.Value)
                    {
                        case "types":
                            await ReadDictionary(reader, _types, key => typeof(Type));
                            break;
                        case "values":
                            await ReadDictionary(reader, _values, key => _types[key]);
                            break;
                        case "defaults":
                            await ReadDictionary(reader, _defaultValues, key => _types[key]);
                            break;
                        default:
                            throw new JsonSerializationException($"Unknown entry : {(string) reader.Value}");
                    }
                }
            }
        }

        private static async Task ReadDictionary<T>(
            JsonReader reader,
            IDictionary<string, T> dictionary,
            Func<string, Type> typeResolver)
        {
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                if (reader.TokenType == JsonToken.EndObject)
                    return;

                if (reader.TokenType != JsonToken.PropertyName)
                    continue;

                var key = (string) reader.Value;

                if (!await reader.ReadAsync().ConfigureAwait(false))
                    throw new JsonSerializationException();

                JToken value = await JToken.ReadFromAsync(reader);

                dictionary.Add(key, (T) value.ToObject(typeResolver(key)));
            }
        }

        private void RegisterSettingsInternal(string key, object defaultValue, object initialValue, Type type)
        {
            _values.Add(key, initialValue);
            _defaultValues.Add(key, defaultValue);
            _types.Add(key, type);
        }

        private static Task RunSynchronous(Action action)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception e)
            {
                return Task.FromException(e);
            }

            return Task.CompletedTask;
        }

        private static Task<T> RunSynchronous<T>(Func<T> action)
        {
            try
            {
                return Task.FromResult(action.Invoke());
            }
            catch (Exception e)
            {
                return Task.FromException<T>(e);
            }
        }

        private void SetSettingsInternal(string key, object value, Type type)
        {
            if (!value.GetType().GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
                throw new ArgumentException();
            if (type != _types[key])
                throw new ArgumentException();

            _values[key] = value;
        }

        private void UnregisterSettingInternal(string key)
        {
            _values.Remove(key);
            _types.Remove(key);
            _defaultValues.Remove(key);
        }

        private static async Task WriteDictionaryAsync<T>(
            JsonWriter writer,
            JsonSerializer serializer,
            IDictionary<string, T> dictionary,
            string key,
            Func<string, Type> typeResolver)
        {
            await writer.WritePropertyNameAsync(key).ConfigureAwait(false);
            await writer.WriteStartObjectAsync().ConfigureAwait(false);

            foreach (KeyValuePair<string, T> value in dictionary)
            {
                await writer.WritePropertyNameAsync(value.Key);
                serializer.Serialize(writer, value.Value, typeResolver(value.Key));
            }

            writer.WriteEndObject();
        }
    }
}
