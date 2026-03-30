const asObject = (value, field) => {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    throw new Error(`${field} must be an object.`);
  }
  return value;
};

const asNonEmptyString = (value, field) => {
  if (typeof value !== "string") {
    throw new Error(`${field} must be a string.`);
  }
  const trimmed = value.trim();
  if (!trimmed) {
    throw new Error(`${field} cannot be empty.`);
  }
  return trimmed;
};

const payload = asObject(input, "input");
const path = asNonEmptyString(payload.path, "input.path");
const findLine = asNonEmptyString(payload.find_line, "input.find_line");
const replaceLine = asNonEmptyString(payload.replace_line, "input.replace_line");
const insertAfterLine = asNonEmptyString(payload.insert_after_line, "input.insert_after_line");

var original = codemode.vault.read(path);
var normalized = original.replace(/\r\n/g, "\n");
var hasTrailingNewline = normalized.endsWith("\n");
var lines = normalized.split("\n");

var targetIndex = -1;
for (var i = 0; i < lines.length; i++) {
  if (lines[i] === findLine) {
    targetIndex = i;
    break;
  }
}
if (targetIndex === -1) {
  throw new Error("Line not found in " + path + ": \"" + findLine + "\"");
}

var updatedLines = lines.slice(0, targetIndex)
  .concat([replaceLine, insertAfterLine])
  .concat(lines.slice(targetIndex + 1));

var updated = updatedLines.join("\n");
if (hasTrailingNewline && !updated.endsWith("\n")) {
  updated += "\n";
}

var saved = codemode.vault.write(path, updated);

codemode.output.set({
  status: "updated",
  path: saved.path,
  bytes_written: saved.bytes_written,
  replaced_index: targetIndex + 1,
  inserted_index: targetIndex + 2,
});
