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
    return res.json({ skills: [], duplicates: [] });
  }
  try {
    const cache = JSON.parse(fs.readFileSync(p, 'utf-8'));
    const skills = cache.skills || [];

    // 按 folderName 分组，找出重复
    const groups = {};
    for (const s of skills) {
      const key = s.folderName.toLowerCase();
      if (!groups[key]) groups[key] = [];
      groups[key].push(s);
    }

    const duplicates = Object.entries(groups)
      .filter(([_, list]) => list.length > 1)
      .map(([name, list]) => ({
        folderName: list[0].folderName,
        locations: list.map(l => ({
          path: l.parentSkillsPath,
          skillMdPath: l.skillMdPath,
          createdTime: l.createdTime,
          isInLibrary: l.isInLibrary,
        })),
        count: list.length,
      }));

    res.json({ skills, duplicates });
  } catch {
    res.json({ skills: [], duplicates: [] });
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
