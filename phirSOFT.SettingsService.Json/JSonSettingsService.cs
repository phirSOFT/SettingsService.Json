// <copyright file="JsonSettingsService.cs" company="phirSOFT">
// Copyright (c) phirSOFT. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using phirSOFT.SettingsService.Abstractions;

namespace phirSOFT.SettingsService.Json
{
    /// <summary>
    ///     Implements a <see cref="ISettingsService"/> for settings stored in json file.
    /// </summary>
    public sealed class JsonSettingsService : CachedSettingsService
    {
        private readonly IDictionary<string, object> _defaultValues;
        private readonly string _filename;
        private readonly IDictionary<string, Type> _types;

        private readonly IDictionary<string, object> _values;

        /// <summary>
        ///     Initializes a new instance of the <see cref="JsonSettingsService"/> class.
        /// </summary>
        /// <param name="filename">the filename.</param>
        private JsonSettingsService(string filename)
        {
            _filename = filename;
            _values = new ConcurrentDictionary<string, object>();
            _types = new ConcurrentDictionary<string, Type>();
            _defaultValues = new ConcurrentDictionary<string, object>();
        }

        /// <inheritdoc/>
        protected override bool SupportConcurrentRegister => false;

        /// <inheritdoc/>
        protected override bool SupportConcurrentUnregister => true;

        /// <inheritdoc/>
        protected override bool SupportConcurrentUpdate => false;

        /// <summary>
        ///     Creates a new <see cref="JsonSettingsService"/> asynchronously
        /// </summary>
        /// <param name="filename">the name of the settings file to open or create.</param>
        /// <returns>
        ///     A <see cref="Task"/>, that represents the asynchronous create operation and yields
        ///     <see cref="JsonSettingsService"/>
        /// </returns>
        public static async Task<JsonSettingsService> CreateAsync(string filename)
        {
            var service = new JsonSettingsService(filename);
            await service.Initialize().ConfigureAwait(false);
            return service;
        }

        /// <inheritdoc/>
        protected override Task<object> GetSettingInternalAsync(string key, Type type)
        {
            return RunSynchronous(() => GetSettingInternal(key, type));
        }

        /// <inheritdoc/>
        protected override Task<bool> IsRegisteredInternalAsync(string key)
        {
            return Task.FromResult(_values.ContainsKey(key));
        }

        /// <inheritdoc/>
        protected override Task RegisterSettingInternalAsync(
            string key,
            object defaultValue,
            object initialValue,
            Type type)
        {
            return RunSynchronous(() => RegisterSettingsInternal(key, defaultValue, initialValue, type));
        }

        /// <inheritdoc/>
        protected override Task SetSettingInternalAsync(string key, object value, Type type)
        {
            return RunSynchronous(() => SetSettingsInternal(key, value, type));
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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
                            await ReadDictionary(reader, _types, key => typeof(Type)).ConfigureAwait(false);
                            break;
                        case "values":
                            await ReadDictionary(reader, _values, key => _types[key]).ConfigureAwait(false);
                            break;
                        case "defaults":
                            await ReadDictionary(reader, _defaultValues, key => _types[key]).ConfigureAwait(false);
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

                JToken value = await JToken.ReadFromAsync(reader).ConfigureAwait(false);

                dictionary.Add(key, (T) value.ToObject(typeResolver(key)));
            }
        }

        private void RegisterSettingsInternal(string key, object defaultValue, object initialValue, Type type)
        {
            _values.Add(key, initialValue);
            _defaultValues.Add(key, defaultValue);
            _types.Add(key, type);
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "We rethrow the exception, by returning a failed tas.")]
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
#pragma warning disable CA1031 // Do not catch general exception types

            catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                return Task.FromException<T>(e);
            }
        }

        private void SetSettingsInternal(string key, object value, Type type)
        {
            if (!value.GetType().GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
                throw new ArgumentException(
                    $"Value of type {value.GetType().FullName} did not match expected type {type.FullName}. ",
                    nameof(value));
            if (type != _types[key])
                throw new ArgumentException(
                    $"{type.FullName} was different, than stored property setting type ({_types[key].FullName})");

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
                await writer.WritePropertyNameAsync(value.Key).ConfigureAwait(false);
                serializer.Serialize(writer, value.Value, typeResolver(value.Key));
            }

            writer.WriteEndObject();
        }
    }
}
