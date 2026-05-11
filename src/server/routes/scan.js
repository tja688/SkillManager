const express = require('express');
const router = express.Router();
const fs = require('fs');
const path = require('path');
const { scanGlobal } = require('../services/scanner');
const { getWorkspacePath } = require('../services/workspace');
const { readLibraryIndex } = require('../services/libraryManager');

let scanJob = null;
let lastScanResults = [];

function getScanCachePath() {
  return path.join(getWorkspacePath(), '.scan_cache.json');
}

function loadLastScan() {
  const p = getScanCachePath();
  if (fs.existsSync(p)) {
    try {
      const cache = JSON.parse(fs.readFileSync(p, 'utf-8'));
      lastScanResults = cache.skills || [];
      return cache;
    } catch {
      // ignore
    }
  }
  return null;
}

loadLastScan();

router.post('/', async (req, res) => {
  if (scanJob && scanJob.status === 'running') {
    return res.status(409).json({ error: '已有扫描任务正在运行' });
  }

  const jobId = Date.now().toString();
  const logs = [];
  scanJob = { id: jobId, status: 'running', logs, progress: 0 };

  res.json({ jobId, status: 'running' });

  // 后台执行扫描
  try {
    const results = await scanGlobal((msg) => {
      logs.push(msg);
      scanJob.logs = logs;
    });

    // 标记是否已在库中
    const libIndex = readLibraryIndex();
    const libNames = new Set(libIndex.skills.map(s => s.folderName.toLowerCase()));
    results.forEach(r => {
      r.isInLibrary = libNames.has(r.folderName.toLowerCase());
    });

    lastScanResults = results;
    fs.writeFileSync(getScanCachePath(), JSON.stringify({ scanTime: new Date().toISOString(), skills: results }, null, 2), 'utf-8');

    scanJob.status = 'completed';
    scanJob.progress = 100;
    scanJob.resultCount = results.length;
  } catch (err) {
    scanJob.status = 'failed';
    scanJob.error = err.message;
  }
});

router.get('/status', (req, res) => {
  if (!scanJob) return res.json({ status: 'idle' });
  res.json(scanJob);
});

router.get('/results', (req, res) => {
  res.json({ scanTime: scanJob?.scanTime || new Date().toISOString(), skills: lastScanResults });
});

module.exports = router;
