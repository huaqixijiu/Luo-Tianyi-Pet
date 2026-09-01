#!/usr/bin/env node

import fs from "node:fs";
import vm from "node:vm";

const pickerPath = process.argv[2] || "候选素材_官方/动画挑选器.html";
const html = fs.readFileSync(pickerPath, "utf8");
const scriptBlocks = [...html.matchAll(/<script>([\s\S]*?)<\/script>/g)].map(match => match[1]);
assert(scriptBlocks.length === 1, `expected 1 inline script, found ${scriptBlocks.length}`);
new vm.Script(scriptBlocks[0], { filename: pickerPath });

const itemIds = [...html.matchAll(/\{ id:"([^"]+)"/g)].map(match => match[1]);
const usageBlock = html.match(/const activeUsage = \{([\s\S]*?)\n    \};/)?.[1] || "";
const usedIds = [...usageBlock.matchAll(/^\s+([A-Za-z0-9_]+):/gm)].map(match => match[1]);
assert(new Set(itemIds).size === itemIds.length, "animation item IDs must be unique");
assert(usedIds.every(id => itemIds.includes(id)), "every active usage must reference an existing item");

for (const expected of [
  "删掉这个动画",
  "只看待删除",
  "目前已经使用的动画与位置",
  "尚未删除任何素材",
]) {
  assert(html.includes(expected), `missing required text: ${expected}`);
}
for (const stale of ["保留这个动画", "只看已保留", "勾选保留的动画"]) {
  assert(!html.includes(stale), `stale keep-list text remains: ${stale}`);
}

const current = executePicker();
assert(current.elements.get("deletionCount").textContent === 0, "new deletion list must start empty");
assert(
  Number(current.elements.get("usedOverviewCount").textContent) === usedIds.length,
  "used overview count must match active usage map",
);
assert(
  current.elements.get("usedOverviewList").innerHTML.includes("当前用途") === false &&
    current.elements.get("usedOverviewList").innerHTML.includes("完整全身互动"),
  "used overview must render real trigger descriptions",
);
assert(current.elements.get("grid").innerHTML.includes("data-delete="), "cards must render delete checkboxes");
assert(!current.elements.get("grid").innerHTML.includes("data-keep="), "legacy keep checkboxes must not render");

const legacyState = {
  version: 3,
  choices: { idle: "e_hehe" },
  scenarioChoices: ["audioSession"],
  keptItems: ["e_hehe", "dance9"],
};
const migrated = executePicker(legacyState);
const migratedState = JSON.parse(migrated.storage.get("luotianyi-pet-animation-picker-state-v4"));
assert(migratedState.version === 4, "legacy state must migrate to version 4");
assert(migratedState.choices.idle === "e_hehe", "legacy role choices must be retained");
assert(migratedState.scenarioChoices.includes("audioSession"), "legacy scenarios must be retained");
assert(migratedState.deletionItems.length === 0, "legacy kept items must never become deletion items");

console.log(
  JSON.stringify(
    {
      pickerPath,
      itemCount: itemIds.length,
      usedCount: usedIds.length,
      deletionDefaultCount: 0,
      legacyMigration: "keeps choices/scenarios and clears deletion list",
    },
    null,
    2,
  ),
);

function executePicker(legacyState = null) {
  const elements = new Map();
  const storage = new Map();
  if (legacyState) {
    storage.set("luotianyi-pet-animation-picker-state-v3", JSON.stringify(legacyState));
  }

  class FakeElement {
    constructor(id) {
      this.id = id;
      this.innerHTML = "";
      this.textContent = "";
      this.value = "";
      this.disabled = false;
      this.style = {};
      this.dataset = {};
      this.classList = { add() {}, remove() {} };
    }

    addEventListener() {}
    setAttribute() {}
    querySelectorAll() { return []; }
    showModal() {}
    close() {}
    focus() {}
    select() {}
    click() {}
  }

  const getElementById = id => {
    if (!elements.has(id)) {
      const element = new FakeElement(id);
      if (id === "category") element.value = "all";
      elements.set(id, element);
    }
    return elements.get(id);
  };

  const context = {
    document: {
      getElementById,
      createElement: tag => new FakeElement(tag),
      execCommand: () => true,
    },
    localStorage: {
      getItem: key => storage.get(key) ?? null,
      setItem: (key, value) => storage.set(key, value),
    },
    navigator: { clipboard: { writeText: async () => {} } },
    Blob: class {},
    URL: { createObjectURL: () => "blob:test", revokeObjectURL() {} },
    confirm: () => true,
    setTimeout: () => 0,
    clearTimeout() {},
    console,
  };

  vm.runInNewContext(scriptBlocks[0], context, { filename: pickerPath });
  return { elements, storage };
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}
