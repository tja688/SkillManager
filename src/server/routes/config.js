const express = require('express');
const router = express.Router();
const { readConfig, setWorkspacePath, migrateWorkspace } = require('../services/workspace');

router.get('/', (req, res) => {
  res.json(readConfig());
});

router.post('/', (req, res) => {
  const { workspacePath } = req.body;
  if (!workspacePath) {
    return res.status(400).json({ error: '缺少 workspacePath' });
  }
  const newPath = setWorkspacePath(workspacePath);
  res.json({ success: true, workspacePath: newPath });
});

router.post('/migrate', (req, res) => {
  const { newPath } = req.body;
  if (!newPath) {
    return res.status(400).json({ error: '缺少 newPath' });
  }
  const result = migrateWorkspace(newPath);
  res.json({ success: true, workspacePath: result });
});

module.exports = router;
