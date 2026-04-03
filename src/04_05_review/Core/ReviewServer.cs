using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FourthDevs.Review.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Review.Core
{
    internal sealed class ReviewServer
    {
        private readonly HttpListener _listener;
        private Thread _thread;
        private volatile bool _running;

        public string Url { get; }

        public ReviewServer(string host, int port)
        {
            Url = string.Format("http://{0}:{1}/", host, port);
            _listener = new HttpListener();
            _listener.Prefixes.Add(Url);
        }

        public void Start()
        {
            _listener.Start();
            _running = true;
            _thread = new Thread(Listen) { IsBackground = true, Name = "ReviewServer" };
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
                catch (Exception ex)
                {
                    if (!_running) break;
                    Console.Error.WriteLine("[review-server] Listener error: " + ex.Message);
                    break;
                }

                // Handle each request on a thread pool thread
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { HandleRequest(ctx); }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("[review-server] Error: " + ex.Message);
                        try
                        {
                            SendError(ctx.Response, 500, "Internal error: " + ex.Message);
                        }
                        catch { }
                    }
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

            if (req.HttpMethod == "OPTIONS")
            {
                resp.StatusCode = 204;
                resp.Close();
                return;
            }

            string path = req.Url.AbsolutePath.TrimEnd('/');
            if (string.IsNullOrEmpty(path)) path = "/";

            // Route
            if (path == "/" || path == "/index.html")
                ServeHtml(resp);
            else if (path == "/api/bootstrap" && req.HttpMethod == "GET")
                HandleBootstrap(resp);
            else if (path == "/api/document" && req.HttpMethod == "GET")
                HandleGetDocument(req, resp);
            else if (path == "/api/document/download" && req.HttpMethod == "GET")
                HandleDownloadDocument(req, resp);
            else if (path == "/api/review" && req.HttpMethod == "POST")
                HandleRunReview(req, resp);
            else if (path == "/api/review/block" && req.HttpMethod == "POST")
                HandleRerunBlock(req, resp);
            else if (path == "/api/document/save" && req.HttpMethod == "POST")
                HandleSaveDocument(req, resp);
            else if (path == "/api/comments/accept" && req.HttpMethod == "POST")
                HandleCommentAction(req, resp, "accept");
            else if (path == "/api/comments/reject" && req.HttpMethod == "POST")
                HandleCommentAction(req, resp, "reject");
            else if (path == "/api/comments/resolve" && req.HttpMethod == "POST")
                HandleCommentAction(req, resp, "resolve");
            else if (path == "/api/comments/convert" && req.HttpMethod == "POST")
                HandleCommentAction(req, resp, "convert");
            else if (path == "/api/comments/revert" && req.HttpMethod == "POST")
                HandleCommentAction(req, resp, "revert");
            else if (path == "/api/comments/accept-all" && req.HttpMethod == "POST")
                HandleBatchAction(req, resp, "accept-all");
            else if (path == "/api/comments/reject-all" && req.HttpMethod == "POST")
                HandleBatchAction(req, resp, "reject-all");
            else
                SendError(resp, 404, "Not Found");
        }

        // ---- Handlers ----

        private void HandleBootstrap(HttpListenerResponse resp)
        {
            var result = new BootstrapResponse
            {
                Documents = Store.ListDocuments(),
                Prompts = Store.ListPrompts()
            };
            SendJson(resp, 200, result);
        }

        private void HandleGetDocument(HttpListenerRequest req, HttpListenerResponse resp)
        {
            string docPath = req.QueryString["path"];
            if (string.IsNullOrEmpty(docPath))
            {
                SendError(resp, 400, "Missing path parameter");
                return;
            }

            var document = Store.LoadDocument(docPath);
            var review = Store.LoadLatestReviewForDocument(docPath);
            if (review != null)
                review = Store.HydrateReviewForDocument(document, review);

            SendJson(resp, 200, new DocumentResponse { Document = document, Review = review });
        }

        private void HandleDownloadDocument(HttpListenerRequest req, HttpListenerResponse resp)
        {
            string docPath = req.QueryString["path"];
            if (string.IsNullOrEmpty(docPath))
            {
                SendError(resp, 400, "Missing path parameter");
                return;
            }

            string markdown = ReviewEngine.GetDocumentMarkdown(docPath);
            byte[] buf = Encoding.UTF8.GetBytes(markdown);
            resp.ContentType = "text/markdown; charset=utf-8";
            string filename = Path.GetFileName(docPath);
            resp.Headers.Add("Content-Disposition", "attachment; filename=\"" + filename + "\"");
            resp.ContentLength64 = buf.Length;
            resp.OutputStream.Write(buf, 0, buf.Length);
            resp.Close();
        }

        private void HandleRunReview(HttpListenerRequest req, HttpListenerResponse resp)
        {
            string body = ReadBody(req);
            JObject json;
            try { json = JObject.Parse(body); }
            catch
            {
                SendError(resp, 400, "Invalid JSON");
                return;
            }

            string docPath = json["documentPath"]?.ToString();
            string promptPath = json["promptPath"]?.ToString();
            string mode = json["mode"]?.ToString() ?? "paragraph";

            if (string.IsNullOrEmpty(docPath) || string.IsNullOrEmpty(promptPath))
            {
                SendError(resp, 400, "Missing documentPath or promptPath");
                return;
            }

            // Stream NDJSON
            resp.ContentType = "application/x-ndjson; charset=utf-8";
            resp.StatusCode = 200;
            resp.SendChunked = true;

            var stream = resp.OutputStream;
            var lockObj = new object();

            Action<ReviewEvent> onEvent = (evt) =>
            {
                string line = JsonConvert.SerializeObject(evt, Formatting.None) + "\n";
                byte[] buf = Encoding.UTF8.GetBytes(line);
                lock (lockObj)
                {
                    try
                    {
                        stream.Write(buf, 0, buf.Length);
                        stream.Flush();
                    }
                    catch { /* client disconnected */ }
                }
            };

            try
            {
                ReviewEngine.RunReview(docPath, promptPath, mode, onEvent)
                    .GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                onEvent(new ReviewEvent { Type = "error", Error = ex.Message });
            }
            finally
            {
                try { resp.Close(); } catch { }
            }
        }

        private void HandleRerunBlock(HttpListenerRequest req, HttpListenerResponse resp)
        {
            string body = ReadBody(req);
            JObject json;
            try { json = JObject.Parse(body); }
            catch
            {
                SendError(resp, 400, "Invalid JSON");
                return;
            }

            string reviewId = json["reviewId"]?.ToString();
            string blockId = json["blockId"]?.ToString();

            if (string.IsNullOrEmpty(reviewId) || string.IsNullOrEmpty(blockId))
            {
                SendError(resp, 400, "Missing reviewId or blockId");
                return;
            }

            resp.ContentType = "application/x-ndjson; charset=utf-8";
            resp.StatusCode = 200;
            resp.SendChunked = true;

            var stream = resp.OutputStream;
            var lockObj = new object();

            Action<ReviewEvent> onEvent = (evt) =>
            {
                string line = JsonConvert.SerializeObject(evt, Formatting.None) + "\n";
                byte[] buf = Encoding.UTF8.GetBytes(line);
                lock (lockObj)
                {
                    try
                    {
                        stream.Write(buf, 0, buf.Length);
                        stream.Flush();
                    }
                    catch { }
                }
            };

            try
            {
                ReviewEngine.RerunReviewBlock(reviewId, blockId, onEvent)
                    .GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                onEvent(new ReviewEvent { Type = "error", Error = ex.Message });
            }
            finally
            {
                try { resp.Close(); } catch { }
            }
        }

        private void HandleSaveDocument(HttpListenerRequest req, HttpListenerResponse resp)
        {
            string body = ReadBody(req);
            JObject json;
            try { json = JObject.Parse(body); }
            catch
            {
                SendError(resp, 400, "Invalid JSON");
                return;
            }

            string reviewId = json["reviewId"]?.ToString();
            string blockId = json["blockId"]?.ToString();
            string text = json["text"]?.ToString();

            if (string.IsNullOrEmpty(reviewId) || string.IsNullOrEmpty(blockId))
            {
                SendError(resp, 400, "Missing reviewId or blockId");
                return;
            }

            var review = ReviewEngine.UpdateBlock(reviewId, blockId, text ?? string.Empty);
            SendJson(resp, 200, new { ok = true, review });
        }

        private void HandleCommentAction(HttpListenerRequest req, HttpListenerResponse resp, string action)
        {
            string body = ReadBody(req);
            JObject json;
            try { json = JObject.Parse(body); }
            catch
            {
                SendError(resp, 400, "Invalid JSON");
                return;
            }

            string reviewId = json["reviewId"]?.ToString();
            string commentId = json["commentId"]?.ToString();

            if (string.IsNullOrEmpty(reviewId) || string.IsNullOrEmpty(commentId))
            {
                SendError(resp, 400, "Missing reviewId or commentId");
                return;
            }

            ReviewData review;
            switch (action)
            {
                case "accept":
                    review = ReviewEngine.AcceptReviewComment(reviewId, commentId);
                    break;
                case "reject":
                    review = ReviewEngine.RejectReviewComment(reviewId, commentId);
                    break;
                case "resolve":
                    review = ReviewEngine.ResolveReviewComment(reviewId, commentId);
                    break;
                case "convert":
                    string suggestion = json["suggestion"]?.ToString() ?? "";
                    review = ReviewEngine.ConvertToSuggestion(reviewId, commentId, suggestion);
                    break;
                case "revert":
                    review = ReviewEngine.RevertReviewComment(reviewId, commentId);
                    break;
                default:
                    SendError(resp, 400, "Unknown action");
                    return;
            }

            if (review == null)
            {
                SendError(resp, 404, "Review not found");
                return;
            }

            // Reload document to return fresh data
            var document = Store.LoadDocument(review.DocumentPath);
            review = Store.HydrateReviewForDocument(document, review);
            SendJson(resp, 200, new { ok = true, review, document });
        }

        private void HandleBatchAction(HttpListenerRequest req, HttpListenerResponse resp, string action)
        {
            string body = ReadBody(req);
            JObject json;
            try { json = JObject.Parse(body); }
            catch
            {
                SendError(resp, 400, "Invalid JSON");
                return;
            }

            string reviewId = json["reviewId"]?.ToString();
            var commentIds = json["commentIds"]?.ToObject<List<string>>() ?? new List<string>();

            if (string.IsNullOrEmpty(reviewId))
            {
                SendError(resp, 400, "Missing reviewId");
                return;
            }

            ReviewData review;
            if (action == "accept-all")
                review = ReviewEngine.BatchAcceptComments(reviewId, commentIds);
            else
                review = ReviewEngine.BatchRejectComments(reviewId, commentIds);

            if (review == null)
            {
                SendError(resp, 404, "Review not found");
                return;
            }

            var document = Store.LoadDocument(review.DocumentPath);
            review = Store.HydrateReviewForDocument(document, review);
            SendJson(resp, 200, new { ok = true, review, document });
        }

        // ---- Helpers ----

        private static string ReadBody(HttpListenerRequest req)
        {
            using (var reader = new StreamReader(req.InputStream, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        private static void SendJson(HttpListenerResponse resp, int status, object obj)
        {
            string json = JsonConvert.SerializeObject(obj, Formatting.None,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
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

        // ---- Inline HTML ----

        private void ServeHtml(HttpListenerResponse resp)
        {
            string html = BuildHtml();
            byte[] buf = Encoding.UTF8.GetBytes(html);
            resp.ContentType = "text/html; charset=utf-8";
            resp.ContentLength64 = buf.Length;
            resp.OutputStream.Write(buf, 0, buf.Length);
            resp.Close();
        }

        private static string BuildHtml()
        {
            return @"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8""/>
<meta name=""viewport"" content=""width=device-width,initial-scale=1""/>
<title>Markdown Review Lab</title>
<style>
*,*::before,*::after{box-sizing:border-box;margin:0;padding:0}
body{font-family:system-ui,-apple-system,sans-serif;background:#f8f9fa;color:#1a1a2e;min-height:100vh}
header{background:#1a1a2e;color:#fff;padding:1rem 2rem;display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:.5rem}
header h1{font-size:1.3rem;font-weight:700}
.controls{display:flex;gap:.5rem;align-items:center;flex-wrap:wrap}
select,button{padding:.4rem .8rem;border-radius:6px;font-size:.85rem;border:1px solid #444}
select{background:#2a2a4a;color:#fff}
button{background:#6366f1;color:#fff;border:none;cursor:pointer;font-weight:600}
button:hover{background:#4f46e5}
button:disabled{opacity:.5;cursor:not-allowed}
.btn-sm{padding:.25rem .5rem;font-size:.75rem}
.btn-danger{background:#dc2626}
.btn-danger:hover{background:#b91c1c}
.btn-success{background:#16a34a}
.btn-success:hover{background:#15803d}
.btn-muted{background:#6b7280}
.btn-muted:hover{background:#4b5563}
main{max-width:960px;margin:1.5rem auto;padding:0 1rem}
.status-bar{background:#fff;border-radius:8px;padding:.75rem 1rem;margin-bottom:1rem;box-shadow:0 1px 3px rgba(0,0,0,.08);font-size:.85rem;color:#6b7280}
.status-bar.active{color:#6366f1;font-weight:600}
.status-bar.error{color:#dc2626}
.document{background:#fff;border-radius:12px;padding:1.5rem;box-shadow:0 1px 4px rgba(0,0,0,.08)}
.block{position:relative;padding:.5rem .75rem;border-radius:6px;margin-bottom:.5rem;line-height:1.6;transition:background .15s}
.block:hover{background:#f1f5f9}
.block.active{background:#eff6ff;outline:2px solid #6366f1}
.block-label{font-size:.65rem;color:#94a3b8;position:absolute;top:.15rem;right:.5rem;font-family:monospace}
.block h1,.block h2,.block h3,.block h4,.block h5,.block h6{margin:.25rem 0}
.block h1{font-size:1.5rem}.block h2{font-size:1.25rem}.block h3{font-size:1.1rem}
.block-code{background:#f1f5f9;font-family:monospace;font-size:.85rem;white-space:pre-wrap;padding:.75rem;border-radius:6px;color:#475569}
.block-hr{border:none;border-top:2px solid #e2e8f0;margin:.75rem 0}
.block-quote{border-left:3px solid #6366f1;padding-left:.75rem;color:#475569;font-style:italic}
.highlight{background:#fef08a;border-radius:2px;cursor:pointer;position:relative}
.highlight.suggestion{background:#bbf7d0}
.highlight.accepted{background:#d1fae5;text-decoration:line-through;opacity:.6}
.highlight.rejected{opacity:.4;text-decoration:line-through}
.highlight.stale{background:#fecaca;opacity:.5}
.comment-popup{position:fixed;z-index:100;background:#1a1a2e;color:#fff;border-radius:10px;padding:1rem;max-width:380px;min-width:260px;box-shadow:0 8px 24px rgba(0,0,0,.25);font-size:.85rem}
.comment-popup .cp-title{font-weight:700;margin-bottom:.25rem}
.comment-popup .cp-severity{font-size:.7rem;padding:.1rem .4rem;border-radius:4px;margin-left:.5rem;text-transform:uppercase}
.cp-severity.high{background:#dc2626}.cp-severity.medium{background:#f59e0b;color:#000}.cp-severity.low{background:#6b7280}
.comment-popup .cp-body{margin:.5rem 0;line-height:1.4;color:#cbd5e1}
.comment-popup .cp-suggestion{background:#2a2a4a;padding:.5rem;border-radius:6px;margin:.5rem 0;font-family:monospace;font-size:.8rem;color:#86efac}
.comment-popup .cp-actions{display:flex;gap:.5rem;margin-top:.5rem}
.summary-box{background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px;padding:1rem;margin-top:1rem;font-size:.9rem;line-height:1.5}
.summary-box h3{font-size:.95rem;margin-bottom:.5rem;color:#1e40af}
.review-stats{display:flex;gap:1rem;margin-top:.75rem;flex-wrap:wrap}
.stat{font-size:.8rem;color:#6b7280}
.stat strong{color:#1a1a2e}
.shortcuts-help{font-size:.7rem;color:#94a3b8;margin-top:.5rem}
.progress-dots{display:flex;gap:4px;align-items:center}
.dot{width:8px;height:8px;border-radius:50%;background:#e2e8f0;transition:background .2s}
.dot.done{background:#16a34a}
.dot.active{background:#6366f1;animation:pulse 1s infinite}
@keyframes pulse{0%,100%{opacity:1}50%{opacity:.4}}
</style>
</head>
<body>
<header>
  <h1>📝 Markdown Review Lab</h1>
  <div class=""controls"">
    <select id=""docSelect""><option value="""">Select document…</option></select>
    <select id=""promptSelect""><option value="""">Select prompt…</option></select>
    <select id=""modeSelect""><option value=""paragraph"">Paragraph</option><option value=""at_once"">At Once</option></select>
    <button id=""runBtn"" disabled>▶ Run Review</button>
    <button id=""downloadBtn"" class=""btn-muted btn-sm"" style=""display:none"">⬇ Download</button>
  </div>
</header>
<main>
  <div class=""status-bar"" id=""status"">Select a document and prompt to begin.</div>
  <div id=""progressArea""></div>
  <div class=""document"" id=""docArea"" style=""display:none""></div>
  <div id=""summaryArea""></div>
  <div class=""shortcuts-help"" id=""shortcutsHelp"" style=""display:none"">
    Keyboard: <b>j</b>/<b>k</b> navigate comments · <b>a</b> accept · <b>r</b> reject · <b>u</b> revert · <b>Esc</b> dismiss
  </div>
</main>
<div class=""comment-popup"" id=""popup"" style=""display:none""></div>

<script>
(function(){
  var state = {
    documents: [], prompts: [], document: null, review: null,
    activeCommentId: null, commentIndex: -1
  };

  var docSel = document.getElementById('docSelect');
  var promptSel = document.getElementById('promptSelect');
  var modeSel = document.getElementById('modeSelect');
  var runBtn = document.getElementById('runBtn');
  var downloadBtn = document.getElementById('downloadBtn');
  var statusEl = document.getElementById('status');
  var docArea = document.getElementById('docArea');
  var summaryArea = document.getElementById('summaryArea');
  var popup = document.getElementById('popup');
  var progressArea = document.getElementById('progressArea');
  var shortcutsHelp = document.getElementById('shortcutsHelp');

  function setStatus(msg, cls) {
    statusEl.textContent = msg;
    statusEl.className = 'status-bar' + (cls ? ' ' + cls : '');
  }

  function api(url, opts) {
    return fetch(url, opts).then(function(r) { return r.json(); });
  }

  function postJson(url, data) {
    return api(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
  }

  // Bootstrap
  api('/api/bootstrap').then(function(d) {
    state.documents = d.documents || [];
    state.prompts = d.prompts || [];
    d.documents.forEach(function(doc) {
      var o = document.createElement('option');
      o.value = doc.path; o.textContent = doc.title || doc.path;
      docSel.appendChild(o);
    });
    d.prompts.forEach(function(p) {
      var o = document.createElement('option');
      o.value = p.path; o.textContent = p.title || p.path;
      promptSel.appendChild(o);
    });
  });

  docSel.addEventListener('change', function() {
    if (!docSel.value) return;
    loadDocument(docSel.value);
  });

  function updateRunBtn() {
    runBtn.disabled = !(docSel.value && promptSel.value);
  }
  docSel.addEventListener('change', updateRunBtn);
  promptSel.addEventListener('change', updateRunBtn);

  function loadDocument(path) {
    setStatus('Loading document…', 'active');
    api('/api/document?path=' + encodeURIComponent(path)).then(function(d) {
      state.document = d.document;
      state.review = d.review;
      renderDocument();
      downloadBtn.style.display = 'inline-block';
      setStatus('Document loaded. ' + (d.review ? 'Review found with ' + d.review.comments.length + ' comments.' : 'No review yet.'), '');
      shortcutsHelp.style.display = d.review ? 'block' : 'none';
    }).catch(function(e) { setStatus('Error: ' + e.message, 'error'); });
  }

  function renderDocument() {
    docArea.style.display = 'block';
    docArea.innerHTML = '';
    summaryArea.innerHTML = '';
    if (!state.document) return;
    var blocks = state.document.blocks;
    blocks.forEach(function(block) {
      var div = document.createElement('div');
      div.className = 'block';
      div.dataset.blockId = block.id;
      var label = document.createElement('span');
      label.className = 'block-label';
      label.textContent = block.id;
      div.appendChild(label);

      var content = document.createElement('div');
      if (block.type === 'heading') {
        var lvl = (block.meta && block.meta.level) || 1;
        var h = document.createElement('h' + Math.min(lvl, 6));
        h.innerHTML = renderInlineWithComments(block);
        content.appendChild(h);
      } else if (block.type === 'code') {
        content.className = 'block-code';
        content.textContent = block.text;
      } else if (block.type === 'thematic_break') {
        var hr = document.createElement('hr');
        hr.className = 'block-hr';
        content.appendChild(hr);
      } else if (block.type === 'blockquote') {
        content.className = 'block-quote';
        content.innerHTML = renderInlineWithComments(block);
      } else if (block.type === 'list_item') {
        var marker = (block.meta && block.meta.listType === 'ordered') ? ((block.meta.marker || '1.') + ' ') : '• ';
        content.innerHTML = marker + renderInlineWithComments(block);
      } else {
        content.innerHTML = renderInlineWithComments(block);
      }
      div.appendChild(content);
      docArea.appendChild(div);
    });

    if (state.review && state.review.summary) {
      renderSummary(state.review.summary);
    }
  }

  function renderInlineWithComments(block) {
    var text = escapeHtml(block.text);
    if (!state.review || !state.review.comments) return text;
    var comments = state.review.comments.filter(function(c) { return c.blockId === block.id; });
    if (comments.length === 0) return text;

    // Sort by start desc to insert from end
    comments.sort(function(a, b) { return b.start - a.start; });
    // We need to work on the original text for positions then escape
    var raw = block.text;
    var segments = [];
    var lastEnd = raw.length;

    // Sort asc for building segments
    var sorted = comments.slice().sort(function(a, b) { return a.start - b.start; });
    var result = '';
    var pos = 0;
    sorted.forEach(function(c) {
      if (c.start > pos) result += escapeHtml(raw.substring(pos, c.start));
      if (c.start >= pos && c.end <= raw.length) {
        var cls = 'highlight';
        if (c.kind === 'suggestion') cls += ' suggestion';
        if (c.status === 'accepted') cls += ' accepted';
        if (c.status === 'rejected') cls += ' rejected';
        if (c.status === 'stale') cls += ' stale';
        result += '<span class=""' + cls + '"" data-comment-id=""' + c.id + '"" onclick=""window._showComment(\'' + c.id + '\', event)"">';
        result += escapeHtml(raw.substring(c.start, c.end));
        result += '</span>';
        pos = c.end;
      }
    });
    if (pos < raw.length) result += escapeHtml(raw.substring(pos));
    return result;
  }

  function escapeHtml(s) {
    return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/""/g,'&quot;');
  }

  window._showComment = function(commentId, evt) {
    evt.stopPropagation();
    var c = findComment(commentId);
    if (!c) return;
    state.activeCommentId = commentId;
    updateCommentIndex();
    showPopup(c, evt.clientX, evt.clientY);
  };

  function findComment(id) {
    if (!state.review) return null;
    for (var i = 0; i < state.review.comments.length; i++) {
      if (state.review.comments[i].id === id) return state.review.comments[i];
    }
    return null;
  }

  function updateCommentIndex() {
    if (!state.review) return;
    var open = state.review.comments.filter(function(c) { return c.status === 'open'; });
    for (var i = 0; i < open.length; i++) {
      if (open[i].id === state.activeCommentId) { state.commentIndex = i; return; }
    }
    state.commentIndex = -1;
  }

  function showPopup(c, x, y) {
    var html = '<div class=""cp-title"">' + escapeHtml(c.title) + '<span class=""cp-severity ' + c.severity + '"">' + c.severity + '</span></div>';
    html += '<div class=""cp-body"">' + escapeHtml(c.body) + '</div>';
    if (c.kind === 'suggestion' && c.suggestion) {
      html += '<div class=""cp-suggestion"">' + escapeHtml(c.suggestion) + '</div>';
    }
    html += '<div style=""font-size:.7rem;color:#94a3b8"">Status: ' + c.status + '</div>';
    html += '<div class=""cp-actions"">';
    if (c.status === 'open') {
      html += '<button class=""btn-sm btn-success"" onclick=""window._commentAction(\'' + c.id + '\',\'accept\')"">✓ Accept</button>';
      html += '<button class=""btn-sm btn-danger"" onclick=""window._commentAction(\'' + c.id + '\',\'reject\')"">✗ Reject</button>';
    }
    if (c.status === 'accepted') {
      html += '<button class=""btn-sm btn-muted"" onclick=""window._commentAction(\'' + c.id + '\',\'revert\')"">↩ Revert</button>';
    }
    html += '<button class=""btn-sm btn-muted"" onclick=""window._hidePopup()"">Close</button>';
    html += '</div>';
    popup.innerHTML = html;
    popup.style.display = 'block';
    // Position
    var pw = 360, ph = popup.offsetHeight || 200;
    var left = Math.min(x + 10, window.innerWidth - pw - 20);
    var top = Math.min(y + 10, window.innerHeight - ph - 20);
    if (top < 10) top = 10;
    if (left < 10) left = 10;
    popup.style.left = left + 'px';
    popup.style.top = top + 'px';
  }

  window._hidePopup = function() {
    popup.style.display = 'none';
    state.activeCommentId = null;
  };

  window._commentAction = function(commentId, action) {
    var reviewId = state.review && state.review.id;
    if (!reviewId) return;
    setStatus('Processing…', 'active');
    postJson('/api/comments/' + action, { reviewId: reviewId, commentId: commentId })
      .then(function(d) {
        if (d.review) state.review = d.review;
        if (d.document) state.document = d.document;
        renderDocument();
        window._hidePopup();
        setStatus('Comment ' + action + 'ed.', '');
      }).catch(function(e) { setStatus('Error: ' + e.message, 'error'); });
  };

  // Run review
  runBtn.addEventListener('click', function() {
    var docPath = docSel.value;
    var promptPath = promptSel.value;
    var mode = modeSel.value;
    if (!docPath || !promptPath) return;

    runBtn.disabled = true;
    setStatus('Running review…', 'active');
    progressArea.innerHTML = '<div class=""progress-dots"" id=""dots""></div>';
    summaryArea.innerHTML = '';

    fetch('/api/review', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ documentPath: docPath, promptPath: promptPath, mode: mode })
    }).then(function(response) {
      var reader = response.body.getReader();
      var decoder = new TextDecoder();
      var buffer = '';

      function read() {
        return reader.read().then(function(result) {
          if (result.done) {
            runBtn.disabled = false;
            return;
          }
          buffer += decoder.decode(result.value, { stream: true });
          var lines = buffer.split('\n');
          buffer = lines.pop();
          lines.forEach(function(line) {
            if (!line.trim()) return;
            try {
              var evt = JSON.parse(line);
              handleReviewEvent(evt);
            } catch(e) {}
          });
          return read();
        });
      }
      return read();
    }).catch(function(e) {
      setStatus('Review error: ' + e.message, 'error');
      runBtn.disabled = false;
    });
  });

  function handleReviewEvent(evt) {
    switch(evt.type) {
      case 'started':
        setStatus('Review started: ' + evt.reviewId, 'active');
        break;
      case 'block_start':
        addDot(evt.blockId, 'active');
        break;
      case 'comment_added':
        if (evt.comment && state.review) {
          state.review.comments.push(evt.comment);
          renderDocument();
        } else if (evt.comment) {
          // review not yet in state
        }
        setStatus('Comment added: ' + (evt.comment ? evt.comment.title : ''), 'active');
        break;
      case 'block_done':
        setDot(evt.blockId, 'done');
        break;
      case 'summary_start':
        setStatus('Generating summary…', 'active');
        break;
      case 'complete':
        state.review = evt.review || state.review;
        loadDocument(docSel.value); // refresh
        if (evt.summary) renderSummary(evt.summary);
        setStatus('Review complete!', '');
        shortcutsHelp.style.display = 'block';
        runBtn.disabled = false;
        break;
      case 'error':
        setStatus('Error: ' + evt.error, 'error');
        runBtn.disabled = false;
        break;
    }
  }

  function addDot(id, cls) {
    var dots = document.getElementById('dots');
    if (!dots) return;
    var d = document.createElement('span');
    d.className = 'dot ' + cls;
    d.dataset.blockId = id;
    d.title = id;
    dots.appendChild(d);
  }

  function setDot(id, cls) {
    var dots = document.getElementById('dots');
    if (!dots) return;
    var all = dots.querySelectorAll('.dot');
    for (var i = 0; i < all.length; i++) {
      if (all[i].dataset.blockId === id) { all[i].className = 'dot ' + cls; return; }
    }
  }

  function renderSummary(text) {
    summaryArea.innerHTML = '<div class=""summary-box""><h3>Review Summary</h3><p>' + escapeHtml(text) + '</p></div>';
    if (state.review) {
      var open = 0, accepted = 0, rejected = 0;
      state.review.comments.forEach(function(c) {
        if (c.status === 'open') open++;
        else if (c.status === 'accepted') accepted++;
        else if (c.status === 'rejected') rejected++;
      });
      var stats = '<div class=""review-stats"">';
      stats += '<span class=""stat"">Total: <strong>' + state.review.comments.length + '</strong></span>';
      stats += '<span class=""stat"">Open: <strong>' + open + '</strong></span>';
      stats += '<span class=""stat"">Accepted: <strong>' + accepted + '</strong></span>';
      stats += '<span class=""stat"">Rejected: <strong>' + rejected + '</strong></span>';
      stats += '</div>';
      summaryArea.innerHTML += stats;
    }
  }

  downloadBtn.addEventListener('click', function() {
    if (!docSel.value) return;
    window.open('/api/document/download?path=' + encodeURIComponent(docSel.value), '_blank');
  });

  // Keyboard shortcuts
  document.addEventListener('keydown', function(e) {
    if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA' || e.target.tagName === 'SELECT') return;
    if (!state.review) return;
    var open = state.review.comments.filter(function(c) { return c.status === 'open'; });
    if (e.key === 'Escape') { window._hidePopup(); return; }
    if (e.key === 'j') {
      state.commentIndex = Math.min(state.commentIndex + 1, open.length - 1);
      if (open[state.commentIndex]) navigateToComment(open[state.commentIndex]);
    }
    if (e.key === 'k') {
      state.commentIndex = Math.max(state.commentIndex - 1, 0);
      if (open[state.commentIndex]) navigateToComment(open[state.commentIndex]);
    }
    if (e.key === 'a' && state.activeCommentId) {
      window._commentAction(state.activeCommentId, 'accept');
    }
    if (e.key === 'r' && state.activeCommentId) {
      window._commentAction(state.activeCommentId, 'reject');
    }
    if (e.key === 'u' && state.activeCommentId) {
      window._commentAction(state.activeCommentId, 'revert');
    }
  });

  function navigateToComment(c) {
    state.activeCommentId = c.id;
    var el = document.querySelector('[data-comment-id=""' + c.id + '""]');
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'center' });
      var rect = el.getBoundingClientRect();
      showPopup(c, rect.right + 10, rect.top);
    }
  }

  document.addEventListener('click', function(e) {
    if (!popup.contains(e.target) && !e.target.classList.contains('highlight')) {
      window._hidePopup();
    }
  });
})();
</script>
</body>
</html>";
        }
    }
}
