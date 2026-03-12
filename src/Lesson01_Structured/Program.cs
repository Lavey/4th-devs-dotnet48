using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Common.Models;
using Newtonsoft.Json;

namespace FourthDevs.Lesson01_Structured
{
    /// <summary>
    /// Lesson 01 – Structured Output
    /// Asks the model to extract person data and return a strictly typed JSON object.
    /// The JSON schema is embedded in the request so the model always honours it.
    ///
    /// Source: 01_01_structured/app.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        private const string Model = "gpt-4.1-mini";

        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            const string text =
                "John is 30 years old and works as a software engineer. " +
                "He is skilled in JavaScript, Python, and React.";

            var person = await ExtractPerson(text);

            Console.WriteLine($"Name:       {person.Name ?? "unknown"}");
            Console.WriteLine($"Age:        {(person.Age.HasValue ? person.Age.ToString() : "unknown")}");
            Console.WriteLine($"Occupation: {person.Occupation ?? "unknown"}");
            Console.WriteLine($"Skills:     {(person.Skills?.Count > 0 ? string.Join(", ", person.Skills) : "none")}");
        }

        static async Task<PersonData> ExtractPerson(string text)
        {
            using (var client = new ResponsesApiClient())
            {
                var request = new ResponsesRequest
                {
                    Model = AiConfig.ResolveModel(Model),
                    Input = new List<InputMessage>
                    {
                        new InputMessage
                        {
                            Role    = "user",
                            Content = $"Extract person information from: \"{text}\""
                        }
                    },
                    Text = new TextOptions { Format = BuildPersonSchema() }
                };

                var response = await client.SendAsync(request);
                string json  = ResponsesApiClient.ExtractText(response);

                return JsonConvert.DeserializeObject<PersonData>(json);
            }
        }

        /// <summary>
        /// Returns the JSON schema object that tells the model what structure to produce.
        /// This is passed verbatim in the "text.format" field of the request.
        /// </summary>
        static object BuildPersonSchema()
        {
            return new
            {
                type   = "json_schema",
                name   = "person",
                strict = true,
                schema = new
                {
                    type = "object",
                    properties = new
                    {
                        name = new
                        {
                            type        = new[] { "string", "null" },
                            description = "Full name of the person. Use null if not mentioned."
                        },
                        age = new
                        {
                            type        = new[] { "number", "null" },
                            description = "Age in years. Use null if not mentioned or unclear."
                        },
                        occupation = new
                        {
                            type        = new[] { "string", "null" },
                            description = "Job title or profession. Use null if not mentioned."
                        },
                        skills = new
                        {
                            type        = "array",
                            items       = new { type = "string" },
                            description = "List of skills, technologies, or competencies. Empty array if none mentioned."
                        }
                    },
                    required             = new[] { "name", "age", "occupation", "skills" },
                    additionalProperties = false
                }
            };
        }
    }

    internal sealed class PersonData
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("age")]
        public double? Age { get; set; }

        [JsonProperty("occupation")]
        public string Occupation { get; set; }

        [JsonProperty("skills")]
        public List<string> Skills { get; set; } = new List<string>();
    }
}
