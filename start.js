const { spawn } = require('child_process');

const PORT = process.env.PORT || 3456;
const URL = `http://localhost:${PORT}`;

console.log('正在启动 SkillManager...');

const server = spawn(process.execPath, ['src/server/index.js'], {
  stdio: 'inherit',
  cwd: __dirname,
});

// 等待服务器就绪后打开浏览器
let opened = false;
const checkAndOpen = async () => {
  if (opened) return;
  const http = require('http');
  const req = http.get(URL, async (res) => {
    if (res.statusCode === 200) {
      opened = true;
      console.log(`正在打开浏览器: ${URL}`);
      try {
        const open = require('open').default;
        await open(URL);
      } catch (e) {
        console.log(`打开浏览器失败: ${e.message}`);
        console.log(`请手动在浏览器中打开: ${URL}`);
      }
    }
  });
  req.on('error', () => {
    // 还未就绪，稍后重试
  });
  req.setTimeout(2000, () => {
    req.destroy();
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
