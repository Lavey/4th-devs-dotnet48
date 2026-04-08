using System;
using System.Collections.Generic;
using System.IO;
using FourthDevs.AxClassifier.Models;
using Newtonsoft.Json;

namespace FourthDevs.AxClassifier.Core
{
    public static class DemoStore
    {
        private static readonly string DemosPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "demos.json");

        public static List<LabeledEmail> Load()
        {
            if (!File.Exists(DemosPath))
                return null;

            try
            {
                string json = File.ReadAllText(DemosPath);
                return JsonConvert.DeserializeObject<List<LabeledEmail>>(json);
            }
            catch
            {
                return null;
            }
        }

        public static void Save(List<LabeledEmail> demos)
        {
            string json = JsonConvert.SerializeObject(demos, Formatting.Indented);
            File.WriteAllText(DemosPath, json);
        }
    }
}
