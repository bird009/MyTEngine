using System;
using GameServices;
using Newtonsoft.Json.Linq;

namespace GameLogic
{
    public sealed class AdPolicy
    {
        public bool Enabled { get; }
        public int DailyLimit { get; }
        public double MinIntervalSeconds { get; }
        public string Revision { get; }
        public string Experiment { get; }
        public string Variant { get; }
        public string Source { get; }
        public string Release { get; }
        public bool DisabledByRemote { get; }
        public bool IsExperiment => Experiment == RemoteConfigKeys.Experiment && Enabled;

        public AdPolicy(bool enabled, int dailyLimit, double minIntervalSeconds,
            string revision = "luban", string experiment = "", string variant = "",
            string source = "builtin", string release = "builtin", bool disabledByRemote = false)
        {
            Enabled = enabled;
            DailyLimit = dailyLimit;
            MinIntervalSeconds = minIntervalSeconds;
            Revision = revision; Experiment = experiment; Variant = variant; Source = source; Release = release;
            DisabledByRemote = disabledByRemote;
        }
    }

    public static class AdPolicyResolver
    {
        public static AdPolicy Resolve(AdPolicy baseline, RemoteConfigValues values,
            bool allowExperiment, string source, out string error)
        {
            error = "";
            var fallback = new AdPolicy(baseline.Enabled && !values.InterstitialDisabled,
                baseline.DailyLimit, baseline.MinIntervalSeconds, release: values.Release,
                disabledByRemote: values.InterstitialDisabled);
            if (!values.PolicyIsRemote || string.IsNullOrWhiteSpace(values.PolicyJson)) return fallback;
            try
            {
                if (values.PolicyJson.Length > 4096) throw new FormatException("policy_too_large");
                var json = JObject.Parse(values.PolicyJson, new JsonLoadSettings
                { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (json["schema"]?.Type != JTokenType.Integer || (int)json["schema"] != 1)
                    throw new FormatException("unsupported_schema");
                var revision = RequiredString(json, "policy_revision", 64);
                var experiment = RequiredString(json, "experiment_key", 40, true);
                var variant = RequiredString(json, "variant", 16, true);
                var interval = json["min_interval_sec"];
                if (interval == null || (interval.Type != JTokenType.Integer && interval.Type != JTokenType.Float))
                    throw new FormatException("invalid_interval_type");
                double seconds = (double)interval;
                if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 10 || seconds > 600)
                    throw new FormatException("interval_out_of_range");
                if (experiment.Length == 0 && variant.Length != 0) throw new FormatException("variant_without_experiment");
                if (experiment.Length > 0)
                {
                    if (experiment != RemoteConfigKeys.Experiment ||
                        !((variant == "a" && seconds == 60) || (variant == "b" && seconds == 90)))
                        throw new FormatException("unsupported_experiment_variant");
                    if (!allowExperiment) { error = "experiment_collection_not_allowed"; return fallback; }
                }
                return new AdPolicy(fallback.Enabled, baseline.DailyLimit, seconds, revision,
                    experiment, variant, source, values.Release, values.InterstitialDisabled);
            }
            catch (Exception exception) when (exception is Newtonsoft.Json.JsonException ||
                exception is FormatException || exception is OverflowException || exception is ArgumentException)
            {
                error = exception is FormatException ? exception.Message : "invalid_policy_json";
                return fallback;
            }
        }

        private static string RequiredString(JObject json, string key, int limit, bool allowEmpty = false)
        {
            if (json[key]?.Type != JTokenType.String) throw new FormatException("invalid_" + key);
            var value = (string)json[key];
            if (value.Length > limit || (!allowEmpty && string.IsNullOrWhiteSpace(value)))
                throw new FormatException("invalid_" + key);
            return value;
        }
    }
}
