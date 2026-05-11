const express = require('express');
const path = require('path');

const app = express();
app.use(express.json({ limit: '50mb' }));

// CORS 允许本地前端
app.use((req, res, next) => {
  res.header('Access-Control-Allow-Origin', '*');
  res.header('Access-Control-Allow-Methods', 'GET, POST, DELETE, OPTIONS');
  res.header('Access-Control-Allow-Headers', 'Content-Type');
  if (req.method === 'OPTIONS') return res.sendStatus(200);
  next();
});

// API 路由
app.use('/api/config', require('./routes/config'));
app.use('/api/library', require('./routes/library'));
app.use('/api/favorites', require('./routes/favorites'));
app.use('/api/scan', require('./routes/scan'));
app.use('/api/map', require('./routes/map'));

// 静态文件（前端）
app.use(express.static(path.join(__dirname, '../client')));

// 兜底返回 index.html（单页应用）
app.get('*', (req, res) => {
  res.sendFile(path.join(__dirname, '../client/index.html'));
});

const PORT = process.env.PORT || 3456;
app.listen(PORT, () => {
  console.log(`SkillManager server running at http://localhost:${PORT}`);
});

module.exports = app;
