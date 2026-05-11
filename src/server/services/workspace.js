const fs = require('fs');
const path = require('path');
const os = require('os');

const CONFIG_DIR = path.join(os.homedir(), '.skillmanager');
const CONFIG_PATH = path.join(CONFIG_DIR, 'config.json');

function ensureConfigDir() {
  if (!fs.existsSync(CONFIG_DIR)) {
    fs.mkdirSync(CONFIG_DIR, { recursive: true });
  }
}

function getDefaultWorkspacePath() {
  return path.join(process.cwd(), 'library');
}

function readConfig() {
  ensureConfigDir();
  if (!fs.existsSync(CONFIG_PATH)) {
    const defaultConfig = { workspacePath: getDefaultWorkspacePath() };
    fs.writeFileSync(CONFIG_PATH, JSON.stringify(defaultConfig, null, 2), 'utf-8');
    return defaultConfig;
  }
  try {
    return JSON.parse(fs.readFileSync(CONFIG_PATH, 'utf-8'));
  } catch {
    return { workspacePath: getDefaultWorkspacePath() };
  }
}

function writeConfig(config) {
  ensureConfigDir();
  fs.writeFileSync(CONFIG_PATH, JSON.stringify(config, null, 2), 'utf-8');
}

function getWorkspacePath() {
  const cfg = readConfig();
  return cfg.workspacePath || getDefaultWorkspacePath();
}

function setWorkspacePath(newPath) {
  const absPath = path.resolve(newPath);
  writeConfig({ workspacePath: absPath });
  ensureWorkspace(absPath);
  return absPath;
}

function ensureWorkspace(workspacePath) {
  if (!fs.existsSync(workspacePath)) {
    fs.mkdirSync(workspacePath, { recursive: true });
  }
  const agentsSkills = path.join(workspacePath, '.agents', 'skills');
  if (!fs.existsSync(agentsSkills)) {
    fs.mkdirSync(agentsSkills, { recursive: true });
  }
  // 生成翻译指导 skill
  const guidePath = path.join(agentsSkills, 'SKILL.md');
  if (!fs.existsSync(guidePath)) {
    generateTranslationGuide(workspacePath);
  }
}

function generateTranslationGuide(workspacePath) {
  const guidePath = path.join(workspacePath, '.agents', 'skills', 'SKILL.md');
  const content = `---
description: 指导外部 Agent 批量翻译仓库中的技能文件
---
# 技能库翻译指导

## 概述

本文件由 SkillManager 自动生成，用于指导外部 AI Agent 对仓库中未翻译的技能进行批量中文翻译。

## 当前任务

1. 读取仓库的 \`.translation_index.json\`，确认哪些技能尚未翻译。
2. 对每个未翻译的技能，读取其 \`SKILL.md\`。
3. 将内容翻译为中文，写入同目录的 \`SKILL.zh.md\`。
4. \`SKILL.zh.md\` 的格式应与原文保持一致（frontmatter、标题层级、段落结构）。

## 翻译规范

- 保持 frontmatter 的键名不变，值翻译为中文。
- 一级标题（#）保持原技能名或翻译为对应中文名。
- 正文段落使用流畅、简洁的中文。
- 代码块、路径、文件名等技术标识符保持原样。

## 状态文件

翻译完成后，SkillManager 会自动检测 \`SKILL.zh.md\` 的存在并更新翻译状态。
你无需手动修改任何索引文件。
`;
  fs.writeFileSync(guidePath, content, 'utf-8');
}

function migrateWorkspace(newPath) {
  const oldPath = getWorkspacePath();
  const absNew = path.resolve(newPath);
  if (oldPath === absNew) return absNew;

  if (!fs.existsSync(absNew)) {
    fs.mkdirSync(absNew, { recursive: true });
  }

  // 复制旧工作区内容到新路径
  const entries = fs.readdirSync(oldPath, { withFileTypes: true });
  for (const entry of entries) {
    const src = path.join(oldPath, entry.name);
    const dest = path.join(absNew, entry.name);
    if (entry.isDirectory()) {
      copyDir(src, dest);
    } else {
      fs.copyFileSync(src, dest);
    }
  }

  setWorkspacePath(absNew);
  return absNew;
}

function copyDir(src, dest) {
  if (!fs.existsSync(dest)) {
    fs.mkdirSync(dest, { recursive: true });
  }
  const entries = fs.readdirSync(src, { withFileTypes: true });
  for (const entry of entries) {
    const s = path.join(src, entry.name);
    const d = path.join(dest, entry.name);
    if (entry.isDirectory()) {
      copyDir(s, d);
    } else {
      fs.copyFileSync(s, d);
    }
  }
}

module.exports = {
  readConfig,
  writeConfig,
  getWorkspacePath,
  setWorkspacePath,
  ensureWorkspace,
  migrateWorkspace,
  generateTranslationGuide,
};
