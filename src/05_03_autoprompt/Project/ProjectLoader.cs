using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FourthDevs.AutoPrompt.Config;
using FourthDevs.AutoPrompt.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.AutoPrompt.Project
{
    public static class ProjectLoader
    {
        public static LoadedProject Load(string projectDir)
        {
            projectDir = Path.GetFullPath(projectDir);
            string configPath = Path.Combine(projectDir, "autoprompt.config.json");

            if (!File.Exists(configPath))
            {
                throw new InvalidOperationException("Missing project config: " + configPath);
            }

            string configJson = File.ReadAllText(configPath);
            var rawConfig = JsonConvert.DeserializeObject<AutoPromptConfigFile>(configJson);

            // Normalize models
            var models = NormalizeModels(rawConfig.Models);

            // Resolve paths
            string schemaPath = Path.GetFullPath(Path.Combine(projectDir, rawConfig.Schema));
            string promptPath = Path.GetFullPath(Path.Combine(projectDir, rawConfig.Prompt));
            string testsDir = Path.GetFullPath(Path.Combine(projectDir, rawConfig.TestsDir));

            if (!File.Exists(schemaPath))
                throw new InvalidOperationException("Schema file not found: " + schemaPath);
            if (!File.Exists(promptPath))
                throw new InvalidOperationException("Prompt file not found: " + promptPath);
            if (!Directory.Exists(testsDir))
                throw new InvalidOperationException("Tests directory not found: " + testsDir);

            // Load schema
            string schemaJson = File.ReadAllText(schemaPath);
            var extractionSchema = JsonConvert.DeserializeObject<ExtractionSchema>(schemaJson);

            // Validate
            ProjectValidator.Validate(rawConfig, extractionSchema);

            // Load test cases
            var allCases = LoadTestCases(testsDir);

            var optimizeCaseIds = rawConfig.Optimization != null ? rawConfig.Optimization.Cases : null;
            var verifyCaseIds = rawConfig.Optimization != null ? rawConfig.Optimization.VerifyCases : null;

            var optimizeCases = optimizeCaseIds != null
                ? allCases.Where(tc => optimizeCaseIds.Contains(tc.Id)).ToList()
                : allCases;

            var verifyCases = verifyCaseIds != null
                ? allCases.Where(tc => verifyCaseIds.Contains(tc.Id)).ToList()
                : allCases;

            return new LoadedProject
            {
                Name = !string.IsNullOrEmpty(rawConfig.Name)
                    ? rawConfig.Name
                    : Path.GetFileName(projectDir),
                Dir = projectDir,
                ConfigPath = configPath,
                PromptPath = promptPath,
                InitialPrompt = File.ReadAllText(promptPath),
                ExtractionSchema = extractionSchema,
                Evaluation = rawConfig.Evaluation,
                Models = models,
                Optimization = rawConfig.Optimization ?? new OptimizationConfig(),
                TestCases = optimizeCases,
                VerifyCases = verifyCases
            };
        }

        private static ResolvedModels NormalizeModels(ModelsConfig models)
        {
            return new ResolvedModels
            {
                Execution = NormalizeModelProfile(
                    models != null ? models.Execution : null,
                    Defaults.DefaultExecution),
                Judge = NormalizeModelProfile(
                    models != null ? models.Judge : null,
                    Defaults.DefaultJudge),
                Improver = NormalizeModelProfile(
                    models != null ? models.Improver : null,
                    Defaults.DefaultImprover)
            };
        }

        private static ModelProfile NormalizeModelProfile(ModelProfileConfig profile, ModelProfile fallback)
        {
            var result = new ModelProfile();

            result.Model = (profile != null && !string.IsNullOrEmpty(profile.Model))
                ? profile.Model
                : fallback.Model;

            if (profile != null && profile.Reasoning != null)
            {
                result.Reasoning = new ReasoningConfig { Effort = profile.Reasoning.Effort };
            }
            else if (fallback.Reasoning != null)
            {
                result.Reasoning = new ReasoningConfig { Effort = fallback.Reasoning.Effort };
            }

            return result;
        }

        private static List<TestCase> LoadTestCases(string testsDir)
        {
            var inputFiles = Directory.GetFiles(testsDir, "input_*.md")
                .Select(Path.GetFileName)
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();

            if (inputFiles.Count == 0)
            {
                throw new InvalidOperationException(
                    "No test cases found in " + testsDir + ". Expected files named input_XX.md");
            }

            var testCases = new List<TestCase>();

            foreach (var fileName in inputFiles)
            {
                string id = fileName
                    .Replace("input_", "")
                    .Replace(".md", "");

                string input = File.ReadAllText(Path.Combine(testsDir, fileName));
                string expectedPath = Path.Combine(testsDir, "expected_" + id + ".json");
                string contextPath = Path.Combine(testsDir, "context_" + id + ".json");
                string priorPath = Path.Combine(testsDir, "prior_" + id + ".json");

                if (!File.Exists(expectedPath))
                {
                    throw new InvalidOperationException(
                        "Missing expected file for test case " + id + ": " + expectedPath);
                }

                JObject context = null;
                if (File.Exists(contextPath))
                {
                    context = JObject.Parse(File.ReadAllText(contextPath));
                }
                else if (File.Exists(priorPath))
                {
                    context = JObject.Parse(File.ReadAllText(priorPath));
                }

                testCases.Add(new TestCase
                {
                    Id = id,
                    Input = input,
                    Expected = JObject.Parse(File.ReadAllText(expectedPath)),
                    Context = context
                });
            }

            return testCases;
        }
    }
}
