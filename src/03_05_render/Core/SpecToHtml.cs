using System;
using System.Collections.Generic;
using System.Text;
using FourthDevs.Render.Models;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Render.Core
{
    /// <summary>
    /// Deterministic renderer that converts a RenderSpec + state into a full HTML document.
    /// Port of spec-to-html.ts.
    /// </summary>
    internal static class SpecToHtml
    {
        private static readonly Dictionary<string, string> GapMap = new Dictionary<string, string>
        {
            ["sm"] = "8px",
            ["md"] = "12px",
            ["lg"] = "18px",
        };

        private static readonly Dictionary<string, string> AlignMap = new Dictionary<string, string>
        {
            ["start"]   = "flex-start",
            ["center"]  = "center",
            ["end"]     = "flex-end",
            ["stretch"] = "stretch",
        };

        private static readonly Dictionary<string, string> JustifyMap = new Dictionary<string, string>
        {
            ["start"]   = "flex-start",
            ["center"]  = "center",
            ["end"]     = "flex-end",
            ["between"] = "space-between",
        };

        // ─── Public entry point ──────────────────────────────────────────────────

        public static string RenderToHtml(string title, RenderSpec spec, Dictionary<string, object> state)
        {
            if (spec == null || spec.Elements == null)
                return BuildDocument(EscapeHtml(title), "<p>No spec provided.</p>");

            var stack = new HashSet<string>(StringComparer.Ordinal);
            string body = RenderElement(spec.Root, spec.Elements, state ?? new Dictionary<string, object>(), stack, 0);

            return BuildDocument(EscapeHtml(title), body);
        }

        // ─── Core rendering ─────────────────────────────────────────────────────

        private static string RenderElement(
            string elementId,
            Dictionary<string, RenderSpecElement> elements,
            Dictionary<string, object> state,
            HashSet<string> stack,
            int depth)
        {
            if (elementId == null || !elements.ContainsKey(elementId))
                return string.Empty;

            // Guard against cycles
            if (stack.Contains(elementId))
                return string.Format("<span class=\"jr-warning\">[circular ref: {0}]</span>", EscapeHtml(elementId));

            if (depth > 40)
                return "<span class=\"jr-warning\">[max depth reached]</span>";

            RenderSpecElement el = elements[elementId];
            if (el == null)
                return string.Empty;

            stack.Add(elementId);

            // Resolve dynamic props (state references)
            Dictionary<string, object> props = ResolveProps(el.Props, state);

            string html = RenderComponent(el.Type, props, el.Children, elements, state, stack, depth);

            stack.Remove(elementId);
            return html;
        }

        private static string RenderChildren(
            List<string> children,
            Dictionary<string, RenderSpecElement> elements,
            Dictionary<string, object> state,
            HashSet<string> stack,
            int depth)
        {
            if (children == null || children.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (string childId in children)
                sb.Append(RenderElement(childId, elements, state, stack, depth + 1));
            return sb.ToString();
        }

        private static string RenderComponent(
            string type,
            Dictionary<string, object> props,
            List<string> childIds,
            Dictionary<string, RenderSpecElement> elements,
            Dictionary<string, object> state,
            HashSet<string> stack,
            int depth)
        {
            switch (type)
            {
                case "Stack":      return RenderStack(props, childIds, elements, state, stack, depth);
                case "Grid":       return RenderGrid(props, childIds, elements, state, stack, depth);
                case "Card":       return RenderCard(props, childIds, elements, state, stack, depth);
                case "Heading":    return RenderHeading(props);
                case "Text":       return RenderText(props);
                case "Badge":      return RenderBadge(props);
                case "Separator":  return "<hr class=\"jr-separator\" />";
                case "Metric":     return RenderMetric(props);
                case "LineChart":  return RenderLineChart(props);
                case "BarChart":   return RenderBarChart(props);
                case "Table":      return RenderTable(props);
                case "Alert":      return RenderAlert(props);
                case "Callout":    return RenderCallout(props);
                case "Accordion":  return RenderAccordion(props);
                case "Input":      return RenderInput(props);
                case "Select":     return RenderSelect(props);
                case "RadioGroup": return RenderRadioGroup(props);
                case "Switch":     return RenderSwitch(props);
                case "Button":     return RenderButton(props);
                default:
                    return string.Format("<span class=\"jr-warning\">[unknown component: {0}]</span>", EscapeHtml(type ?? "(null)"));
            }
        }

        // ─── Component renderers ─────────────────────────────────────────────────

        private static string RenderStack(
            Dictionary<string, object> props,
            List<string> childIds,
            Dictionary<string, RenderSpecElement> elements,
            Dictionary<string, object> state,
            HashSet<string> stack,
            int depth)
        {
            string direction = GetString(props, "direction", "vertical");
            string gap       = GetString(props, "gap", "md");
            string align     = GetString(props, "align", null);
            string justify   = GetString(props, "justify", null);

            string flexDir = direction == "horizontal" ? "row" : "column";
            string gapPx;
            if (!GapMap.TryGetValue(gap, out gapPx)) gapPx = "12px";

            string alignVal;
            if (align == null || !AlignMap.TryGetValue(align, out alignVal)) alignVal = null;

            string justifyVal;
            if (justify == null || !JustifyMap.TryGetValue(justify, out justifyVal)) justifyVal = null;

            var style = new StringBuilder();
            style.AppendFormat("display:flex;flex-direction:{0};gap:{1};", flexDir, gapPx);
            if (alignVal != null)   style.AppendFormat("align-items:{0};", alignVal);
            if (justifyVal != null) style.AppendFormat("justify-content:{0};", justifyVal);

            string children = RenderChildren(childIds, elements, state, stack, depth);
            return string.Format("<section class=\"jr-stack\" style=\"{0}\">{1}</section>", style, children);
        }

        private static string RenderGrid(
            Dictionary<string, object> props,
            List<string> childIds,
            Dictionary<string, RenderSpecElement> elements,
            Dictionary<string, object> state,
            HashSet<string> stack,
            int depth)
        {
            int columns = GetInt(props, "columns", 2);
            if (columns < 1) columns = 1;
            if (columns > 4) columns = 4;

            string gap   = GetString(props, "gap", "md");
            string gapPx;
            if (!GapMap.TryGetValue(gap, out gapPx)) gapPx = "12px";

            string style = string.Format(
                "display:grid;grid-template-columns:repeat({0},minmax(0,1fr));gap:{1};", columns, gapPx);

            string children = RenderChildren(childIds, elements, state, stack, depth);
            return string.Format("<section class=\"jr-grid\" style=\"{0}\">{1}</section>", style, children);
        }

        private static string RenderCard(
            Dictionary<string, object> props,
            List<string> childIds,
            Dictionary<string, RenderSpecElement> elements,
            Dictionary<string, object> state,
            HashSet<string> stack,
            int depth)
        {
            string title       = GetString(props, "title", null);
            string description = GetString(props, "description", null);
            string children    = RenderChildren(childIds, elements, state, stack, depth);

            var sb = new StringBuilder();
            sb.Append("<section class=\"jr-card\">");
            if (!string.IsNullOrEmpty(title))
                sb.AppendFormat("<h3 class=\"jr-card-title\">{0}</h3>", EscapeHtml(title));
            if (!string.IsNullOrEmpty(description))
                sb.AppendFormat("<p class=\"jr-card-description\">{0}</p>", EscapeHtml(description));
            sb.AppendFormat("<div class=\"jr-card-content\">{0}</div>", children);
            sb.Append("</section>");
            return sb.ToString();
        }

        private static string RenderHeading(Dictionary<string, object> props)
        {
            string text  = GetString(props, "text", string.Empty);
            string level = GetString(props, "level", "h2");

            // Sanitize level to h1..h4
            if (level != "h1" && level != "h2" && level != "h3" && level != "h4")
                level = "h2";

            return string.Format("<{0} class=\"jr-heading\">{1}</{0}>", level, EscapeHtml(text));
        }

        private static string RenderText(Dictionary<string, object> props)
        {
            string content = GetString(props, "content", string.Empty);
            bool muted     = GetBool(props, "muted", false);
            string cls     = muted ? "jr-text jr-text-muted" : "jr-text";
            return string.Format("<p class=\"{0}\">{1}</p>", cls, EscapeHtml(content));
        }

        private static string RenderBadge(Dictionary<string, object> props)
        {
            string text    = GetString(props, "text", string.Empty);
            string variant = GetString(props, "variant", "default");

            string variantCls = string.Empty;
            if (variant == "success" || variant == "warning" || variant == "danger")
                variantCls = " jr-badge-" + variant;

            return string.Format("<span class=\"jr-badge{0}\">{1}</span>", variantCls, EscapeHtml(text));
        }

        private static string RenderMetric(Dictionary<string, object> props)
        {
            string label  = GetString(props, "label", string.Empty);
            string value  = ToDisplay(GetProp(props, "value"));
            string detail = GetString(props, "detail", null);
            string trend  = GetString(props, "trend", null);

            string arrow = string.Empty;
            if (trend == "up")      arrow = "▲ ";
            else if (trend == "down")    arrow = "▼ ";
            else if (trend == "neutral") arrow = "• ";

            var sb = new StringBuilder();
            sb.Append("<article class=\"jr-metric\">");
            sb.AppendFormat("<div class=\"jr-metric-label\">{0}</div>", EscapeHtml(label));
            sb.AppendFormat("<div class=\"jr-metric-value\">{0}</div>", EscapeHtml(value));
            if (detail != null || arrow.Length > 0)
            {
                sb.AppendFormat("<div class=\"jr-metric-detail\">{0}{1}</div>",
                    EscapeHtml(arrow), EscapeHtml(detail ?? string.Empty));
            }
            sb.Append("</article>");
            return sb.ToString();
        }

        private static string RenderLineChart(Dictionary<string, object> props)
        {
            string title  = GetString(props, "title", null);
            string xKey   = GetString(props, "xKey", "x");
            string yKey   = GetString(props, "yKey", "y");
            int height    = GetInt(props, "height", 140);

            List<Dictionary<string, object>> data = GetDataList(props, "data");

            var sb = new StringBuilder();
            sb.Append("<div class=\"jr-chart\">");
            if (!string.IsNullOrEmpty(title))
                sb.AppendFormat("<div class=\"jr-chart-title\">{0}</div>", EscapeHtml(title));

            if (data == null || data.Count == 0)
            {
                sb.Append("<div class=\"jr-chart-empty\">No data</div>");
                sb.Append("</div>");
                return sb.ToString();
            }

            // Compute min/max y for normalization
            double minY = double.MaxValue;
            double maxY = double.MinValue;
            var yValues = new List<double>();
            foreach (var row in data)
            {
                double y = ToDouble(GetRowValue(row, yKey));
                yValues.Add(y);
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            double rangeY = maxY - minY;
            if (rangeY == 0) rangeY = 1;

            int n = data.Count;
            int svgW = 400;
            int svgH = height;
            double padX = 10;
            double padY = 10;

            // Build polyline points
            var points = new StringBuilder();
            for (int i = 0; i < n; i++)
            {
                double x = padX + (n == 1 ? (svgW - 2 * padX) / 2.0 : (double)i / (n - 1) * (svgW - 2 * padX));
                double y = padY + (1 - (yValues[i] - minY) / rangeY) * (svgH - 2 * padY);
                if (i > 0) points.Append(" ");
                points.AppendFormat("{0:F1},{1:F1}", x, y);
            }

            sb.Append("<div class=\"jr-chart-line\">");
            sb.AppendFormat(
                "<svg viewBox=\"0 0 {0} {1}\" preserveAspectRatio=\"none\" style=\"height:{2}px\">",
                svgW, svgH, height);
            sb.AppendFormat(
                "<polyline points=\"{0}\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" />",
                points);
            sb.Append("</svg></div>");

            // Labels
            sb.Append("<div class=\"jr-chart-labels\">");
            foreach (var row in data)
            {
                string label = ToDisplay(GetRowValue(row, xKey));
                sb.AppendFormat("<span>{0}</span>", EscapeHtml(label));
            }
            sb.Append("</div>");

            sb.Append("</div>");
            return sb.ToString();
        }

        private static string RenderBarChart(Dictionary<string, object> props)
        {
            string title = GetString(props, "title", null);
            string xKey  = GetString(props, "xKey", "x");
            string yKey  = GetString(props, "yKey", "y");

            List<Dictionary<string, object>> data = GetDataList(props, "data");

            var sb = new StringBuilder();
            sb.Append("<div class=\"jr-chart\">");
            if (!string.IsNullOrEmpty(title))
                sb.AppendFormat("<div class=\"jr-chart-title\">{0}</div>", EscapeHtml(title));

            if (data == null || data.Count == 0)
            {
                sb.Append("<div class=\"jr-chart-empty\">No data</div>");
                sb.Append("</div>");
                return sb.ToString();
            }

            // Find max for percentage calculation
            double maxY = 0;
            foreach (var row in data)
            {
                double y = ToDouble(GetRowValue(row, yKey));
                if (y > maxY) maxY = y;
            }
            if (maxY == 0) maxY = 1;

            sb.Append("<div class=\"jr-bar-list\">");
            foreach (var row in data)
            {
                string label  = ToDisplay(GetRowValue(row, xKey));
                double yVal   = ToDouble(GetRowValue(row, yKey));
                double pct    = yVal / maxY * 100;
                string valStr = ToDisplay(GetRowValue(row, yKey));

                sb.Append("<div class=\"jr-bar-row\">");
                sb.AppendFormat("<span class=\"jr-bar-label\">{0}</span>", EscapeHtml(label));
                sb.Append("<div class=\"jr-bar-track\">");
                sb.AppendFormat("<div class=\"jr-bar-fill\" style=\"width:{0:F1}%\"></div>", pct);
                sb.Append("</div>");
                sb.AppendFormat("<span class=\"jr-bar-value\">{0}</span>", EscapeHtml(valStr));
                sb.Append("</div>");
            }
            sb.Append("</div>");

            sb.Append("</div>");
            return sb.ToString();
        }

        private static string RenderTable(Dictionary<string, object> props)
        {
            string emptyMsg = GetString(props, "emptyMessage", "No data");

            // columns: [{key, label}]
            List<Dictionary<string, object>> columns = GetDataList(props, "columns");
            List<Dictionary<string, object>> data    = GetDataList(props, "data");

            if (columns == null || columns.Count == 0)
                return string.Format("<div class=\"jr-table-empty\">{0}</div>", EscapeHtml(emptyMsg));

            var sb = new StringBuilder();
            sb.Append("<div class=\"jr-table-wrap\">");
            sb.Append("<table class=\"jr-table\">");

            // Header
            sb.Append("<thead><tr>");
            foreach (var col in columns)
            {
                string colLabel = ToDisplay(GetRowValue(col, "label"));
                sb.AppendFormat("<th>{0}</th>", EscapeHtml(colLabel));
            }
            sb.Append("</tr></thead>");

            // Body
            sb.Append("<tbody>");
            if (data == null || data.Count == 0)
            {
                sb.AppendFormat(
                    "<tr><td colspan=\"{0}\" class=\"jr-table-empty\">{1}</td></tr>",
                    columns.Count, EscapeHtml(emptyMsg));
            }
            else
            {
                foreach (var row in data)
                {
                    sb.Append("<tr>");
                    foreach (var col in columns)
                    {
                        string colKey  = ToDisplay(GetRowValue(col, "key"));
                        string cellVal = ToDisplay(GetRowValue(row, colKey));
                        sb.AppendFormat("<td>{0}</td>", EscapeHtml(cellVal));
                    }
                    sb.Append("</tr>");
                }
            }
            sb.Append("</tbody>");
            sb.Append("</table></div>");
            return sb.ToString();
        }

        private static string RenderAlert(Dictionary<string, object> props)
        {
            string title   = GetString(props, "title", string.Empty);
            string message = GetString(props, "message", null);
            string tone    = GetString(props, "tone", "info");

            string cls = "jr-alert jr-alert-" + tone;
            var sb = new StringBuilder();
            sb.AppendFormat("<section class=\"{0}\">", cls);
            sb.AppendFormat("<strong>{0}</strong>", EscapeHtml(title));
            if (!string.IsNullOrEmpty(message))
                sb.AppendFormat("<p>{0}</p>", EscapeHtml(message));
            sb.Append("</section>");
            return sb.ToString();
        }

        private static string RenderCallout(Dictionary<string, object> props)
        {
            string title   = GetString(props, "title", null);
            string content = GetString(props, "content", string.Empty);
            string type    = GetString(props, "type", "info");

            string cls = "jr-callout jr-callout-" + type;
            var sb = new StringBuilder();
            sb.AppendFormat("<section class=\"{0}\">", cls);
            if (!string.IsNullOrEmpty(title))
                sb.AppendFormat("<strong>{0}</strong>", EscapeHtml(title));
            sb.AppendFormat("<p>{0}</p>", EscapeHtml(content));
            sb.Append("</section>");
            return sb.ToString();
        }

        private static string RenderAccordion(Dictionary<string, object> props)
        {
            // items: [{title, content}]
            List<Dictionary<string, object>> items = GetDataList(props, "items");

            var sb = new StringBuilder();
            sb.Append("<section class=\"jr-accordion\">");
            if (items != null)
            {
                foreach (var item in items)
                {
                    string itemTitle   = ToDisplay(GetRowValue(item, "title"));
                    string itemContent = ToDisplay(GetRowValue(item, "content"));
                    sb.Append("<details class=\"jr-accordion-item\">");
                    sb.AppendFormat("<summary>{0}</summary>", EscapeHtml(itemTitle));
                    sb.AppendFormat("<p>{0}</p>", EscapeHtml(itemContent));
                    sb.Append("</details>");
                }
            }
            sb.Append("</section>");
            return sb.ToString();
        }

        private static string RenderInput(Dictionary<string, object> props)
        {
            string label       = GetString(props, "label", null);
            string value       = GetString(props, "value", string.Empty);
            string placeholder = GetString(props, "placeholder", string.Empty);

            var sb = new StringBuilder();
            sb.Append("<label>");
            if (!string.IsNullOrEmpty(label))
                sb.AppendFormat("<span class=\"jr-input-label\">{0}</span>", EscapeHtml(label));
            sb.AppendFormat(
                "<input class=\"jr-input\" disabled value=\"{0}\" placeholder=\"{1}\" />",
                EscapeHtml(value), EscapeHtml(placeholder));
            sb.Append("</label>");
            return sb.ToString();
        }

        private static string RenderSelect(Dictionary<string, object> props)
        {
            string label = GetString(props, "label", null);
            string value = GetString(props, "value", string.Empty);
            List<Dictionary<string, object>> options = GetDataList(props, "options");

            var sb = new StringBuilder();
            sb.Append("<label>");
            if (!string.IsNullOrEmpty(label))
                sb.AppendFormat("<span class=\"jr-input-label\">{0}</span>", EscapeHtml(label));
            sb.Append("<select class=\"jr-input\" disabled>");
            if (options != null)
            {
                foreach (var opt in options)
                {
                    string optVal   = ToDisplay(GetRowValue(opt, "value"));
                    string optLabel = ToDisplay(GetRowValue(opt, "label"));
                    string sel      = optVal == value ? " selected" : string.Empty;
                    sb.AppendFormat("<option value=\"{0}\"{1}>{2}</option>",
                        EscapeHtml(optVal), sel, EscapeHtml(optLabel));
                }
            }
            sb.Append("</select></label>");
            return sb.ToString();
        }

        private static string RenderRadioGroup(Dictionary<string, object> props)
        {
            string label   = GetString(props, "label", null);
            string value   = GetString(props, "value", string.Empty);
            List<Dictionary<string, object>> options = GetDataList(props, "options");

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(label))
                sb.AppendFormat("<span class=\"jr-input-label\">{0}</span>", EscapeHtml(label));
            sb.Append("<div class=\"jr-radio-group\">");
            if (options != null)
            {
                foreach (var opt in options)
                {
                    string optVal   = ToDisplay(GetRowValue(opt, "value"));
                    string optLabel = ToDisplay(GetRowValue(opt, "label"));
                    string chk      = optVal == value ? " checked" : string.Empty;
                    sb.Append("<label class=\"jr-radio\">");
                    sb.AppendFormat("<input type=\"radio\" disabled{0} />", chk);
                    sb.AppendFormat("<span>{0}</span>", EscapeHtml(optLabel));
                    sb.Append("</label>");
                }
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string RenderSwitch(Dictionary<string, object> props)
        {
            string label   = GetString(props, "label", string.Empty);
            bool   @checked = GetBool(props, "checked", false);
            string pill    = @checked ? "On" : "Off";

            return string.Format(
                "<label class=\"jr-switch\">{0} <span class=\"jr-switch-pill\">{1}</span></label>",
                EscapeHtml(label), pill);
        }

        private static string RenderButton(Dictionary<string, object> props)
        {
            string label   = GetString(props, "label", "Button");
            string variant = GetString(props, "variant", "default");

            string cls = "jr-button";
            if (variant == "secondary") cls += " jr-button-secondary";
            else if (variant == "danger") cls += " jr-button-danger";

            return string.Format("<button class=\"{0}\" disabled>{1}</button>", cls, EscapeHtml(label));
        }

        // ─── State resolution ────────────────────────────────────────────────────

        private static Dictionary<string, object> ResolveProps(
            Dictionary<string, object> props,
            Dictionary<string, object> state)
        {
            if (props == null) return new Dictionary<string, object>();
            var resolved = new Dictionary<string, object>(props.Count);
            foreach (var kv in props)
                resolved[kv.Key] = ResolveDynamic(kv.Value, state);
            return resolved;
        }

        private static object ResolveDynamic(object value, Dictionary<string, object> state)
        {
            if (value == null) return null;

            // JObject: check for $state reference
            var jObj = value as JObject;
            if (jObj != null)
            {
                JToken stateRef;
                if (jObj.TryGetValue("$state", out stateRef))
                {
                    string pointer = stateRef.ToString();
                    return GetByPointer(state, pointer);
                }

                // Recursively resolve all values
                var dict = new Dictionary<string, object>();
                foreach (var prop in jObj.Properties())
                    dict[prop.Name] = ResolveDynamic(prop.Value, state);
                return dict;
            }

            // JArray: resolve each element
            var jArr = value as JArray;
            if (jArr != null)
            {
                var list = new List<object>(jArr.Count);
                foreach (JToken item in jArr)
                    list.Add(ResolveDynamic(item, state));
                return list;
            }

            // JValue: unwrap
            var jVal = value as JValue;
            if (jVal != null)
                return jVal.Value;

            // Dictionary: resolve recursively
            var dictValue = value as Dictionary<string, object>;
            if (dictValue != null)
            {
                var result = new Dictionary<string, object>(dictValue.Count);
                foreach (var kv in dictValue)
                    result[kv.Key] = ResolveDynamic(kv.Value, state);
                return result;
            }

            // List: resolve each element
            var listValue = value as List<object>;
            if (listValue != null)
            {
                var result = new List<object>(listValue.Count);
                foreach (object item in listValue)
                    result.Add(ResolveDynamic(item, state));
                return result;
            }

            return value;
        }

        /// <summary>
        /// Resolves a JSON Pointer (e.g., "/users/0/name") against a root object.
        /// Root can be Dictionary&lt;string,object&gt;, List&lt;object&gt;, JObject, JArray, or primitives.
        /// </summary>
        private static object GetByPointer(object root, string pointer)
        {
            if (string.IsNullOrEmpty(pointer) || pointer == "/")
                return root;

            string[] segments = pointer.Split('/');
            object current = root;

            foreach (string raw in segments)
            {
                if (raw.Length == 0) continue; // skip leading empty from the leading '/'

                // Decode JSON Pointer escape sequences
                string seg = raw.Replace("~1", "/").Replace("~0", "~");

                if (current == null) return null;

                // JObject
                var jObj = current as JObject;
                if (jObj != null)
                {
                    JToken child;
                    if (!jObj.TryGetValue(seg, out child))
                        return null;
                    current = UnwrapToken(child);
                    continue;
                }

                // JArray
                var jArr = current as JArray;
                if (jArr != null)
                {
                    int idx;
                    if (!int.TryParse(seg, out idx) || idx < 0 || idx >= jArr.Count)
                        return null;
                    current = UnwrapToken(jArr[idx]);
                    continue;
                }

                // Dictionary<string, object>
                var dict = current as Dictionary<string, object>;
                if (dict != null)
                {
                    object child;
                    if (!dict.TryGetValue(seg, out child))
                        return null;
                    current = child;
                    continue;
                }

                // List<object>
                var list = current as List<object>;
                if (list != null)
                {
                    int idx;
                    if (!int.TryParse(seg, out idx) || idx < 0 || idx >= list.Count)
                        return null;
                    current = list[idx];
                    continue;
                }

                return null;
            }

            return current;
        }

        private static object UnwrapToken(JToken token)
        {
            if (token == null) return null;
            if (token is JObject) return token;
            if (token is JArray) return token;
            if (token is JValue)
            {
                var jVal = (JValue)token;
                return jVal.Value;
            }
            return token;
        }

        // ─── Prop accessors ──────────────────────────────────────────────────────

        private static object GetProp(Dictionary<string, object> props, string key)
        {
            if (props == null) return null;
            object val;
            props.TryGetValue(key, out val);
            return val;
        }

        private static string GetString(Dictionary<string, object> props, string key, string defaultValue)
        {
            object val = GetProp(props, key);
            if (val == null) return defaultValue;
            return val.ToString();
        }

        private static int GetInt(Dictionary<string, object> props, string key, int defaultValue)
        {
            object val = GetProp(props, key);
            if (val == null) return defaultValue;
            int result;
            if (int.TryParse(val.ToString(), out result)) return result;
            return defaultValue;
        }

        private static bool GetBool(Dictionary<string, object> props, string key, bool defaultValue)
        {
            object val = GetProp(props, key);
            if (val == null) return defaultValue;
            if (val is bool) return (bool)val;
            string s = val.ToString().ToLowerInvariant();
            if (s == "true" || s == "1") return true;
            if (s == "false" || s == "0") return false;
            return defaultValue;
        }

        /// <summary>
        /// Gets a list of row objects from props. Handles List&lt;object&gt; from ResolveDynamic.
        /// </summary>
        private static List<Dictionary<string, object>> GetDataList(Dictionary<string, object> props, string key)
        {
            object val = GetProp(props, key);
            if (val == null) return null;

            // Already a List<object> (from ResolveDynamic)
            var listObj = val as List<object>;
            if (listObj != null)
            {
                var result = new List<Dictionary<string, object>>(listObj.Count);
                foreach (object item in listObj)
                    result.Add(ToRowDict(item));
                return result;
            }

            // JArray (not yet resolved)
            var jArr = val as JArray;
            if (jArr != null)
            {
                var result = new List<Dictionary<string, object>>(jArr.Count);
                foreach (JToken item in jArr)
                    result.Add(ToRowDict(UnwrapToken(item)));
                return result;
            }

            return null;
        }

        private static Dictionary<string, object> ToRowDict(object item)
        {
            if (item == null) return new Dictionary<string, object>();

            var dict = item as Dictionary<string, object>;
            if (dict != null) return dict;

            var jObj = item as JObject;
            if (jObj != null)
            {
                var result = new Dictionary<string, object>();
                foreach (var prop in jObj.Properties())
                    result[prop.Name] = UnwrapToken(prop.Value);
                return result;
            }

            return new Dictionary<string, object>();
        }

        private static object GetRowValue(Dictionary<string, object> row, string key)
        {
            if (row == null || key == null) return null;
            object val;
            row.TryGetValue(key, out val);
            return val;
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private static string EscapeHtml(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? string.Empty;
            return s
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }

        private static string ToDisplay(object value)
        {
            if (value == null) return string.Empty;
            if (value is bool) return (bool)value ? "true" : "false";
            return value.ToString();
        }

        private static double ToDouble(object value)
        {
            if (value == null) return 0;
            double d;
            if (double.TryParse(value.ToString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out d))
                return d;
            return 0;
        }

        // ─── Document wrapper ─────────────────────────────────────────────────────

        private static string BuildDocument(string escapedTitle, string body)
        {
            return string.Format(
@"<!doctype html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
  <title>{0}</title>
  <style>{1}</style>
</head>
<body>{2}</body>
</html>",
                escapedTitle,
                BuildStyles(),
                body);
        }

        private static string BuildStyles()
        {
            return @"
:root { color-scheme: light; --bg: #ffffff; --text: #0f172a; --muted: #475569; --panel: #f8fafc; --panel-border: #dbe3f0; --accent: #2563eb; --ok: #128a49; --warn: #a45800; --danger: #ba1f2f; }
* { box-sizing: border-box; }
body { margin: 0; padding: 14px; font-family: Inter, ui-sans-serif, system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif; color: var(--text); background: var(--bg); }
.jr-stack { min-width: 0; }
.jr-grid { min-width: 0; }
.jr-card { border: 1px solid var(--panel-border); border-radius: 12px; background: var(--panel); padding: 12px; }
.jr-card-title { margin: 0 0 5px; font-size: 14px; }
.jr-card-description { margin: 0 0 10px; color: var(--muted); font-size: 12px; }
.jr-card-content { min-width: 0; }
.jr-heading { margin: 0 0 6px; line-height: 1.2; }
.jr-text { margin: 0; line-height: 1.45; }
.jr-text-muted { color: var(--muted); }
.jr-badge { display: inline-flex; align-items: center; border-radius: 999px; padding: 2px 8px; font-size: 11px; font-weight: 600; background: #e2e8f0; }
.jr-badge-success { background: #dcfce7; color: #14532d; }
.jr-badge-warning { background: #fef3c7; color: #78350f; }
.jr-badge-danger { background: #fee2e2; color: #7f1d1d; }
.jr-separator { border: none; border-top: 1px solid var(--panel-border); margin: 6px 0; }
.jr-metric { border: 1px solid var(--panel-border); border-radius: 10px; background: white; padding: 10px; }
.jr-metric-label { font-size: 12px; color: var(--muted); }
.jr-metric-value { margin-top: 4px; font-size: 24px; font-weight: 700; }
.jr-metric-detail { margin-top: 4px; font-size: 12px; color: var(--muted); }
.jr-chart { border: 1px solid var(--panel-border); border-radius: 10px; background: white; padding: 10px; }
.jr-chart-title { font-size: 12px; color: var(--muted); margin-bottom: 8px; }
.jr-chart-line svg { width: 100%; height: 140px; color: var(--accent); }
.jr-chart-labels { margin-top: 6px; display: grid; grid-template-columns: repeat(auto-fit, minmax(50px, 1fr)); gap: 4px; font-size: 11px; color: var(--muted); }
.jr-bar-list { display: grid; gap: 8px; }
.jr-bar-row { display: grid; grid-template-columns: minmax(80px, 1fr) 3fr auto; gap: 8px; align-items: center; }
.jr-bar-track { border-radius: 999px; background: #e2e8f0; overflow: hidden; min-height: 10px; }
.jr-bar-fill { height: 10px; background: var(--accent); }
.jr-bar-label, .jr-bar-value { font-size: 12px; color: var(--muted); }
.jr-chart-empty { font-size: 12px; color: var(--muted); }
.jr-table-wrap { border: 1px solid var(--panel-border); border-radius: 10px; overflow: auto; }
.jr-table { width: 100%; border-collapse: collapse; }
.jr-table th, .jr-table td { border-bottom: 1px solid var(--panel-border); padding: 8px; text-align: left; font-size: 12px; }
.jr-table th { background: #f1f5f9; font-size: 11px; letter-spacing: 0.02em; text-transform: uppercase; }
.jr-table-empty { font-size: 12px; color: var(--muted); }
.jr-alert, .jr-callout { border: 1px solid var(--panel-border); border-radius: 10px; padding: 10px; }
.jr-alert p, .jr-callout p { margin: 4px 0 0; }
.jr-alert-warning, .jr-callout-warning { border-color: #f59e0b; background: #fffbeb; color: #7c2d12; }
.jr-alert-danger, .jr-callout-important { border-color: #ef4444; background: #fef2f2; color: #7f1d1d; }
.jr-alert-success { border-color: #22c55e; background: #f0fdf4; color: #14532d; }
.jr-alert-info, .jr-callout-info, .jr-callout-tip { border-color: #60a5fa; background: #eff6ff; color: #1e3a8a; }
.jr-accordion { border: 1px solid var(--panel-border); border-radius: 10px; overflow: hidden; }
.jr-accordion-item + .jr-accordion-item { border-top: 1px solid var(--panel-border); }
.jr-accordion-item summary { cursor: pointer; padding: 8px 10px; font-weight: 600; }
.jr-accordion-item p { margin: 0; padding: 0 10px 10px; color: var(--muted); }
.jr-input-label { display: block; margin-bottom: 4px; font-size: 12px; color: var(--muted); }
.jr-input { width: 100%; border: 1px solid var(--panel-border); border-radius: 8px; padding: 8px; font-size: 12px; background: white; color: var(--text); }
.jr-radio-group { display: grid; gap: 6px; }
.jr-radio { display: inline-flex; align-items: center; gap: 6px; font-size: 12px; }
.jr-switch { display: inline-flex; align-items: center; gap: 8px; font-size: 12px; }
.jr-switch-pill { border-radius: 999px; border: 1px solid var(--panel-border); padding: 2px 7px; }
.jr-button { border: 1px solid var(--panel-border); border-radius: 8px; padding: 7px 12px; background: #f8fafc; color: var(--text); font-size: 12px; }
.jr-warning { border-radius: 8px; padding: 6px 8px; font-size: 12px; background: #fef9c3; color: #854d0e; }
";
        }
    }
}
