const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const { parseSkillMd } = require('./skillParser');
const { getWorkspacePath, ensureWorkspace } = require('./workspace');

function getLibraryIndexPath() {
  return path.join(getWorkspacePath(), '.library_index.json');
}

function getTranslationIndexPath() {
  return path.join(getWorkspacePath(), '.translation_index.json');
}

function readLibraryIndex() {
  const p = getLibraryIndexPath();
  if (!fs.existsSync(p)) return { version: 1, lastUpdated: new Date().toISOString(), skills: [] };
  try {
    const index = JSON.parse(fs.readFileSync(p, 'utf-8'));
    let changed = false;
    for (const sk of index.skills) {
      if (!sk.contentHash && fs.existsSync(sk.skillMdPath)) {
        const content = fs.readFileSync(sk.skillMdPath, 'utf-8');
        sk.contentHash = computeHash(content);
        changed = true;
      }
    }
    if (changed) {
      writeLibraryIndex(index);
    }
    return index;
  } catch {
    return { version: 1, lastUpdated: new Date().toISOString(), skills: [] };
  }
}

function writeLibraryIndex(index) {
  index.lastUpdated = new Date().toISOString();
  fs.writeFileSync(getLibraryIndexPath(), JSON.stringify(index, null, 2), 'utf-8');
}

function readTranslationIndex() {
  const p = getTranslationIndexPath();
  if (!fs.existsSync(p)) return { skills: {} };
  try {
    return JSON.parse(fs.readFileSync(p, 'utf-8'));
  } catch {
    return { skills: {} };
  }
}

function writeTranslationIndex(index) {
  fs.writeFileSync(getTranslationIndexPath(), JSON.stringify(index, null, 2), 'utf-8');
}

function generateId() {
  return crypto.randomUUID();
}

function readSkillContent(skillMdPath) {
  if (!fs.existsSync(skillMdPath)) return '';
  return fs.readFileSync(skillMdPath, 'utf-8');
}

function getSkillZhPath(skillMdPath) {
  const dir = path.dirname(skillMdPath);
  const zhPath = path.join(dir, 'SKILL.zh.md');
  if (fs.existsSync(zhPath)) return zhPath;
  const zhPath2 = path.join(dir, 'skill.zh.md');
  if (fs.existsSync(zhPath2)) return zhPath2;
  return null;
}

function computeHash(content) {
  return crypto.createHash('sha256').update(content).digest('hex');
}

/**
 * 检查并更新翻译状态
 */
function refreshTranslationStatus() {
  const libIndex = readLibraryIndex();
  const transIndex = readTranslationIndex();
  let changed = false;

  for (const skill of libIndex.skills) {
    const zhPath = getSkillZhPath(skill.skillMdPath);
    const hasZh = !!zhPath;
    const prev = transIndex.skills[skill.folderName];
    if (!prev || prev.hasTranslation !== hasZh) {
      transIndex.skills[skill.folderName] = {
        hasTranslation: hasZh,
        translatedAt: hasZh ? new Date().toISOString() : undefined,
      };
      changed = true;
    }
    skill.hasTranslation = hasZh;

    // 同时更新 descriptionZh / skillTitleZh
    if (hasZh) {
      const parsed = parseSkillMd(zhPath);
      skill.descriptionZh = parsed.description || '';
      skill.skillTitleZh = parsed.skillTitle || '';
    } else {
      skill.descriptionZh = '';
      skill.skillTitleZh = '';
    }
  }

  if (changed) {
    writeTranslationIndex(transIndex);
    writeLibraryIndex(libIndex);
  }
  return { libIndex, transIndex };
}

/**
 * 获取库中所有技能（支持过滤和排序）
 */
function getAllSkills({ search = '', translated = 'all', sortBy = 'createdTime' } = {}) {
  const { libIndex } = refreshTranslationStatus();
  let skills = [...libIndex.skills];

  if (search) {
    const s = search.toLowerCase();
    skills = skills.filter(sk =>
      sk.folderName.toLowerCase().includes(s) ||
      (sk.description || '').toLowerCase().includes(s) ||
      (sk.descriptionZh || '').toLowerCase().includes(s)
    );
  }

  if (translated === 'yes') {
    skills = skills.filter(sk => sk.hasTranslation);
  } else if (translated === 'no') {
    skills = skills.filter(sk => !sk.hasTranslation);
  }

  // 排序
  skills.sort((a, b) => {
    if (sortBy === 'name') {
      return a.folderName.localeCompare(b.folderName);
    }
    if (sortBy === 'importedAt') {
      return new Date(b.importedAt || 0) - new Date(a.importedAt || 0);
    }
    // 默认 createdTime 从新到老
    return new Date(b.createdTime || 0) - new Date(a.createdTime || 0);
  });

  return skills;
}

/**
 * 获取技能详情（包含中文翻译内容）
 */
function getSkillDetail(id) {
  const libIndex = readLibraryIndex();
  const skill = libIndex.skills.find(s => s.id === id);
  if (!skill) return null;

  const content = readSkillContent(skill.skillMdPath);
  const zhPath = getSkillZhPath(skill.skillMdPath);
  const zhContent = zhPath ? readSkillContent(zhPath) : '';

  return {
    ...skill,
    content,
    zhContent,
    hasTranslation: !!zhPath,
  };
}

/**
 * 删除技能
 */
function deleteSkill(id) {
  const libIndex = readLibraryIndex();
  const idx = libIndex.skills.findIndex(s => s.id === id);
  if (idx === -1) return false;

  const skill = libIndex.skills[idx];
  const folderPath = path.dirname(skill.skillMdPath);

  // 物理删除
  if (fs.existsSync(folderPath)) {
    fs.rmSync(folderPath, { recursive: true, force: true });
  }

  libIndex.skills.splice(idx, 1);
  writeLibraryIndex(libIndex);

  // 清理翻译索引
  const transIndex = readTranslationIndex();
  delete transIndex.skills[skill.folderName];
  writeTranslationIndex(transIndex);

  return true;
}

/**
 * 获取可用的新文件夹名（处理重名）
 */
function getAvailableFolderName(workspacePath, baseName) {
  const target = path.join(workspacePath, baseName);
  if (!fs.existsSync(target)) return baseName;

  let i = 1;
  while (true) {
    const candidate = `${baseName}_${i}`;
    const candidatePath = path.join(workspacePath, candidate);
    if (!fs.existsSync(candidatePath)) return candidate;
    i++;
  }
}

/**
 * 复制目录
 */
function copyDirectory(src, dest) {
  if (!fs.existsSync(dest)) {
    fs.mkdirSync(dest, { recursive: true });
  }
  const entries = fs.readdirSync(src, { withFileTypes: true });
  for (const entry of entries) {
    const s = path.join(src, entry.name);
    const d = path.join(dest, entry.name);
    if (entry.isDirectory()) {
      copyDirectory(s, d);
    } else {
      fs.copyFileSync(s, d);
    }
  }
}

/**
 * 增量入库
 * @param {Array} scannedSkills - scanner 返回的结果数组
 * @returns {{imported: number, skipped: number, renamed: number, details: Array}}
 */
function importSkills(scannedSkills) {
  ensureWorkspace(getWorkspacePath());
  const workspacePath = getWorkspacePath();
  const libIndex = readLibraryIndex();
  const transIndex = readTranslationIndex();

  const existingNames = new Map(); // folderNameLower -> { folderName, hash }
  for (const sk of libIndex.skills) {
    const content = fs.existsSync(sk.skillMdPath) ? fs.readFileSync(sk.skillMdPath, 'utf-8') : '';
    existingNames.set(sk.folderName.toLowerCase(), {
      folderName: sk.folderName,
      hash: computeHash(content),
    });
  }

  const result = { imported: 0, skipped: 0, renamed: 0, details: [] };

  for (const sc of scannedSkills) {
    const baseName = sc.folderName;
    const existing = existingNames.get(baseName.toLowerCase());

    if (existing) {
      // 同名存在，对比 hash
      if (existing.hash === sc.contentHash) {
        result.skipped++;
        result.details.push({ folderName: baseName, action: 'skipped', reason: '已存在且内容相同' });
        continue;
      }
      // hash 不同，需要重命名
      const newName = getAvailableFolderName(workspacePath, baseName);
      const destPath = path.join(workspacePath, newName);
      copyDirectory(path.dirname(sc.skillMdPath), destPath);

      const newSkillMdPath = path.join(destPath, path.basename(sc.skillMdPath));
      const stats = fs.statSync(destPath);
      const parsed = parseSkillMd(newSkillMdPath);
      const skillEntry = {
        id: generateId(),
        folderName: newName,
        originalFolderName: baseName,
        skillMdPath: newSkillMdPath,
        description: parsed.description || '',
        descriptionZh: '',
        skillTitle: parsed.skillTitle || '',
        skillTitleZh: '',
        hasTranslation: false,
        createdTime: sc.createdTime,
        importedAt: new Date().toISOString(),
        sourcePath: path.dirname(sc.skillMdPath),
      };
      libIndex.skills.push(skillEntry);
      existingNames.set(newName.toLowerCase(), { folderName: newName, hash: sc.contentHash });
      result.renamed++;
      result.details.push({ folderName: baseName, action: 'renamed', newName, reason: '同名但内容不同' });
    } else {
      // 不存在，直接复制
      const destPath = path.join(workspacePath, baseName);
      copyDirectory(path.dirname(sc.skillMdPath), destPath);

      const newSkillMdPath = path.join(destPath, path.basename(sc.skillMdPath));
      const parsed = parseSkillMd(newSkillMdPath);
      const skillEntry = {
        id: generateId(),
        folderName: baseName,
        originalFolderName: baseName,
        skillMdPath: newSkillMdPath,
        description: parsed.description || '',
        descriptionZh: '',
        skillTitle: parsed.skillTitle || '',
        skillTitleZh: '',
        hasTranslation: false,
        createdTime: sc.createdTime,
        importedAt: new Date().toISOString(),
        sourcePath: path.dirname(sc.skillMdPath),
      };
      libIndex.skills.push(skillEntry);
      existingNames.set(baseName.toLowerCase(), { folderName: baseName, hash: sc.contentHash });
      result.imported++;
      result.details.push({ folderName: baseName, action: 'imported', reason: '新增' });
    }
  }

  writeLibraryIndex(libIndex);
  writeTranslationIndex(transIndex);
  return result;
}

/**
 * 获取翻译状态统计
 */
function getTranslationStatus() {
  const { libIndex, transIndex } = refreshTranslationStatus();
  const total = libIndex.skills.length;
  const translated = libIndex.skills.filter(s => s.hasTranslation).length;
  const untranslated = total - translated;
  const untranslatedList = libIndex.skills
    .filter(s => !s.hasTranslation)
    .map(s => ({
      folderName: s.folderName,
      skillMdPath: s.skillMdPath,
      description: s.description,
    }));

  return { total, translated, untranslated, untranslatedList };
}

module.exports = {
  readLibraryIndex,
  writeLibraryIndex,
  readTranslationIndex,
  writeTranslationIndex,
  getAllSkills,
  getSkillDetail,
  deleteSkill,
  importSkills,
  getTranslationStatus,
  getSkillZhPath,
  parseSkillMd,
};
