using System;
using System.Net;
using System.Text;
using System.Threading;
using FourthDevs.Apps.Models;
using Newtonsoft.Json;

namespace FourthDevs.Apps.Core
{
    internal sealed class UiServer
    {
        private readonly HttpListener _listener;
        private readonly string _todoPath;
        private readonly string _shoppingPath;
        private Thread _thread;
        private volatile bool _running;

        public string Url { get; }

        public UiServer(string host, int port, string todoPath, string shoppingPath)
        {
            _todoPath     = todoPath;
            _shoppingPath = shoppingPath;
            Url           = string.Format("http://{0}:{1}/", host, port);

            _listener = new HttpListener();
            _listener.Prefixes.Add(Url);
        }

        public void Start()
        {
            _listener.Start();
            _running = true;
            _thread  = new Thread(Listen) { IsBackground = true, Name = "UiServer" };
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            try { _listener.Stop(); } catch { }
        }

        private void Listen()
        {
            while (_running)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = _listener.GetContext();
                }
                catch (HttpListenerException)
                {
                    // Listener was stopped intentionally – exit the loop cleanly.
                    break;
                }
                catch (Exception ex)
                {
                    if (!_running) break;
                    Console.Error.WriteLine("[ui-server] Listener error: " + ex.Message);
                    break;
                }

                try { HandleRequest(ctx); }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("[ui-server] Error: " + ex.Message);
                }
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            var req  = ctx.Request;
            var resp = ctx.Response;

            // CORS headers
            resp.Headers.Add("Access-Control-Allow-Origin", "*");
            resp.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            resp.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (req.HttpMethod == "OPTIONS")
            {
                resp.StatusCode = 204;
                resp.Close();
                return;
            }

            string path = req.Url.AbsolutePath.TrimEnd('/');
            if (string.IsNullOrEmpty(path)) path = "/";

            if (path == "" || path == "/" || path == "/index.html")
            {
                ServeHtml(resp, req.Url.Query);
            }
            else if (path == "/api/lists")
            {
                if (req.HttpMethod == "GET")
                    ServeGetLists(resp);
                else if (req.HttpMethod == "POST")
                    ServePostLists(req, resp);
                else
                    SendError(resp, 405, "Method Not Allowed");
            }
            else
            {
                SendError(resp, 404, "Not Found");
            }
        }

        private void ServeHtml(HttpListenerResponse resp, string query)
        {
            string focus = "todo";
            if (!string.IsNullOrEmpty(query))
            {
                // Parse ?focus=todo|shopping
                string q = query.TrimStart('?');
                foreach (string part in q.Split('&'))
                {
                    string[] kv = part.Split('=');
                    if (kv.Length == 2 && kv[0] == "focus")
                    {
                        string val = kv[1].Trim().ToLowerInvariant();
                        if (val == "todo" || val == "shopping")
                            focus = val;
                        break;
                    }
                }
            }

            string html = BuildHtml(focus);
            byte[] buf  = Encoding.UTF8.GetBytes(html);
            resp.ContentType     = "text/html; charset=utf-8";
            resp.ContentLength64 = buf.Length;
            resp.OutputStream.Write(buf, 0, buf.Length);
            resp.Close();
        }

        private void ServeGetLists(HttpListenerResponse resp)
        {
            ListsState state = ListFiles.ReadListsState(_todoPath, _shoppingPath);
            SendJson(resp, 200, state);
        }

        private void ServePostLists(HttpListenerRequest req, HttpListenerResponse resp)
        {
            string body;
            using (var reader = new System.IO.StreamReader(req.InputStream, Encoding.UTF8))
                body = reader.ReadToEnd();

            ListsState incoming;
            try
            {
                incoming = JsonConvert.DeserializeObject<ListsState>(body);
            }
            catch (Exception ex)
            {
                SendError(resp, 400, "Invalid JSON: " + ex.Message);
                return;
            }

            ListFiles.WriteListsState(_todoPath, _shoppingPath, incoming);
            ListsState updated = ListFiles.ReadListsState(_todoPath, _shoppingPath);
            SendJson(resp, 200, updated);
        }

        private static void SendJson(HttpListenerResponse resp, int status, object obj)
        {
            string json = JsonConvert.SerializeObject(obj, Formatting.None);
            byte[] buf  = Encoding.UTF8.GetBytes(json);
            resp.StatusCode      = status;
            resp.ContentType     = "application/json; charset=utf-8";
            resp.ContentLength64 = buf.Length;
            resp.OutputStream.Write(buf, 0, buf.Length);
            resp.Close();
        }

        private static void SendError(HttpListenerResponse resp, int status, string message)
        {
            byte[] buf = Encoding.UTF8.GetBytes(message);
            resp.StatusCode      = status;
            resp.ContentType     = "text/plain; charset=utf-8";
            resp.ContentLength64 = buf.Length;
            resp.OutputStream.Write(buf, 0, buf.Length);
            resp.Close();
        }

        private static string BuildHtml(string initialFocus)
        {
            return @"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
  <title>List Manager</title>
  <style>
    *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
    body { font-family: system-ui, sans-serif; background: #f4f4f5; color: #111; min-height: 100vh; }
    header { background: #18181b; color: #fff; padding: 1rem 2rem; display: flex; align-items: center; gap: 1rem; }
    header h1 { font-size: 1.4rem; font-weight: 700; }
    main { max-width: 900px; margin: 2rem auto; padding: 0 1rem; display: grid; grid-template-columns: 1fr 1fr; gap: 1.5rem; }
    @media(max-width:600px){ main { grid-template-columns: 1fr; } }
    .section { background: #fff; border-radius: 12px; padding: 1.5rem; box-shadow: 0 1px 4px rgba(0,0,0,.08); }
    .section.focused { box-shadow: 0 0 0 3px #6366f1, 0 1px 4px rgba(0,0,0,.08); }
    .section h2 { font-size: 1.1rem; font-weight: 600; margin-bottom: 1rem; padding-bottom: .5rem; border-bottom: 1px solid #e4e4e7; }
    ul { list-style: none; display: flex; flex-direction: column; gap: .5rem; min-height: 2rem; }
    li { display: flex; align-items: center; gap: .6rem; padding: .4rem .5rem; border-radius: 6px; }
    li:hover { background: #f4f4f5; }
    li input[type=checkbox] { width: 1.1rem; height: 1.1rem; cursor: pointer; accent-color: #6366f1; flex-shrink: 0; }
    li label { cursor: pointer; line-height: 1.4; }
    li.done label { text-decoration: line-through; color: #71717a; }
    .empty { color: #a1a1aa; font-style: italic; font-size: .9rem; }
    footer { max-width: 900px; margin: 0 auto 2rem; padding: 0 1rem; display: flex; gap: 1rem; align-items: center; }
    .btn { padding: .6rem 1.4rem; border: none; border-radius: 8px; cursor: pointer; font-size: .95rem; font-weight: 600; }
    .btn-save { background: #6366f1; color: #fff; }
    .btn-save:hover { background: #4f46e5; }
    .status { font-size: .85rem; color: #71717a; }
    .status.ok  { color: #16a34a; }
    .status.err { color: #dc2626; }
  </style>
</head>
<body>
  <header><h1>📋 List Manager</h1></header>
  <main>
    <div class=""section"" id=""todo-section"">
      <h2>✅ Todo</h2>
      <ul id=""todo-list""><li class=""empty"">Loading…</li></ul>
    </div>
    <div class=""section"" id=""shopping-section"">
      <h2>🛒 Shopping</h2>
      <ul id=""shopping-list""><li class=""empty"">Loading…</li></ul>
    </div>
  </main>
  <footer>
    <button class=""btn btn-save"" onclick=""saveAll()"">💾 Save</button>
    <span class=""status"" id=""status""></span>
  </footer>
  <script>
    var state = { todo: [], shopping: [] };

    function getParam(name) {
      var q = location.search.substring(1).split('&');
      for (var i = 0; i < q.length; i++) {
        var kv = q[i].split('=');
        if (kv[0] === name) return decodeURIComponent(kv[1] || '');
      }
      return '';
    }

    function renderList(listId, items) {
      var ul = document.getElementById(listId);
      ul.innerHTML = '';
      if (!items || items.length === 0) {
        var li = document.createElement('li');
        li.className = 'empty';
        li.textContent = 'No items';
        ul.appendChild(li);
        return;
      }
      items.forEach(function(item) {
        var li  = document.createElement('li');
        li.className = item.done ? 'done' : '';
        var cb  = document.createElement('input');
        cb.type = 'checkbox';
        cb.checked = item.done;
        cb.id = listId + '-' + item.id;
        cb.addEventListener('change', function() {
          item.done = cb.checked;
          li.className = item.done ? 'done' : '';
        });
        var lbl = document.createElement('label');
        lbl.htmlFor = cb.id;
        lbl.textContent = item.text;
        li.appendChild(cb);
        li.appendChild(lbl);
        ul.appendChild(li);
      });
    }

    function loadLists() {
      fetch('/api/lists')
        .then(function(r) { return r.json(); })
        .then(function(data) {
          state = data;
          renderList('todo-list', state.todo);
          renderList('shopping-list', state.shopping);
          var focus = getParam('focus') || '" + initialFocus + @"';
          var sectionId = focus === 'shopping' ? 'shopping-section' : 'todo-section';
          var el = document.getElementById(sectionId);
          if (el) {
            el.classList.add('focused');
            el.scrollIntoView({ behavior: 'smooth', block: 'start' });
          }
        })
        .catch(function(e) { setStatus('Failed to load: ' + e.message, true); });
    }

    function saveAll() {
      setStatus('Saving…', false);
      fetch('/api/lists', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ todo: state.todo, shopping: state.shopping })
      })
      .then(function(r) { return r.json(); })
      .then(function(data) {
        state = data;
        renderList('todo-list', state.todo);
        renderList('shopping-list', state.shopping);
        setStatus('Saved ✓', false, true);
      })
      .catch(function(e) { setStatus('Save failed: ' + e.message, true); });
    }

    function setStatus(msg, isErr, isOk) {
      var el = document.getElementById('status');
      el.textContent = msg;
      el.className = 'status' + (isErr ? ' err' : isOk ? ' ok' : '');
      if (!isErr) setTimeout(function() { el.textContent = ''; el.className = 'status'; }, 3000);
    }

    loadLists();
  </script>
</body>
</html>";
        }
    }
}
