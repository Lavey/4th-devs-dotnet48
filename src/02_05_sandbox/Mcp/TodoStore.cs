using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace FourthDevs.Sandbox.Mcp
{
    /// <summary>
    /// In-memory todo item model.
    /// Mirrors the Todo interface in 02_05_sandbox/src/schemas.ts.
    /// </summary>
    internal sealed class Todo
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("completed")]
        public bool Completed { get; set; }

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; }

        [JsonProperty("updatedAt")]
        public string UpdatedAt { get; set; }
    }

    /// <summary>
    /// Thread-safe in-memory todo store that replaces the TypeScript MCP todo server.
    /// All methods return JSON strings matching the TypeScript server's response format.
    ///
    /// Mirrors servers/todo.ts (i-am-alice/4th-devs 02_05_sandbox).
    /// </summary>
    internal static class TodoStore
    {
        private static readonly Dictionary<string, Todo> _todos =
            new Dictionary<string, Todo>(StringComparer.Ordinal);

        private static readonly object _lock = new object();

        /// <summary>Creates a new todo. Returns <c>{"todo": {...}}</c>.</summary>
        public static string Create(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return JsonConvert.SerializeObject(new { error = "title is required" });

            var todo = new Todo
            {
                Id        = Guid.NewGuid().ToString(),
                Title     = title,
                Completed = false,
                CreatedAt = DateTime.UtcNow.ToString("O"),
                UpdatedAt = DateTime.UtcNow.ToString("O"),
            };

            lock (_lock) { _todos[todo.Id] = todo; }
            return JsonConvert.SerializeObject(new { todo });
        }

        /// <summary>Gets a todo by id. Returns <c>{"todo": {...}}</c> or <c>{"error": "..."}</c>.</summary>
        public static string Get(string id)
        {
            lock (_lock)
            {
                if (!_todos.TryGetValue(id ?? string.Empty, out Todo todo))
                    return JsonConvert.SerializeObject(new { error = "Not found" });
                return JsonConvert.SerializeObject(new { todo });
            }
        }

        /// <summary>
        /// Lists todos, optionally filtered by completion status.
        /// Returns <c>{"todos": [...]}</c>.
        /// </summary>
        public static string List(bool? completed = null)
        {
            lock (_lock)
            {
                IEnumerable<Todo> result = _todos.Values;
                if (completed.HasValue)
                    result = result.Where(t => t.Completed == completed.Value);
                return JsonConvert.SerializeObject(new { todos = result.ToArray() });
            }
        }

        /// <summary>
        /// Updates a todo. Returns updated <c>{"todo": {...}}</c> or <c>{"error": "..."}</c>.
        /// </summary>
        public static string Update(string id, string title, bool? completed)
        {
            lock (_lock)
            {
                if (!_todos.TryGetValue(id ?? string.Empty, out Todo todo))
                    return JsonConvert.SerializeObject(new { error = "Not found" });

                if (title != null) todo.Title = title;
                if (completed.HasValue) todo.Completed = completed.Value;
                todo.UpdatedAt = DateTime.UtcNow.ToString("O");
                return JsonConvert.SerializeObject(new { todo });
            }
        }

        /// <summary>Deletes a todo. Returns <c>{"success": true/false}</c>.</summary>
        public static string Delete(string id)
        {
            bool existed;
            lock (_lock) { existed = _todos.Remove(id ?? string.Empty); }
            return JsonConvert.SerializeObject(new { success = existed });
        }

        /// <summary>Clears all todos (used between runs).</summary>
        public static void Reset()
        {
            lock (_lock) { _todos.Clear(); }
        }
    }
}
