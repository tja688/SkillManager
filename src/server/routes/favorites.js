const express = require('express');
const router = express.Router();
const fs = require('fs');
const path = require('path');
const { getWorkspacePath } = require('../services/workspace');
const { readLibraryIndex } = require('../services/libraryManager');

function getFavoritesPath() {
  return path.join(getWorkspacePath(), '.favorites_index.json');
}

function readFavorites() {
  const p = getFavoritesPath();
  if (!fs.existsSync(p)) return { skillIds: [] };
  try {
    return JSON.parse(fs.readFileSync(p, 'utf-8'));
  } catch {
    return { skillIds: [] };
  }
}

function writeFavorites(data) {
  fs.writeFileSync(getFavoritesPath(), JSON.stringify(data, null, 2), 'utf-8');
}

router.get('/', (req, res) => {
  const fav = readFavorites();
  const libIndex = readLibraryIndex();
  const skills = libIndex.skills.filter(s => fav.skillIds.includes(s.id));
  res.json(skills);
});

router.post('/', (req, res) => {
  const { skillId } = req.body;
  if (!skillId) return res.status(400).json({ error: '缺少 skillId' });
  const fav = readFavorites();
  if (!fav.skillIds.includes(skillId)) {
    fav.skillIds.push(skillId);
    writeFavorites(fav);
  }
  res.json({ success: true });
});

router.delete('/:skillId', (req, res) => {
  const fav = readFavorites();
  fav.skillIds = fav.skillIds.filter(id => id !== req.params.skillId);
  writeFavorites(fav);
  res.json({ success: true });
});

module.exports = router;
