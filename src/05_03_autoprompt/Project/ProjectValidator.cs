using System;
using System.Collections.Generic;
using FourthDevs.AutoPrompt.Models;
using Newtonsoft.Json.Linq;

namespace FourthDevs.AutoPrompt.Project
{
    public static class ProjectValidator
    {
        private static readonly HashSet<string> AllowedFieldModes =
            new HashSet<string> { "exact", "semantic" };

        private static readonly HashSet<string> AllowedReasoningEfforts =
            new HashSet<string> { "none", "minimal", "low", "medium", "high", "xhigh" };

        private static void Fail(string message)
        {
            throw new InvalidOperationException("Invalid project config: " + message);
        }

        public static void Validate(AutoPromptConfigFile config, ExtractionSchema extractionSchema)
        {
            if (config == null) Fail("config must be an object");
            if (string.IsNullOrEmpty(config.Prompt)) Fail("\"prompt\" must be a non-empty string");
            if (string.IsNullOrEmpty(config.Schema)) Fail("\"schema\" must be a non-empty string");
            if (string.IsNullOrEmpty(config.TestsDir)) Fail("\"testsDir\" must be a non-empty string");

            var sections = config.Evaluation != null ? config.Evaluation.Sections : null;
            if (sections == null || sections.Count == 0)
            {
                Fail("\"evaluation.sections\" must be a non-empty array");
            }

            if (config.Optimization != null && config.Optimization.Candidates.HasValue)
            {
                if (config.Optimization.Candidates.Value <= 0)
                    Fail("\"optimization.candidates\" must be a positive integer when provided");
            }

            var modelRoles = config.Models;
            if (modelRoles == null) Fail("\"models\" must be an object");

            ValidateModelRole(modelRoles.Execution, "execution");
            ValidateModelRole(modelRoles.Judge, "judge");
            ValidateModelRole(modelRoles.Improver, "improver");

            if (string.IsNullOrEmpty(extractionSchema.Name))
                Fail("schema module must export an object with \"name\"");
            if (extractionSchema.Schema == null)
                Fail("schema module must export an object with \"schema\"");

            var rootProperties = extractionSchema.Schema["properties"] as JObject;
            var seenKeys = new HashSet<string>();
            double totalWeight = 0;

            foreach (var section in sections)
            {
                if (string.IsNullOrEmpty(section.Key)) Fail("each section needs a non-empty key");
                if (seenKeys.Contains(section.Key))
                    Fail(string.Format("duplicate section key \"{0}\"", section.Key));
                seenKeys.Add(section.Key);

                if (double.IsNaN(section.Weight) || double.IsInfinity(section.Weight) || section.Weight <= 0)
                    Fail(string.Format("section \"{0}\" must have a positive numeric weight", section.Key));
                totalWeight += section.Weight;

                if (section.MatchBy == null || section.MatchBy.Count == 0)
                    Fail(string.Format("section \"{0}\" must define a non-empty matchBy array", section.Key));

                if (section.Fields == null || section.Fields.Count == 0)
                    Fail(string.Format("section \"{0}\" must define a fields object", section.Key));

                foreach (var matchField in section.MatchBy)
                {
                    if (string.IsNullOrEmpty(matchField))
                        Fail(string.Format("section \"{0}\" has an invalid matchBy field", section.Key));
                    if (!section.Fields.ContainsKey(matchField))
                        Fail(string.Format(
                            "section \"{0}\" matchBy field \"{1}\" must also appear in fields",
                            section.Key, matchField));
                }

                foreach (var kvp in section.Fields)
                {
                    if (!AllowedFieldModes.Contains(kvp.Value))
                        Fail(string.Format(
                            "section \"{0}\" field \"{1}\" must be \"exact\" or \"semantic\"",
                            section.Key, kvp.Key));
                }

                if (rootProperties != null)
                {
                    var schemaSection = rootProperties[section.Key];
                    if (schemaSection == null)
                        Fail(string.Format(
                            "section \"{0}\" does not exist in the schema root", section.Key));
                    var typeToken = schemaSection["type"];
                    if (typeToken == null || typeToken.Value<string>() != "array")
                        Fail(string.Format(
                            "section \"{0}\" must point to an array field in the schema root", section.Key));
                }
            }

            if (Math.Abs(totalWeight - 1) > 0.000001)
            {
                Fail(string.Format("section weights must sum to 1. Received {0}", totalWeight));
            }
        }

        private static void ValidateModelRole(ModelProfileConfig profile, string role)
        {
            if (profile == null)
                Fail(string.Format("\"models.{0}\" must be an object", role));

            if (string.IsNullOrWhiteSpace(profile.Model))
                Fail(string.Format("\"models.{0}.model\" must be a non-empty string", role));

            if (profile.Reasoning != null)
            {
                if (string.IsNullOrEmpty(profile.Reasoning.Effort) ||
                    !AllowedReasoningEfforts.Contains(profile.Reasoning.Effort))
                {
                    Fail(string.Format(
                        "\"models.{0}.reasoning.effort\" must be one of {1}",
                        role, string.Join(", ", AllowedReasoningEfforts)));
                }
            }
        }
    }
}
