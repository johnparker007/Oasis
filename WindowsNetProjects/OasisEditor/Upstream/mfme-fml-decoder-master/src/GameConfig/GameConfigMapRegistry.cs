using System;
using System.Collections.Generic;

namespace MfmeFmlDecoder.GameConfig
{
    /// <summary>
    /// In-assembly GameConfig maps keyed by GAM System name.
    /// </summary>
    internal static class GameConfigMapRegistry
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, Func<GameConfigMap>> Factories =
            new Dictionary<string, Func<GameConfigMap>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, GameConfigMap> Cache =
            new Dictionary<string, GameConfigMap>(StringComparer.Ordinal);

        static GameConfigMapRegistry()
        {
            GameConfigMapRegistration.RegisterAll();
        }

        public static void Register(string system, Func<GameConfigMap> factory)
        {
            if (string.IsNullOrWhiteSpace(system))
                throw new ArgumentException("System name required.", nameof(system));
            if (factory is null)
                throw new ArgumentNullException(nameof(factory));

            Factories[system] = factory;
        }

        public static bool TryGet(string system, out GameConfigMap map, out string error)
        {
            map = null;
            error = null;
            if (string.IsNullOrWhiteSpace(system))
            {
                error = "GAM has no System line.";
                return false;
            }

            if (!TryResolveFactory(system, out string canonical, out Func<GameConfigMap> factory))
            {
                error = $"No GameConfig map for system '{system}'.";
                return false;
            }

            lock (Gate)
            {
                if (Cache.TryGetValue(canonical, out map))
                    return true;

                map = factory();
                Cache[canonical] = map;
                return true;
            }
        }

        private static bool TryResolveFactory(
            string system,
            out string canonical,
            out Func<GameConfigMap> factory)
        {
            if (Factories.TryGetValue(system, out factory))
            {
                canonical = system;
                return true;
            }

            foreach (KeyValuePair<string, Func<GameConfigMap>> entry in Factories)
            {
                if (string.Equals(entry.Key, system, StringComparison.OrdinalIgnoreCase))
                {
                    canonical = entry.Key;
                    factory = entry.Value;
                    return true;
                }
            }

            canonical = null;
            factory = null;
            return false;
        }
    }
}
