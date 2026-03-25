using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Calendar.Tools
{
    public class LocalToolDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public object Parameters { get; set; }
        public Func<JObject, Task<object>> Handler { get; set; }
    }
}
