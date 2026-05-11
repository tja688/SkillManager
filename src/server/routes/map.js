const express = require('express');
const router = express.Router();
const { exec } = require('child_process');
const fs = require('fs');
const path = require('path');
const { getWorkspacePath } = require('../services/workspace');

function getScanCachePath() {
  return path.join(getWorkspacePath(), '.scan_cache.json');
}

router.get('/', (req, res) => {
  const p = getScanCachePath();
  if (!fs.existsSync(p)) {
    return res.json({ zones: [], duplicateNames: [] });
  }
  try {
    const cache = JSON.parse(fs.readFileSync(p, 'utf-8'));
    const skills = cache.skills || [];

    // 1. 按上层目录分组 (parentSkillsPath 的父目录)
    const zoneMap = {};
    for (const s of skills) {
      const zonePath = path.dirname(s.parentSkillsPath);
      if (!zoneMap[zonePath]) zoneMap[zonePath] = [];
      zoneMap[zonePath].push(s);
    }

    // 2. 找出跨 zone 重复的 skill 名
    const nameCount = {};
    for (const s of skills) {
      const key = s.folderName.toLowerCase();
      nameCount[key] = (nameCount[key] || 0) + 1;
    }
    const duplicateNames = Object.entries(nameCount)
      .filter(([_, count]) => count > 1)
      .map(([name]) => name);

    // 3. 组装 zones
    const zones = Object.entries(zoneMap).map(([zonePath, zoneSkills]) => ({
      zonePath,
      skills: zoneSkills.map(s => ({
        folderName: s.folderName,
        skillMdPath: s.skillMdPath,
        createdTime: s.createdTime,
        isDuplicate: nameCount[s.folderName.toLowerCase()] > 1,
      })),
      skillCount: zoneSkills.length,
    }));

    // 按 skillCount 降序排列
    zones.sort((a, b) => b.skillCount - a.skillCount);

    res.json({ zones, duplicateNames });
  } catch {
    res.json({ zones: [], duplicateNames: [] });
  }
});

router.post('/open', (req, res) => {
  const { targetPath } = req.body;
  if (!targetPath) return res.status(400).json({ error: '缺少 targetPath' });

  const cmd = `explorer /select,"${targetPath}"`;
  exec(cmd, (err) => {
    if (err) return res.status(500).json({ error: err.message });
    res.json({ success: true });
  });
});

module.exports = router;
