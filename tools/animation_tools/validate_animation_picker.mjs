#!/usr/bin/env node

import fs from "node:fs";
import vm from "node:vm";
import path from "node:path";

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
  "按模式查看正式动画",
  "三个模式通用",
  "全部素材与候选",
  "尚未删除任何素材",
]) {
  assert(html.includes(expected), `missing required text: ${expected}`);
}
for (const stale of ["保留这个动画", "只看已保留", "勾选保留的动画"]) {
  assert(!html.includes(stale), `stale keep-list text remains: ${stale}`);
}

const current = executePicker();
for (const item of current.items) {
  assert(fs.existsSync(path.resolve(path.dirname(pickerPath), item.src)), `missing preview: ${item.id}: ${item.src}`);
}
for (const [id, suffix] of Object.entries({
  requested_birthday_fishing: "十周年生日_摸鱼一分钟_透明无损.webp",
  requested_newyear_oneclick_sing: "元旦祝福_一键唱歌_无缝循环.gif",
  e_music: "心律共鸣_享受音乐_无缝_0.5x.gif",
  e_expand: "resonance-expand.frames.webp",
  new_headpat_guoyue: "guoyue-headpat.frames.webp",
  crystal_sleep: "crystal-long-idle-sleep.frames.webp"
})) assert(current.items.find(item => item.id === id).src.endsWith(suffix), `stale preview: ${id}`);
assert(html.includes("0.1.0.65") && html.includes("不再闪光换图"), "missing latest music rules");
assert(!current.items.find(item => item.id === "p_loading").motion, "launch wait must remain still");
assert(current.elements.get("deletionCount").textContent === 0, "new deletion list must start empty");
assert(
  current.elements.get("usedOverviewTitle").textContent.includes("模式一"),
  "picker must default to mode one",
);
assert(
  current.elements.get("usedOverviewList").innerHTML.includes("华裳长发") &&
    !current.elements.get("usedOverviewList").innerHTML.includes("晶蓝礼服互动 · 遮眼睛"),
  "mode one must show its own idle animation and hide mode-two interactions",
);
assert(current.elements.get("grid").innerHTML.includes("data-delete="), "cards must render delete checkboxes");
assert(!current.elements.get("grid").innerHTML.includes("data-keep="), "legacy keep checkboxes must not render");
assert(
  ["华裳长发版", "晶蓝礼服版", "经典猫耳版"].every(name =>
    current.elements.get("modeOverviewList").innerHTML.includes(name)),
  "mode overview must render all three appearance profiles",
);
assert(
  current.elements.get("modeOverviewList").innerHTML.includes("三个模式通用") &&
    current.elements.get("modeOverviewList").innerHTML.includes('aria-pressed="true"'),
  "mode selector must expose the shared view and active selection",
);

const modeTwo = executePicker(null, "mode2");
assert(
  modeTwo.elements.get("usedOverviewList").innerHTML.includes("晶蓝礼服互动 · 遮眼睛") &&
    modeTwo.elements.get("usedOverviewList").innerHTML.includes("晶蓝礼服长待机 · 睡觉") &&
    !modeTwo.elements.get("usedOverviewList").innerHTML.includes("经典猫耳版拖拽"),
  "mode two must show crystal interactions and hide mode-three-only rules",
);
const modeThree = executePicker(null, "mode3");
assert(
  modeThree.elements.get("usedOverviewList").innerHTML.includes("十周年 · 旋转舞") &&
    modeThree.elements.get("usedOverviewList").innerHTML.includes("心律共鸣 · 嘿嘿") &&
    !modeThree.elements.get("usedOverviewList").innerHTML.includes("晶蓝礼服互动 · 遮眼睛"),
  "mode three must show classic interactions and hide mode-two-only rules",
);
const shared = executePicker(null, "shared");
for (const id of ["sleep_zzz", "sleep_bun", "sleep_ling", "sleep_dissolve"]) {
  const title = current.items.find(item => item.id === id).title;
  assert(modeTwo.elements.get("usedOverviewList").innerHTML.includes(title), `missing mode-two decoration ${id}`);
  for (const other of [current, modeThree, shared])
    assert(!other.elements.get("usedOverviewList").innerHTML.includes(title), `decoration leaks to another mode ${id}`);
}
for (const title of ["心律共鸣 · 给我", "心律共鸣 · 大成功"]) {
  assert(!current.elements.get("usedOverviewList").innerHTML.includes(title),
    `mode one must not expose disabled file-drop animation: ${title}`);
  assert(!shared.elements.get("usedOverviewList").innerHTML.includes(title),
    `file-drop animations are no longer shared by all three modes: ${title}`);
  for (const mode of [modeTwo, modeThree]) {
    assert(mode.elements.get("usedOverviewList").innerHTML.includes(title),
      `modes two and three must retain file-drop animation: ${title}`);
  }
  assert(current.elements.get("grid").innerHTML.includes(title),
    `shared source assets must remain in the full candidate library: ${title}`);
}
assert(
  shared.elements.get("usedOverviewList").innerHTML.includes("代号洛天依 · 好奇摇摆") &&
    shared.elements.get("usedOverviewList").innerHTML.includes("持续显示 30 秒") &&
    !shared.elements.get("usedOverviewList").innerHTML.includes("Q版小人全身待机"),
  "shared view must contain the long message reminder but no mode-specific idle",
);
for (const removedId of [
  "thumb10", "thumbCode", "file_run_preview", "file_eat_preview",
  "file_full_flow_preview", "new_headpat_orange", "p_wake", "p_launch",
  "p_wink", "p_land", "p_goodjob", "p_lowbattery",
]) {
  assert(!itemIds.includes(removedId), `removed preview remains: ${removedId}`);
}

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
      modeCounts: {
        mode1: Number(current.elements.get("usedOverviewCount").textContent),
        mode2: Number(modeTwo.elements.get("usedOverviewCount").textContent),
        mode3: Number(modeThree.elements.get("usedOverviewCount").textContent),
        shared: Number(shared.elements.get("usedOverviewCount").textContent),
      },
      deletionDefaultCount: 0,
      legacyMigration: "keeps choices/scenarios and clears deletion list",
    },
    null,
    2,
  ),
);

function executePicker(legacyState = null, selectedMode = null) {
  const elements = new Map();
  const storage = new Map();
  if (legacyState) {
    storage.set("luotianyi-pet-animation-picker-state-v3", JSON.stringify(legacyState));
  }
  if (selectedMode) {
    storage.set("luotianyi-pet-animation-picker-mode-v1", selectedMode);
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

  vm.runInNewContext(scriptBlocks[0] + "\n;globalThis.validatedItems = items;", context, { filename: pickerPath });
  return { elements, storage, items: context.validatedItems };
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}
