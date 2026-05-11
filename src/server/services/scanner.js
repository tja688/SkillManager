const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const { parseSkillMd } = require('./skillParser');

const EXCLUDED_NAMES = new Set([
  '$recycle.bin', '$sysreset', 'config.msi', 'windows', 'program files', 'program files (x86)',
  'programdata', 'system volume information', 'node_modules', '.git', 'bin', 'obj', 'temp', 'tmp',
  'vendor', 'dist', 'build', 'out', 'coverage', '.nuget', 'packages', 'cache', 'logs',
  'inetpub', 'intel', 'perflogs', 'swsetup', 'syswow64', 'winsxs',
]);

function isExcluded(name) {
  return EXCLUDED_NAMES.has(name.toLowerCase());
}

function getDrives() {
  const drives = [];
  for (let i = 67; i <= 90; i++) {
    const letter = String.fromCharCode(i);
    const drive = `${letter}:\\`;
    try {
      fs.accessSync(drive, fs.constants.R_OK);
      drives.push(drive);
    } catch {
      // ignore
    }
  }
  return drives;
}

/**
 * 并发控制的目录扫描
 * 目标：快速找到所有名为 "skills" 的目录
 */
async function findSkillsDirs(rootPath, progressCb, concurrency = 40) {
  const skillsDirs = [];
  const queue = [rootPath];
  let active = 0;
  let resolved = false;

  return new Promise((resolve) => {
    function done() {
      if (resolved) return;
      resolved = true;
      resolve(skillsDirs);
    }

    async function processNext() {
      while (queue.length > 0 && active < concurrency) {
        const current = queue.shift();
        active++;
        (async () => {
          try {
            const entries = await fs.promises.readdir(current, { withFileTypes: true });
            for (const entry of entries) {
              if (!entry.isDirectory()) continue;
              const name = entry.name;
              if (isExcluded(name)) continue;
              const fullPath = path.join(current, name);

              if (name.toLowerCase() === 'skills') {
                skillsDirs.push(fullPath);
              } else {
                queue.push(fullPath);
              }
            }
          } catch {
            // ignore permission errors
          }
          active--;
          if (queue.length === 0 && active === 0) {
            done();
          } else {
            processNext();
          }
        })();
      }
    }

    processNext();
    // Safety timeout: if somehow stuck, resolve after 5 minutes
    const safetyTimer = setTimeout(done, 5 * 60 * 1000);
    if (safetyTimer.unref) safetyTimer.unref();
  });
}

async function scanGlobal(progressCb = () => {}) {
  const drives = getDrives();
  progressCb(`发现 ${drives.length} 个盘符: ${drives.join(', ')}`);

  const allResults = [];

  for (const drive of drives) {
    progressCb(`正在扫描盘符 ${drive}...`);
    try {
      const skillsDirs = await findSkillsDirs(drive, progressCb);
      progressCb(`盘符 ${drive} 发现 ${skillsDirs.length} 个 skills 目录`);

      for (const skillsDir of skillsDirs) {
        try {
          const subEntries = await fs.promises.readdir(skillsDir, { withFileTypes: true });
          for (const sub of subEntries) {
            if (!sub.isDirectory()) continue;
            const skillFolderPath = path.join(skillsDir, sub.name);
            const skillMdPath = path.join(skillFolderPath, 'SKILL.md');
            const skillMdPathLower = path.join(skillFolderPath, 'skill.md');
            let actualMdPath = null;
            if (fs.existsSync(skillMdPath)) actualMdPath = skillMdPath;
            else if (fs.existsSync(skillMdPathLower)) actualMdPath = skillMdPathLower;

            if (actualMdPath) {
              try {
                const stats = fs.statSync(actualMdPath);
                const folderStats = fs.statSync(skillFolderPath);
                const content = fs.readFileSync(actualMdPath, 'utf-8');
                const hash = crypto.createHash('sha256').update(content).digest('hex');
                const parsed = parseSkillMd(actualMdPath);
                allResults.push({
                  folderName: sub.name,
                  skillMdPath: actualMdPath,
                  parentSkillsPath: skillsDir,
                  createdTime: folderStats.birthtime.toISOString(),
                  modifiedTime: stats.mtime.toISOString(),
                  description: parsed.description || parsed.skillTitle || '',
                  contentHash: hash,
                  size: stats.size,
                });
              } catch {
                // ignore invalid skill
              }
            }
          }
        } catch {
          // ignore unreadable skills dir
        }
      }
    } catch (err) {
      progressCb(`扫描 ${drive} 出错: ${err.message}`);
    }
  }

  progressCb(`扫描完成，共发现 ${allResults.length} 个技能`);
  return allResults;
}

module.exports = { scanGlobal, getDrives };
