using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Tools.Rulebook
{
    static class Params
    {
        public static string String(JObject p, string key, string fallback = null)
        {
            var token = p[key];
            if (token == null) return fallback ?? throw new ArgumentException($"param '{key}' is required");
            return token.Value<string>();
        }

        public static bool Bool(JObject p, string key, bool fallback) => p[key]?.Value<bool>() ?? fallback;

        public static string[] Strings(JObject p, string key, bool required = true)
        {
            var token = p[key];
            if (token == null)
                return required ? throw new ArgumentException($"param '{key}' is required") : Array.Empty<string>();
            return token.Type == JTokenType.Array ? token.Values<string>().ToArray() : new[] { token.Value<string>() };
        }

        public static JObject Object(JObject p, string key)
            => p[key] as JObject ?? throw new ArgumentException($"param '{key}' must be an object");

        public static Dictionary<string, string[]> Table(JObject p, string key)
            => Object(p, key).Properties().ToDictionary(prop => prop.Name, prop => prop.Value.Values<string>().ToArray());
    }
}
