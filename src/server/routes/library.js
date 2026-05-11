const express = require('express');
const router = express.Router();
const { getAllSkills, getSkillDetail, deleteSkill, importSkills, getTranslationStatus } = require('../services/libraryManager');

router.get('/translation/status', (req, res) => {
  res.json(getTranslationStatus());
});

router.get('/', (req, res) => {
  const { search, translated, sortBy } = req.query;
  const skills = getAllSkills({ search, translated, sortBy });
  res.json(skills);
});

router.post('/import', (req, res) => {
  let { skills } = req.body;
  // 如果未提供 skills，尝试从 scan cache 读取
  if (!skills || !Array.isArray(skills) || skills.length === 0) {
    const fs = require('fs');
    const path = require('path');
    const { getWorkspacePath } = require('../services/workspace');
    const cachePath = path.join(getWorkspacePath(), '.scan_cache.json');
    if (fs.existsSync(cachePath)) {
      try {
        const cache = JSON.parse(fs.readFileSync(cachePath, 'utf-8'));
        skills = cache.skills || [];
      } catch {
        return res.status(400).json({ error: '无法读取扫描缓存' });
      }
    } else {
      return res.status(400).json({ error: '缺少 skills 且无扫描缓存' });
    }
  }
  const result = importSkills(skills);
  res.json(result);
});

router.get('/:id/content', (req, res) => {
  const detail = getSkillDetail(req.params.id);
  if (!detail) return res.status(404).json({ error: '技能不存在' });
  res.json(detail);
});

router.delete('/:id', (req, res) => {
  const ok = deleteSkill(req.params.id);
  if (!ok) return res.status(404).json({ error: '技能不存在' });
  res.json({ success: true });
});

module.exports = router;
