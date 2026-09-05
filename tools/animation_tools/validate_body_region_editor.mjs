import fs from "node:fs";
import path from "node:path";
import url from "node:url";

const scriptDirectory = path.dirname(url.fileURLToPath(import.meta.url));
const root = path.resolve(scriptDirectory, "..", "..");
const editorPath = path.join(root, "候选素材_官方", "身体区域描边编辑器.html");
const html = fs.readFileSync(editorPath, "utf8");

const scriptBlocks = [...html.matchAll(/<script>([\s\S]*?)<\/script>/g)];
if (scriptBlocks.length !== 1) {
  throw new Error("Expected one inline script, found " + scriptBlocks.length + ".");
}
new Function(scriptBlocks[0][1]);

const declaredIds = new Set(
  [...html.matchAll(/id="([^"]+)"/g)].map((match) => match[1]),
);
const referencedIds = new Set(
  [...scriptBlocks[0][1].matchAll(/getElementById\("([^"]+)"\)/g)]
    .map((match) => match[1]),
);
for (const id of referencedIds) {
  if (!declaredIds.has(id)) {
    throw new Error("Script references missing element id: " + id);
  }
}

const requiredRegions = [
  "LeftEye",
  "RightEye",
  "Mouth",
  "Face",
  "LeftHand",
  "RightHand",
  "Chest",
  "LowerBodySensitiveArea",
  "LeftFoot",
  "RightFoot",
  "HeadAndHair",
  "OtherBody",
];
for (const region of requiredRegions) {
  if (!scriptBlocks[0][1].includes('id: "' + region + '"')) {
    throw new Error("Missing body region: " + region);
  }
}

const presetSources = [
  "assets/animations/processed/用户提供_新全身_透明.png",
  "assets/animations/processed/用户提供_Q版小人全身_透明.png",
  "assets/animations/processed/用户提供_Q版小人全身_经典_透明.png",
];
for (const source of presetSources) {
  if (!fs.existsSync(path.join(root, source))) {
    throw new Error("Missing preset image: " + source);
  }
}

console.log(JSON.stringify({
  editorPath: path.relative(root, editorPath).replaceAll("\\", "/"),
  bytes: Buffer.byteLength(html),
  elementIds: declaredIds.size,
  referencedIds: referencedIds.size,
  regionCount: requiredRegions.length,
  presetCount: presetSources.length,
  scriptSyntax: "ok",
}, null, 2));
