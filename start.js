const { spawn } = require('child_process');
const open = require('open');

const PORT = process.env.PORT || 3456;
const URL = `http://localhost:${PORT}`;

console.log('正在启动 SkillManager...');

const server = spawn(process.execPath, ['src/server/index.js'], {
  stdio: 'inherit',
  cwd: __dirname,
});

// 等待服务器就绪后打开浏览器
let opened = false;
const checkAndOpen = () => {
  if (opened) return;
  const http = require('http');
  const req = http.get(URL, (res) => {
    if (res.statusCode === 200) {
      opened = true;
      console.log(`正在打开浏览器: ${URL}`);
      open(URL);
    }
  });
  req.on('error', () => {
    // 还未就绪，稍后重试
  });
  req.setTimeout(2000, () => {
    req.abort();
  });
};

const interval = setInterval(checkAndOpen, 1500);
setTimeout(() => {
  clearInterval(interval);
  if (!opened) {
    console.log(`请手动在浏览器中打开: ${URL}`);
  }
}, 30000);

process.on('SIGINT', () => {
  console.log('\n正在关闭 SkillManager...');
  server.kill();
  process.exit(0);
});
