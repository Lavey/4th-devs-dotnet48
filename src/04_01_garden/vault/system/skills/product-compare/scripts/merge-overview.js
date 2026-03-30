const asObject = (value, field) => {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    throw new Error(`${field} must be an object.`);
  }
  return value;
};

const asString = (value, field) => {
  if (typeof value !== "string") {
    throw new Error(`${field} must be a string.`);
  }
  const trimmed = value.trim();
  if (!trimmed) {
    throw new Error(`${field} cannot be empty.`);
  }
  return trimmed;
};

const slugToTitle = (slug) =>
  slug
    .split(/[-_]/g)
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(" ");

const normalizeTopic = (value) => {
  const raw = asString(value, "input.topic").toLowerCase();
  const normalized = raw
    .replace(/\s+/g, "-")
    .replace(/[^a-z0-9/_-]+/g, "-")
    .replace(/\/+/g, "/")
    .replace(/^-+|-+$/g, "");

  if (!normalized || normalized === "." || normalized === "..") {
    throw new Error("input.topic resolved to an invalid path.");
  }

  if (
    normalized.startsWith("/") ||
    normalized.startsWith("../") ||
    normalized.includes("/../")
  ) {
    throw new Error("input.topic must be a safe relative path segment.");
  }

  return normalized;
};

const findHeadingTitle = (content) => {
  const match = content.match(/^#\s+(.+)$/m);
  return match ? match[1].trim() : undefined;
};

const getSectionBody = (content, heading) => {
  const lines = content.replace(/\r\n/g, "\n").split("\n");
  const headingRe = new RegExp(`^##\\s+${heading}\\s*$`, "i");
  const sectionStart = lines.findIndex((line) => headingRe.test(line.trim()));
  if (sectionStart === -1) return "";

  const bodyLines = [];
  for (var i = sectionStart + 1; i < lines.length; i++) {
    if (/^##\s+/.test(lines[i].trim())) break;
    bodyLines.push(lines[i]);
  }
  return bodyLines.join("\n").trim();
};

const extractUrls = (content) => {
  const matches = content.match(/https?:\/\/[^\s)]+/g);
  return matches ? matches.filter(function(v, i, a) { return a.indexOf(v) === i; }) : [];
};

const stripLeadingH1 = (content) => {
  const lines = content.replace(/\r\n/g, "\n").split("\n");
  if (lines.length === 0) return content;

  var firstNonEmpty = -1;
  for (var i = 0; i < lines.length; i++) {
    if (lines[i].trim().length > 0) {
      firstNonEmpty = i;
      break;
    }
  }

  if (firstNonEmpty === -1) return content;
  if (!/^#\s+/.test(lines[firstNonEmpty])) return content;

  var next = lines.slice(0, firstNonEmpty).concat(lines.slice(firstNonEmpty + 1));
  while (next.length > 0 && next[0].trim() === "") next.shift();
  return next.join("\n");
};

const missingInfoCount = (content) => {
  const matches = content.match(
    /Not found in reviewed sources as of|Conflicting information across sources; verification required\./gi,
  );
  return matches ? matches.length : 0;
};

const inputObj = asObject(input, "input");
const topic = normalizeTopic(inputObj.topic);
const topicDir = "vault/research/" + topic;
const overviewPath = topicDir + "/overview.md";
const today = new Date().toISOString().slice(0, 10);

var entries;
try {
  entries = codemode.vault.list(topicDir);
} catch (error) {
  var message = error instanceof Error ? error.message : String(error);
  throw new Error(
    "Unable to open topic folder \"" + topicDir + "\". Ensure it exists and contains product notes. (" + message + ")",
  );
}

var productFiles = entries
  .filter(function(entry) { return !entry.is_dir; })
  .map(function(entry) { return entry.name; })
  .filter(function(name) {
    return name.endsWith(".md") && name !== "overview.md" && !name.startsWith(".");
  })
  .sort(function(a, b) { return a < b ? -1 : a > b ? 1 : 0; });

if (productFiles.length < 2) {
  throw new Error(
    "Expected at least 2 product markdown files in \"" + topicDir + "\". Create <product>.md files first.",
  );
}

if (productFiles.length > 3) {
  throw new Error(
    "Found " + productFiles.length + " product files in \"" + topicDir + "\", but this skill supports at most 3. Remove extra files and retry.",
  );
}

var mergedSections = [];
var productNames = [];
var totalMissingMentions = 0;

for (var fi = 0; fi < productFiles.length; fi++) {
  var fileName = productFiles[fi];
  var filePath = topicDir + "/" + fileName;
  var content = codemode.vault.read(filePath);
  var cleaned = stripLeadingH1(content).trim();
  var fallbackName = slugToTitle(fileName.replace(/\.md$/i, ""));
  var productName = findHeadingTitle(content) || fallbackName;
  var sourcesSection = getSectionBody(content, "Sources");
  var sourceUrls = extractUrls(sourcesSection);
  if (sourceUrls.length === 0) {
    throw new Error(
      filePath + " must include at least one real URL in the \"## Sources\" section before overview merge.",
    );
  }
  var missingMentions = missingInfoCount(content);

  productNames.push(productName);
  totalMissingMentions += missingMentions;

  mergedSections.push(
    [
      "## " + productName,
      "",
      "Source file: `" + filePath + "`",
      "Source URLs detected: " + sourceUrls.length,
      "",
      cleaned || "_No content found in this product file._",
      "",
    ].join("\n"),
  );
}

var frontmatter = [
  "---",
  "title: " + JSON.stringify(slugToTitle(topic) + " - Research Overview"),
  "date: " + JSON.stringify(today),
  "topic: " + JSON.stringify(topic),
  "product_count: " + productFiles.length,
  "missing_information_mentions: " + totalMissingMentions,
  "---",
  "",
].join("\n");

var body = [
  "# " + slugToTitle(topic) + " overview",
  "",
  "This overview merges product research notes from `" + topicDir + "`.",
  "",
  "## Products covered",
].concat(productNames.map(function(name) { return "- " + name; })).concat([
  "",
  "## Missing information summary",
  totalMissingMentions > 0
    ? "Detected " + totalMissingMentions + " explicit missing/conflicting-information note(s) across product files."
    : "No explicit missing/conflicting-information notes were detected in product files.",
  "",
  "## Merged product notes",
  "",
  mergedSections.join("\n---\n\n"),
  "",
]).join("\n");

var saved = codemode.vault.write(overviewPath, frontmatter + body);

codemode.output.set({
  path: saved.path,
  bytes_written: saved.bytes_written,
  topic: topic,
  merged_files: productFiles.map(function(name) { return topicDir + "/" + name; }),
  merged_count: productFiles.length,
  missing_information_mentions: totalMissingMentions,
});
