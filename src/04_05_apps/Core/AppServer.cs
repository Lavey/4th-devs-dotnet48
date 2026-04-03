using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FourthDevs.McpApps.Agent;
using FourthDevs.McpApps.Core;
using FourthDevs.McpApps.Store;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.McpApps.Core
{
    internal sealed class AppServer
    {
        private readonly HttpListener _listener;
        private Thread _thread;
        private volatile bool _running;

        public string Url { get; }

        public AppServer(string host, int port)
        {
            Url = string.Format("http://{0}:{1}/", host, port);
            _listener = new HttpListener();
            _listener.Prefixes.Add(Url);
        }

        public void Start()
        {
            _listener.Start();
            _running = true;
            _thread = new Thread(Listen) { IsBackground = true, Name = "AppServer" };
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
                try { ctx = _listener.GetContext(); }
                catch (HttpListenerException) { break; }
                catch { if (!_running) break; continue; }

                // Handle async on thread pool
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { HandleRequest(ctx); }
                    catch (Exception ex) { Console.Error.WriteLine("[server] " + ex.Message); }
                });
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var resp = ctx.Response;

            resp.Headers.Add("Access-Control-Allow-Origin", "*");
            resp.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            resp.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (req.HttpMethod == "OPTIONS") { resp.StatusCode = 204; resp.Close(); return; }

            string path = req.Url.AbsolutePath.TrimEnd('/');
            if (string.IsNullOrEmpty(path)) path = "/";

            try
            {
                if (path == "/" || path == "/index.html")
                    ServeHtml(resp);
                else if (path == "/api/bootstrap" && req.HttpMethod == "GET")
                    HandleBootstrap(resp);
                else if (path == "/api/chat" && req.HttpMethod == "POST")
                    HandleChat(req, resp);
                else if (path == "/api/mcp/tools/list" && req.HttpMethod == "POST")
                    HandleToolsList(resp);
                else if (path == "/api/mcp/tools/call" && req.HttpMethod == "POST")
                    HandleToolCall(req, resp);
                else
                    SendError(resp, 404, "Not Found");
            }
            catch (Exception ex)
            {
                SendError(resp, 500, ex.Message);
            }
        }

        private void HandleBootstrap(HttpListenerResponse resp)
        {
            var todosState = TodoStore.ReadState();
            var suggestions = new JArray
            {
                new JObject { ["category"] = "📋 Todos", ["text"] = "Show my todo board" },
                new JObject { ["category"] = "📋 Todos", ["text"] = "Add a todo: prepare monthly report" },
                new JObject { ["category"] = "📊 Sales", ["text"] = "Show me the sales report" },
                new JObject { ["category"] = "📊 Sales", ["text"] = "Open sales analytics for March" },
                new JObject { ["category"] = "📧 Campaigns", ["text"] = "List all newsletter campaigns" },
                new JObject { ["category"] = "📧 Campaigns", ["text"] = "Get the Spring Launch campaign report" },
                new JObject { ["category"] = "📧 Campaigns", ["text"] = "Compare Spring Launch vs January Welcome" },
                new JObject { ["category"] = "🎟️ Coupons", ["text"] = "Show active coupons" },
                new JObject { ["category"] = "🎟️ Coupons", ["text"] = "Create a 15% coupon SAVE15 for Growth plan" },
                new JObject { ["category"] = "📦 Products", ["text"] = "List all products with pricing" }
            };

            SendJson(resp, 200, new JObject
            {
                ["todosSummary"] = TodoStore.Summarize(todosState),
                ["productsSummary"] = StripeStore.SummarizeProducts(),
                ["salesSummary"] = StripeStore.SummarizeSales(),
                ["campaignsSummary"] = NewsletterStore.SummarizeCampaigns(),
                ["suggestions"] = suggestions
            });
        }

        private void HandleChat(HttpListenerRequest req, HttpListenerResponse resp)
        {
            string body = ReadBody(req);
            var parsed = JObject.Parse(body);
            string message = parsed["message"]?.ToString() ?? "";
            string context = parsed["context"]?.ToString();

            var result = AgentRunner.RunTurnAsync(message, context).GetAwaiter().GetResult();
            SendJson(resp, 200, JObject.FromObject(result));
        }

        private void HandleToolsList(HttpListenerResponse resp)
        {
            SendJson(resp, 200, new JObject { ["tools"] = ToolRegistry.GetDefinitionsForApi() });
        }

        private void HandleToolCall(HttpListenerRequest req, HttpListenerResponse resp)
        {
            string body = ReadBody(req);
            var parsed = JObject.Parse(body);
            string name = parsed["name"]?.ToString() ?? "";
            var args = parsed["arguments"] as JObject ?? new JObject();
            var tool = ToolRegistry.Find(name);
            if (tool == null) { SendError(resp, 404, "Tool not found: " + name); return; }
            var result = tool.Handler(args);
            SendJson(resp, 200, JObject.FromObject(new { text = result.Text, data = result.Structured }));
        }

        // ── HTML ──

        private void ServeHtml(HttpListenerResponse resp)
        {
            byte[] buf = Encoding.UTF8.GetBytes(HtmlPage);
            resp.ContentType = "text/html; charset=utf-8";
            resp.ContentLength64 = buf.Length;
            resp.OutputStream.Write(buf, 0, buf.Length);
            resp.Close();
        }

        // ── Helpers ──

        private static string ReadBody(HttpListenerRequest req)
        {
            using (var reader = new StreamReader(req.InputStream, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        private static void SendJson(HttpListenerResponse resp, int status, JToken obj)
        {
            string json = obj.ToString(Formatting.None);
            byte[] buf = Encoding.UTF8.GetBytes(json);
            resp.StatusCode = status;
            resp.ContentType = "application/json; charset=utf-8";
            resp.ContentLength64 = buf.Length;
            resp.OutputStream.Write(buf, 0, buf.Length);
            resp.Close();
        }

        private static void SendError(HttpListenerResponse resp, int status, string message)
        {
            byte[] buf = Encoding.UTF8.GetBytes(message);
            resp.StatusCode = status;
            resp.ContentType = "text/plain; charset=utf-8";
            resp.ContentLength64 = buf.Length;
            resp.OutputStream.Write(buf, 0, buf.Length);
            resp.Close();
        }

        // ── Inline HTML ──

        private static readonly string HtmlPage = @"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>Marketing Ops</title>
<style>
*,*::before,*::after{box-sizing:border-box;margin:0;padding:0}
body{font-family:system-ui,sans-serif;background:#0f172a;color:#e2e8f0;min-height:100vh;display:flex;flex-direction:column}
header{background:#1e293b;padding:1rem 2rem;display:flex;align-items:center;gap:1rem;border-bottom:1px solid #334155}
header h1{font-size:1.3rem;font-weight:700;color:#f1f5f9}
header span{color:#94a3b8;font-size:.85rem}
.container{flex:1;display:grid;grid-template-columns:260px 1fr;max-height:calc(100vh - 60px);overflow:hidden}
@media(max-width:768px){.container{grid-template-columns:1fr}}
.sidebar{background:#1e293b;border-right:1px solid #334155;padding:1rem;overflow-y:auto}
.sidebar h3{font-size:.8rem;text-transform:uppercase;color:#64748b;margin:1rem 0 .5rem;letter-spacing:.05em}
.sidebar button{display:block;width:100%;text-align:left;background:transparent;border:1px solid #334155;color:#cbd5e1;padding:.5rem .75rem;border-radius:6px;margin-bottom:.4rem;cursor:pointer;font-size:.85rem;transition:background .15s}
.sidebar button:hover{background:#334155}
.chat-area{display:flex;flex-direction:column;overflow:hidden}
.messages{flex:1;overflow-y:auto;padding:1.5rem;display:flex;flex-direction:column;gap:1rem}
.msg{max-width:85%;padding:.75rem 1rem;border-radius:12px;line-height:1.5;font-size:.9rem;word-break:break-word;white-space:pre-wrap}
.msg.user{align-self:flex-end;background:#4f46e5;color:#fff;border-bottom-right-radius:4px}
.msg.agent{align-self:flex-start;background:#1e293b;color:#e2e8f0;border-bottom-left-radius:4px;border:1px solid #334155}
.msg.agent .tool-card{background:#0f172a;border:1px solid #334155;border-radius:8px;padding:.75rem;margin-top:.5rem;font-size:.82rem}
.msg.agent .tool-card h4{color:#818cf8;font-size:.8rem;margin-bottom:.25rem}
.msg.agent .tool-card pre{overflow-x:auto;color:#94a3b8;font-size:.78rem;line-height:1.4;white-space:pre-wrap}
.input-bar{display:flex;gap:.5rem;padding:1rem 1.5rem;background:#1e293b;border-top:1px solid #334155}
.input-bar input{flex:1;background:#0f172a;border:1px solid #334155;border-radius:8px;padding:.6rem 1rem;color:#e2e8f0;font-size:.9rem;outline:none}
.input-bar input:focus{border-color:#6366f1}
.input-bar button{background:#6366f1;color:#fff;border:none;border-radius:8px;padding:.6rem 1.4rem;font-weight:600;cursor:pointer;font-size:.9rem}
.input-bar button:hover{background:#4f46e5}
.input-bar button:disabled{opacity:.5;cursor:not-allowed}
.loading{color:#94a3b8;font-style:italic;font-size:.85rem;padding:.5rem 0}
</style>
</head>
<body>
<header><h1>🚀 Marketing Ops</h1><span>AI assistant with tool-calling agent</span></header>
<div class=""container"">
  <div class=""sidebar"" id=""sidebar""><p class=""loading"">Loading suggestions…</p></div>
  <div class=""chat-area"">
    <div class=""messages"" id=""messages""></div>
    <div class=""input-bar"">
      <input id=""input"" placeholder=""Ask about campaigns, sales, coupons, products, or todos…"" autocomplete=""off"" />
      <button id=""send"" onclick=""sendMessage()"">Send</button>
    </div>
  </div>
</div>
<script>
var busy=false;
function $(id){return document.getElementById(id)}
function escHtml(s){var d=document.createElement('div');d.textContent=s;return d.innerHTML}

function appendMsg(role,html){
  var div=document.createElement('div');
  div.className='msg '+role;
  div.innerHTML=html;
  $('messages').appendChild(div);
  $('messages').scrollTop=$('messages').scrollHeight;
}

function sendMessage(text){
  var input=$('input');
  var msg=text||input.value.trim();
  if(!msg||busy)return;
  input.value='';
  appendMsg('user',escHtml(msg));
  busy=true;
  $('send').disabled=true;
  appendMsg('agent','<span class=""loading"">Thinking…</span>');
  fetch('/api/chat',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({message:msg})})
    .then(function(r){return r.json()})
    .then(function(data){
      var msgs=$('messages');
      msgs.removeChild(msgs.lastChild);
      var html=escHtml(data.text||'(no response)');
      if(data.toolExecutions&&data.toolExecutions.length>0){
        data.toolExecutions.forEach(function(te){
          html+='<div class=""tool-card""><h4>🔧 '+escHtml(te.toolName)+'</h4>';
          if(te.toolArgs) html+='<pre>'+escHtml(JSON.stringify(te.toolArgs,null,2))+'</pre>';
          if(te.toolResult){
            var preview=JSON.stringify(te.toolResult,null,2);
            if(preview.length>500)preview=preview.substring(0,500)+'…';
            html+='<pre>'+escHtml(preview)+'</pre>';
          }
          html+='</div>';
        });
      }
      appendMsg('agent',html);
    })
    .catch(function(e){
      var msgs=$('messages');
      msgs.removeChild(msgs.lastChild);
      appendMsg('agent','<span style=""color:#f87171"">Error: '+escHtml(e.message)+'</span>');
    })
    .then(function(){busy=false;$('send').disabled=false;$('input').focus()});
}

$('input').addEventListener('keydown',function(e){if(e.key==='Enter')sendMessage()});

fetch('/api/bootstrap')
  .then(function(r){return r.json()})
  .then(function(data){
    var sb=$('sidebar');
    sb.innerHTML='';
    if(data.suggestions){
      var cats={};
      data.suggestions.forEach(function(s){
        if(!cats[s.category])cats[s.category]=[];
        cats[s.category].push(s.text);
      });
      Object.keys(cats).forEach(function(cat){
        var h=document.createElement('h3');
        h.textContent=cat;
        sb.appendChild(h);
        cats[cat].forEach(function(t){
          var btn=document.createElement('button');
          btn.textContent=t;
          btn.onclick=function(){sendMessage(t)};
          sb.appendChild(btn);
        });
      });
    }
  });
$('input').focus();
</script>
</body>
</html>";
    }
}
