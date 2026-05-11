const API_BASE = '';

function api(path) {
  return `${API_BASE}${path}`;
}

let currentPage = 'library';
let librarySkills = [];
let selectedSkillId = null;
let scanResults = [];
let scanTimer = null;

// ========== Router ==========
function initRouter() {
  window.addEventListener('hashchange', handleRoute);
  handleRoute();
}

function handleRoute() {
  const hash = window.location.hash.replace('#/', '') || 'library';
  currentPage = hash.split('/')[0];

  document.querySelectorAll('.nav-item').forEach(el => {
    el.classList.toggle('active', el.dataset.page === currentPage);
  });

  document.querySelectorAll('.page').forEach(el => {
    el.classList.toggle('active', el.dataset.page === currentPage);
  });

  if (currentPage === 'library') loadLibrary();
  if (currentPage === 'scan') loadScan();
  if (currentPage === 'map') loadMap();
  if (currentPage === 'settings') loadSettings();
}

// ========== Library ==========
async function loadLibrary() {
  const main = document.getElementById('main-content');
  main.innerHTML = `
    <div class="page active" data-page="library">
      <div class="toolbar">
        <input type="text" id="lib-search" placeholder="搜索技能名字或描述..." />
        <select id="lib-filter">
          <option value="all">全部</option>
          <option value="yes">已翻译</option>
          <option value="no">未翻译</option>
        </select>
        <span id="lib-count" style="color:var(--text-secondary);font-size:12px;"></span>
      </div>
      <div class="library-layout">
        <div class="skill-list" id="skill-list"></div>
        <div class="skill-detail" id="skill-detail">
          <div class="empty-state">选择左侧技能查看详情</div>
        </div>
      </div>
      <div class="translation-panel" id="translation-panel"></div>
    </div>
  `;

  const searchEl = document.getElementById('lib-search');
  const filterEl = document.getElementById('lib-filter');

  searchEl.addEventListener('input', debounce(() => fetchLibrary(), 300));
  filterEl.addEventListener('change', () => fetchLibrary());

  await fetchLibrary();
  await fetchTranslationStatus();
}

async function fetchLibrary() {
  const search = document.getElementById('lib-search').value;
  const translated = document.getElementById('lib-filter').value;
  const res = await fetch(api(`/api/library?search=${encodeURIComponent(search)}&translated=${translated}`));
  librarySkills = await res.json();
  renderSkillList();
  document.getElementById('lib-count').textContent = `共 ${librarySkills.length} 个技能`;
}

function renderSkillList() {
  const container = document.getElementById('skill-list');
  if (!container) return;

  const html = librarySkills.map(sk => `
    <div class="skill-item ${sk.id === selectedSkillId ? 'active' : ''}" data-id="${sk.id}">
      <div class="name">${escapeHtml(sk.folderName)}</div>
      <div class="desc">${escapeHtml(sk.descriptionZh || sk.description || '暂无描述')}</div>
      <div class="meta">
        <span class="badge ${sk.hasTranslation ? 'translated' : ''}">${sk.hasTranslation ? '已翻译' : '未翻译'}</span>
        <span>${formatDate(sk.createdTime)}</span>
      </div>
    </div>
  `).join('');

  container.innerHTML = html || '<div class="empty-state" style="padding:20px;">无匹配技能</div>';

  container.querySelectorAll('.skill-item').forEach(el => {
    el.addEventListener('click', () => {
      selectedSkillId = el.dataset.id;
      renderSkillList();
      loadSkillDetail(selectedSkillId);
    });
  });
}

async function loadSkillDetail(id) {
  const container = document.getElementById('skill-detail');
  const res = await fetch(api(`/api/library/${id}/content`));
  const data = await res.json();

  const displayContent = data.zhContent || data.content || '';
  const langBadge = data.zhContent ? '<span class="badge translated">中文</span>' : '<span class="badge">原文</span>';

  container.innerHTML = `
    <h2>${escapeHtml(data.folderName)} ${langBadge}</h2>
    <div class="detail-actions">
      <button class="btn btn-danger" onclick="deleteSkill('${id}')">删除技能</button>
      <button class="btn" onclick="openFolder('${data.skillMdPath}')">打开文件夹</button>
    </div>
    <div class="markdown-body">${simpleMarkdown(displayContent)}</div>
  `;
}

async function deleteSkill(id) {
  if (!confirm('确定要删除这个技能吗？此操作不可恢复。')) return;
  await fetch(api(`/api/library/${id}`), { method: 'DELETE' });
  selectedSkillId = null;
  await fetchLibrary();
  document.getElementById('skill-detail').innerHTML = '<div class="empty-state">选择左侧技能查看详情</div>';
}

function openFolder(skillMdPath) {
  const dir = skillMdPath.replace(/\\/g, '/').replace(/\//g, '\\');
  fetch(api('/api/map/open'), {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ targetPath: dir })
  });
}

async function fetchTranslationStatus() {
  const res = await fetch(api('/api/library/translation/status'));
  const data = await res.json();
  const panel = document.getElementById('translation-panel');
  if (!panel) return;

  panel.innerHTML = `
    <div class="stats">
      <span><strong>${data.total}</strong> 总数</span>
      <span><strong style="color:var(--success)">${data.translated}</strong> 已翻译</span>
      <span><strong style="color:var(--warning)">${data.untranslated}</strong> 未翻译</span>
    </div>
    ${data.untranslated > 0 ? `<div style="font-size:12px;color:var(--text-secondary);margin-bottom:6px;">待翻译列表（前20个）：</div>` : ''}
    <div style="display:flex;flex-wrap:wrap;gap:6px;">
      ${data.untranslatedList.slice(0, 20).map(u => `
        <span class="badge" title="${escapeHtml(u.skillMdPath)}">${escapeHtml(u.folderName)}</span>
      `).join('')}
      ${data.untranslated > 20 ? `<span class="badge">+${data.untranslated - 20} 更多</span>` : ''}
    </div>
  `;
}

// ========== Scan ==========
async function loadScan() {
  const main = document.getElementById('main-content');
  main.innerHTML = `
    <div class="page active" data-page="scan">
      <div class="toolbar">
        <button class="btn btn-primary" id="btn-scan">开始全电脑扫描</button>
        <button class="btn btn-success" id="btn-import" style="display:none;">增量入库</button>
        <span id="scan-status" style="color:var(--text-secondary);font-size:12px;"></span>
      </div>
      <div class="scan-content" id="scan-content"></div>
      <div class="log-panel" id="scan-logs" style="display:none;"></div>
    </div>
  `;

  document.getElementById('btn-scan').addEventListener('click', startScan);
  document.getElementById('btn-import').addEventListener('click', doImport);

  // 尝试加载缓存结果
  const res = await fetch(api('/api/scan/results'));
  const data = await res.json();
  if (data.skills && data.skills.length > 0) {
    scanResults = data.skills;
    renderScanResults();
  }
}

async function startScan() {
  const statusEl = document.getElementById('scan-status');
  const logsEl = document.getElementById('scan-logs');
  const importBtn = document.getElementById('btn-import');

  statusEl.textContent = '扫描中...';
  logsEl.style.display = 'block';
  logsEl.innerHTML = '';
  importBtn.style.display = 'none';

  const res = await fetch(api('/api/scan'), { method: 'POST' });
  const { jobId } = await res.json();

  scanTimer = setInterval(async () => {
    const sres = await fetch(api('/api/scan/status'));
    const status = await sres.json();

    if (status.logs) {
      logsEl.innerHTML = status.logs.slice(-20).map(l => `<div class="log-line">${escapeHtml(l)}</div>`).join('');
      logsEl.scrollTop = logsEl.scrollHeight;
    }

    if (status.status === 'completed') {
      clearInterval(scanTimer);
      statusEl.textContent = `扫描完成，发现 ${status.resultCount} 个技能`;
      const rres = await fetch(api('/api/scan/results'));
      const data = await rres.json();
      scanResults = data.skills || [];
      renderScanResults();
      importBtn.style.display = 'inline-block';
    } else if (status.status === 'failed') {
      clearInterval(scanTimer);
      statusEl.textContent = '扫描失败: ' + status.error;
    }
  }, 1500);
}

function renderScanResults() {
  const container = document.getElementById('scan-content');
  if (!scanResults.length) {
    container.innerHTML = '<div class="empty-state">暂无扫描结果，点击"开始全电脑扫描"</div>';
    return;
  }

  const now = Date.now();
  const oneMonth = 30 * 24 * 60 * 60 * 1000;
  const sixMonths = 180 * 24 * 60 * 60 * 1000;

  const monthGroup = [];
  const halfYearGroup = [];
  const olderGroup = [];

  for (const s of scanResults) {
    const t = new Date(s.createdTime).getTime();
    if (now - t <= oneMonth) monthGroup.push(s);
    else if (now - t <= sixMonths) halfYearGroup.push(s);
    else olderGroup.push(s);
  }

  container.innerHTML = `
    ${renderGroup('最近一个月', monthGroup, true)}
    ${renderGroup('最近半年', halfYearGroup, false)}
    ${renderGroup('一年以上', olderGroup, false)}
  `;

  container.querySelectorAll('.scan-group-header').forEach(h => {
    h.addEventListener('click', () => {
      h.parentElement.classList.toggle('expanded');
    });
  });
}

function renderGroup(title, items, expanded) {
  if (!items.length) return '';
  return `
    <div class="scan-group ${expanded ? 'expanded' : ''}">
      <div class="scan-group-header">
        <span class="title">${title}</span>
        <span class="count">${items.length} 个</span>
      </div>
      <div class="scan-group-body">
        ${items.map(s => `
          <div class="scan-skill-row">
            <div class="info">
              <div class="name">${escapeHtml(s.folderName)}</div>
              <div class="path">${escapeHtml(s.parentSkillsPath)}</div>
            </div>
            <div style="display:flex;align-items:center;">
              <span class="date">${formatDate(s.createdTime)}</span>
              <span class="status ${s.isInLibrary ? 'status-in-lib' : 'status-new'}">
                ${s.isInLibrary ? '已在库' : '新发现'}
              </span>
            </div>
          </div>
        `).join('')}
      </div>
    </div>
  `;
}

async function doImport() {
  if (!scanResults.length) return;
  const btn = document.getElementById('btn-import');
  btn.disabled = true;
  btn.textContent = '入库中...';

  const res = await fetch(api('/api/library/import'), {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ skills: scanResults }),
  });

  const result = await res.json();
  alert(`入库完成！导入 ${result.imported} 个，重命名 ${result.renamed} 个，跳过 ${result.skipped} 个。`);

  btn.disabled = false;
  btn.textContent = '增量入库';

  // 刷新扫描结果中的 isInLibrary 状态
  scanResults.forEach(s => { s.isInLibrary = true; });
  renderScanResults();
}

// ========== Map ==========
async function loadMap() {
  const main = document.getElementById('main-content');
  main.innerHTML = `
    <div class="page active" data-page="map">
      <div class="toolbar">
        <span style="color:var(--text-secondary);font-size:13px;">全局技能地图 — 展示全电脑发现的技能分布与重复情况</span>
      </div>
      <div class="map-content" id="map-content"></div>
    </div>
  `;

  const res = await fetch(api('/api/map'));
  const data = await res.json();
  renderMap(data);
}

function renderMap(data) {
  const container = document.getElementById('map-content');
  const duplicates = data.duplicates || [];
  const allSkills = data.skills || [];

  container.innerHTML = `
    <div class="map-section">
      <h3>重复技能分布 (${duplicates.length} 组)</h3>
      ${duplicates.length === 0 ? '<div style="color:var(--text-secondary);font-size:13px;">未发现重复技能</div>' : ''}
      ${duplicates.map(d => `
        <div class="map-card duplicate">
          <div class="card-title">${escapeHtml(d.folderName)} <span style="color:var(--warning);font-size:12px;">(${d.count} 处)</span></div>
          <div class="location-list">
            ${d.locations.map(loc => `
              <div class="location-item">
                <span>${escapeHtml(loc.path)}</span>
                <div>
                  ${loc.isInLibrary ? '<span class="badge translated" style="margin-right:6px;">已入库</span>' : '<span class="badge" style="margin-right:6px;">未入库</span>'}
                  <button class="btn" onclick="openMapFolder('${escapeHtml(loc.skillMdPath.replace(/\\/g, '\\\\').replace(/'/g, "\\'"))}')">打开</button>
                </div>
              </div>
            `).join('')}
          </div>
        </div>
      `).join('')}
    </div>

    <div class="map-section">
      <h3>全部技能 (${allSkills.length} 个)</h3>
      ${allSkills.slice(0, 200).map(s => `
        <div class="map-card">
          <div class="card-title">${escapeHtml(s.folderName)}</div>
          <div class="location-item">
            <span>${escapeHtml(s.parentSkillsPath)}</span>
            <button class="btn" onclick="openMapFolder('${escapeHtml(s.skillMdPath.replace(/\\/g, '\\\\').replace(/'/g, "\\'"))}')">打开</button>
          </div>
        </div>
      `).join('')}
      ${allSkills.length > 200 ? `<div style="color:var(--text-secondary);text-align:center;padding:10px;">还有 ${allSkills.length - 200} 个技能未显示</div>` : ''}
    </div>
  `;
}

function openMapFolder(p) {
  fetch(api('/api/map/open'), {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ targetPath: p }),
  });
}

// ========== Settings ==========
async function loadSettings() {
  const main = document.getElementById('main-content');
  main.innerHTML = `
    <div class="page active" data-page="settings">
      <div class="settings-content">
        <h2>设置</h2>
        <div class="form-group">
          <label>工作区路径（技能库存储目录）</label>
          <input type="text" id="setting-path" placeholder="C:\\Users\\...\\SkillLibrary" />
        </div>
        <button class="btn btn-primary" id="btn-save-path">保存路径</button>
        <button class="btn" id="btn-migrate" style="margin-left:10px;">迁移到新路径</button>
        <div id="setting-msg" style="margin-top:12px;font-size:13px;color:var(--success);"></div>
      </div>
    </div>
  `;

  const res = await fetch(api('/api/config'));
  const cfg = await res.json();
  document.getElementById('setting-path').value = cfg.workspacePath || '';

  document.getElementById('btn-save-path').addEventListener('click', async () => {
    const p = document.getElementById('setting-path').value;
    await fetch(api('/api/config'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ workspacePath: p }),
    });
    document.getElementById('setting-msg').textContent = '已保存';
  });

  document.getElementById('btn-migrate').addEventListener('click', async () => {
    const p = document.getElementById('setting-path').value;
    if (!confirm(`确定要将工作区迁移到 ${p} 吗？旧数据将被复制到新路径。`)) return;
    const res = await fetch(api('/api/config/migrate'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ newPath: p }),
    });
    const data = await res.json();
    document.getElementById('setting-msg').textContent = `已迁移到: ${data.workspacePath}`;
  });
}

// ========== Utils ==========
function debounce(fn, wait) {
  let t;
  return (...args) => {
    clearTimeout(t);
    t = setTimeout(() => fn(...args), wait);
  };
}

function escapeHtml(str) {
  if (!str) return '';
  return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function formatDate(iso) {
  if (!iso) return '';
  const d = new Date(iso);
  return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;
}

function simpleMarkdown(md) {
  if (!md) return '<p style="color:var(--text-secondary)">无内容</p>';
  let html = escapeHtml(md);

  // code blocks
  html = html.replace(/```([\s\S]*?)```/g, '<pre><code>$1</code></pre>');
  // inline code
  html = html.replace(/`([^`]+)`/g, '<code>$1</code>');
  // headers
  html = html.replace(/^#### (.*$)/gim, '<h4>$1</h4>');
  html = html.replace(/^### (.*$)/gim, '<h3>$1</h3>');
  html = html.replace(/^## (.*$)/gim, '<h2>$1</h2>');
  html = html.replace(/^# (.*$)/gim, '<h1>$1</h1>');
  // bold / italic
  html = html.replace(/\*\*\*(.*?)\*\*\*/g, '<strong><em>$1</em></strong>');
  html = html.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
  html = html.replace(/\*(.*?)\*/g, '<em>$1</em>');
  // blockquote
  html = html.replace(/^&gt; (.*$)/gim, '<blockquote>$1</blockquote>');
  // lists
  html = html.replace(/^\- (.*$)/gim, '<ul><li>$1</li></ul>');
  html = html.replace(/^\d+\. (.*$)/gim, '<ol><li>$1</li></ol>');
  // links
  html = html.replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2" target="_blank" style="color:var(--accent)">$1</a>');
  // line breaks
  html = html.replace(/\n/g, '<br>');
  // fix nested lists
  html = html.replace(/<\/ul><br><ul>/g, '');
  html = html.replace(/<\/ol><br><ol>/g, '');

  return html;
}

// ========== Init ==========
document.addEventListener('DOMContentLoaded', initRouter);
