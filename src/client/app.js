const API_BASE = '';

function api(path) {
  return `${API_BASE}${path}`;
}

let currentPage = 'library';
let librarySkills = [];
let selectedSkillId = null;
let scanResults = [];
let scanTimer = null;
let favoriteIds = new Set();

// ========== Settings / Layout Memory ==========
function loadAppSettings() {
  try {
    return JSON.parse(localStorage.getItem('sm_settings') || '{}');
  } catch {
    return {};
  }
}

function saveAppSettings(patch) {
  const s = loadAppSettings();
  Object.assign(s, patch);
  localStorage.setItem('sm_settings', JSON.stringify(s));
}

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
  if (currentPage === 'favorites') loadFavorites();
  if (currentPage === 'scan') loadScan();
  if (currentPage === 'map') loadMap();
  if (currentPage === 'settings') loadSettings();
}

// ========== Library ==========
async function loadLibrary() {
  const settings = loadAppSettings();
  const leftWidth = settings.libraryLeftWidth || 380;

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
        <select id="lib-sort">
          <option value="createdTime">创建时间 (新→老)</option>
          <option value="name">名字 (A→Z)</option>
          <option value="importedAt">入库时间 (新→老)</option>
        </select>
        <span id="lib-count" style="color:var(--text-secondary);font-size:12px;"></span>
      </div>
      <div class="library-layout" id="library-layout" style="--left-width:${leftWidth}px;">
        <div class="skill-list" id="skill-list"></div>
        <div class="resizer" id="library-resizer"></div>
        <div class="skill-detail" id="skill-detail">
          <div class="empty-state">选择左侧技能查看详情</div>
        </div>
      </div>
      <div class="translation-panel" id="translation-panel"></div>
    </div>
  `;

  // restore sort
  const sortEl = document.getElementById('lib-sort');
  if (settings.librarySortBy) sortEl.value = settings.librarySortBy;

  const searchEl = document.getElementById('lib-search');
  const filterEl = document.getElementById('lib-filter');

  searchEl.addEventListener('input', debounce(() => fetchLibrary(), 300));
  filterEl.addEventListener('change', () => fetchLibrary());
  sortEl.addEventListener('change', () => { saveAppSettings({ librarySortBy: sortEl.value }); fetchLibrary(); });

  initResizer(document.getElementById('library-resizer'), document.getElementById('library-layout'), 'libraryLeftWidth');

  await fetchLibrary();
  await fetchTranslationStatus();
}

async function fetchLibrary() {
  const search = document.getElementById('lib-search').value;
  const translated = document.getElementById('lib-filter').value;
  const sortBy = document.getElementById('lib-sort').value;
  const res = await fetch(api(`/api/library?search=${encodeURIComponent(search)}&translated=${translated}&sortBy=${sortBy}`));
  librarySkills = await res.json();
  await loadFavoritesSet();
  renderSkillList();
  document.getElementById('lib-count').textContent = `共 ${librarySkills.length} 个技能`;
}

async function loadFavoritesSet() {
  try {
    const res = await fetch(api('/api/favorites'));
    const favs = await res.json();
    favoriteIds = new Set(favs.map(f => f.id));
  } catch {
    favoriteIds = new Set();
  }
}

function renderSkillList() {
  const container = document.getElementById('skill-list');
  if (!container) return;

  const html = librarySkills.map(sk => {
    const isFav = favoriteIds.has(sk.id);
    return `
    <div class="skill-item ${sk.id === selectedSkillId ? 'active' : ''}" data-id="${sk.id}">
      <div class="name">
        ${escapeHtml(sk.folderName)}
        <span class="fav-star ${isFav ? 'active' : ''}" data-id="${sk.id}" title="${isFav ? '取消收藏' : '收藏'}">★</span>
      </div>
      <div class="desc">${escapeHtml(sk.descriptionZh || sk.description || '暂无描述')}</div>
      <div class="meta">
        <span class="badge ${sk.hasTranslation ? 'translated' : ''}">${sk.hasTranslation ? '已翻译' : '未翻译'}</span>
        <span>${formatDate(sk.createdTime)}</span>
      </div>
    </div>
  `}).join('');

  container.innerHTML = html || '<div class="empty-state" style="padding:20px;">无匹配技能</div>';

  container.querySelectorAll('.skill-item').forEach(el => {
    el.addEventListener('click', (e) => {
      if (e.target.classList.contains('fav-star')) return;
      selectedSkillId = el.dataset.id;
      renderSkillList();
      loadSkillDetail(selectedSkillId);
    });
  });

  container.querySelectorAll('.fav-star').forEach(el => {
    el.addEventListener('click', async (e) => {
      e.stopPropagation();
      const id = el.dataset.id;
      if (favoriteIds.has(id)) {
        await fetch(api(`/api/favorites/${id}`), { method: 'DELETE' });
        favoriteIds.delete(id);
      } else {
        await fetch(api('/api/favorites'), { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ skillId: id }) });
        favoriteIds.add(id);
      }
      renderSkillList();
      if (currentPage === 'favorites') loadFavorites();
    });
  });
}

async function loadSkillDetail(id) {
  const container = document.getElementById('skill-detail');
  const res = await fetch(api(`/api/library/${id}/content`));
  const data = await res.json();
  const isFav = favoriteIds.has(id);

  const displayContent = data.zhContent || data.content || '';
  const langBadge = data.zhContent ? '<span class="badge translated">中文</span>' : '<span class="badge">原文</span>';

  container.innerHTML = `
    <h2>${escapeHtml(data.folderName)} ${langBadge}</h2>
    <div class="detail-actions">
      <button class="btn btn-danger" onclick="deleteSkill('${id}')">删除技能</button>
      <button class="btn" onclick="openFolder('${data.skillMdPath.replace(/\\/g, '\\\\').replace(/'/g, "\\'")}')">打开文件夹</button>
      <button class="btn ${isFav ? 'btn-primary' : ''}" id="detail-fav-btn">${isFav ? '★ 已收藏' : '☆ 收藏'}</button>
    </div>
    <div class="markdown-body">${simpleMarkdown(displayContent)}</div>
  `;

  const favBtn = document.getElementById('detail-fav-btn');
  if (favBtn) {
    favBtn.addEventListener('click', async () => {
      if (favoriteIds.has(id)) {
        await fetch(api(`/api/favorites/${id}`), { method: 'DELETE' });
        favoriteIds.delete(id);
      } else {
        await fetch(api('/api/favorites'), { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ skillId: id }) });
        favoriteIds.add(id);
      }
      renderSkillList();
      loadSkillDetail(id);
      if (currentPage === 'favorites') loadFavorites();
    });
  }
}

async function deleteSkill(id) {
  if (!confirm('确定要删除这个技能吗？此操作不可恢复。')) return;
  await fetch(api(`/api/library/${id}`), { method: 'DELETE' });
  selectedSkillId = null;
  await fetchLibrary();
  document.getElementById('skill-detail').innerHTML = '<div class="empty-state">选择左侧技能查看详情</div>';
}

function openFolder(skillMdPath) {
  fetch(api('/api/map/open'), {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ targetPath: skillMdPath }),
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

// ========== Resizer ==========
function initResizer(resizerEl, containerEl, settingKey) {
  if (!resizerEl || !containerEl) return;
  let isDragging = false;
  let rafId = null;
  let resizeState = null;

  resizerEl.addEventListener('mousedown', (e) => {
    isDragging = true;
    const rect = containerEl.getBoundingClientRect();
    resizeState = { left: rect.left, width: rect.width };
    document.body.style.cursor = 'col-resize';
    document.body.style.userSelect = 'none';
    const overlay = document.createElement('div');
    overlay.id = 'resizer-overlay';
    overlay.style.cssText = 'position:fixed;top:0;left:0;right:0;bottom:0;z-index:9999;cursor:col-resize;';
    document.body.appendChild(overlay);
  });

  window.addEventListener('mousemove', (e) => {
    if (!isDragging) return;
    if (rafId) return;
    rafId = requestAnimationFrame(() => {
      rafId = null;
      const state = resizeState;
      let w = e.clientX - state.left;
      if (w < 200) w = 200;
      if (w > state.width - 300) w = state.width - 300;
      containerEl.style.setProperty('--left-width', w + 'px');
    });
  });

  window.addEventListener('mouseup', () => {
    if (!isDragging) return;
    isDragging = false;
    resizeState = null;
    document.body.style.cursor = '';
    document.body.style.userSelect = '';
    const overlay = document.getElementById('resizer-overlay');
    if (overlay) overlay.remove();
    const w = parseInt(containerEl.style.getPropertyValue('--left-width'));
    saveAppSettings({ [settingKey]: w });
  });
}

// ========== Favorites ==========
async function loadFavorites() {
  const main = document.getElementById('main-content');
  main.innerHTML = `
    <div class="page active" data-page="favorites">
      <div class="toolbar">
        <span style="font-size:14px;font-weight:600;">收藏夹</span>
        <span id="fav-count" style="color:var(--text-secondary);font-size:12px;"></span>
      </div>
      <div class="library-layout" id="fav-layout" style="--left-width:380px;">
        <div class="skill-list" id="fav-list"></div>
        <div class="resizer" id="fav-resizer"></div>
        <div class="skill-detail" id="fav-detail">
          <div class="empty-state">选择左侧技能查看详情</div>
        </div>
      </div>
    </div>
  `;

  initResizer(document.getElementById('fav-resizer'), document.getElementById('fav-layout'), 'favLeftWidth');

  const res = await fetch(api('/api/favorites'));
  const favs = await res.json();
  favoriteIds = new Set(favs.map(f => f.id));
  librarySkills = favs; // reuse renderSkillList
  selectedSkillId = null;
  renderFavList();
  document.getElementById('fav-count').textContent = `共 ${favs.length} 个收藏`;
}

function renderFavList() {
  const container = document.getElementById('fav-list');
  if (!container) return;

  const html = librarySkills.map(sk => `
    <div class="skill-item ${sk.id === selectedSkillId ? 'active' : ''}" data-id="${sk.id}">
      <div class="name">
        ${escapeHtml(sk.folderName)}
        <span class="fav-star active" data-id="${sk.id}" title="取消收藏">★</span>
      </div>
      <div class="desc">${escapeHtml(sk.descriptionZh || sk.description || '暂无描述')}</div>
      <div class="meta">
        <span class="badge ${sk.hasTranslation ? 'translated' : ''}">${sk.hasTranslation ? '已翻译' : '未翻译'}</span>
        <span>${formatDate(sk.createdTime)}</span>
      </div>
    </div>
  `).join('');

  container.innerHTML = html || '<div class="empty-state" style="padding:20px;">暂无收藏技能</div>';

  container.querySelectorAll('.skill-item').forEach(el => {
    el.addEventListener('click', (e) => {
      if (e.target.classList.contains('fav-star')) return;
      selectedSkillId = el.dataset.id;
      renderFavList();
      loadFavDetail(selectedSkillId);
    });
  });

  container.querySelectorAll('.fav-star').forEach(el => {
    el.addEventListener('click', async (e) => {
      e.stopPropagation();
      const id = el.dataset.id;
      await fetch(api(`/api/favorites/${id}`), { method: 'DELETE' });
      favoriteIds.delete(id);
      librarySkills = librarySkills.filter(s => s.id !== id);
      renderFavList();
      if (currentPage === 'library') renderSkillList();
    });
  });
}

async function loadFavDetail(id) {
  const container = document.getElementById('fav-detail');
  const res = await fetch(api(`/api/library/${id}/content`));
  const data = await res.json();
  const isFav = favoriteIds.has(id);

  const displayContent = data.zhContent || data.content || '';
  const langBadge = data.zhContent ? '<span class="badge translated">中文</span>' : '<span class="badge">原文</span>';

  container.innerHTML = `
    <h2>${escapeHtml(data.folderName)} ${langBadge}</h2>
    <div class="detail-actions">
      <button class="btn btn-danger" onclick="deleteSkill('${id}')">删除技能</button>
      <button class="btn" onclick="openFolder('${data.skillMdPath.replace(/\\/g, '\\\\').replace(/'/g, "\\'")}')">打开文件夹</button>
      <button class="btn ${isFav ? 'btn-primary' : ''}" id="detail-fav-btn">${isFav ? '★ 已收藏' : '☆ 收藏'}</button>
    </div>
    <div class="markdown-body">${simpleMarkdown(displayContent)}</div>
  `;

  const favBtn = document.getElementById('detail-fav-btn');
  if (favBtn) {
    favBtn.addEventListener('click', async () => {
      if (favoriteIds.has(id)) {
        await fetch(api(`/api/favorites/${id}`), { method: 'DELETE' });
        favoriteIds.delete(id);
        librarySkills = librarySkills.filter(s => s.id !== id);
        renderFavList();
      } else {
        await fetch(api('/api/favorites'), { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ skillId: id }) });
        favoriteIds.add(id);
      }
      loadFavDetail(id);
      if (currentPage === 'library') renderSkillList();
    });
  }
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
      statusEl.textContent = `扫描完成，发现 ${status.resultCount} 个新技能`;
      const rres = await fetch(api('/api/scan/results'));
      const data = await rres.json();
      scanResults = data.skills || [];
      renderScanResults();
      if (scanResults.length > 0) importBtn.style.display = 'inline-block';
    } else if (status.status === 'failed') {
      clearInterval(scanTimer);
      statusEl.textContent = '扫描失败: ' + status.error;
    }
  }, 1500);
}

function renderScanResults() {
  const container = document.getElementById('scan-content');
  if (!scanResults.length) {
    container.innerHTML = '<div class="empty-state">暂无新增技能，点击"开始全电脑扫描"</div>';
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
              <span class="status status-new">新发现</span>
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
    body: JSON.stringify({}),
  });

  const result = await res.json();
  alert(`入库完成！导入 ${result.imported} 个，重命名 ${result.renamed} 个，跳过 ${result.skipped} 个。`);

  btn.disabled = false;
  btn.textContent = '增量入库';
  scanResults = [];
  renderScanResults();
}

// ========== Map (Workspaces) ==========
async function loadMap() {
  const main = document.getElementById('main-content');
  main.innerHTML = `
    <div class="page active" data-page="map">
      <div class="toolbar">
        <span style="color:var(--text-secondary);font-size:13px;">全局技能地图 — 展示技能工作区分布与重复情况</span>
      </div>
      <div class="library-layout" id="map-layout" style="--left-width:420px;">
        <div class="skill-list" id="map-zone-list"></div>
        <div class="resizer" id="map-resizer"></div>
        <div class="skill-detail" id="map-detail">
          <div class="empty-state">点击左侧工作区查看详情</div>
        </div>
      </div>
    </div>
  `;

  const settings = loadAppSettings();
  if (settings.mapLeftWidth) {
    document.getElementById('map-layout').style.setProperty('--left-width', settings.mapLeftWidth + 'px');
  }
  initResizer(document.getElementById('map-resizer'), document.getElementById('map-layout'), 'mapLeftWidth');

  const res = await fetch(api('/api/map'));
  const data = await res.json();
  renderMapZones(data);
}

function renderMapZones(data) {
  const container = document.getElementById('map-zone-list');
  const zones = data.zones || [];
  const dupSet = new Set(data.duplicateNames || []);

  if (!zones.length) {
    container.innerHTML = '<div class="empty-state" style="padding:20px;">暂无扫描数据</div>';
    return;
  }

  container.innerHTML = zones.map(z => {
    const dupCount = z.skills.filter(s => dupSet.has(s.folderName.toLowerCase())).length;
    return `
      <div class="skill-item" data-zone="${escapeHtml(z.zonePath)}">
        <div class="name">${escapeHtml(z.zonePath)}</div>
        <div class="desc">${z.skillCount} 个技能 ${dupCount > 0 ? `<span style="color:var(--warning);">(${dupCount} 个重复)</span>` : ''}</div>
      </div>
    `;
  }).join('');

  container.querySelectorAll('.skill-item').forEach(el => {
    el.addEventListener('click', () => {
      container.querySelectorAll('.skill-item').forEach(x => x.classList.remove('active'));
      el.classList.add('active');
      const zonePath = el.dataset.zone;
      const zone = zones.find(z => z.zonePath === zonePath);
      renderMapDetail(zone, dupSet);
    });
  });
}

function renderMapDetail(zone, dupSet) {
  const container = document.getElementById('map-detail');
  if (!zone) {
    container.innerHTML = '<div class="empty-state">点击左侧工作区查看详情</div>';
    return;
  }

  container.innerHTML = `
    <h2>${escapeHtml(zone.zonePath)}</h2>
    <div class="detail-actions">
      <button class="btn" onclick="openFolder('${escapeHtml(zone.zonePath.replace(/\\/g, '\\\\').replace(/'/g, "\\'"))}')">打开文件夹</button>
    </div>
    <div style="margin-top:12px;">
      ${zone.skills.map(s => `
        <div class="scan-skill-row" style="border-bottom:1px solid var(--border);padding:10px 0;">
          <div class="info">
            <div class="name">${escapeHtml(s.folderName)} ${dupSet.has(s.folderName.toLowerCase()) ? '<span style="color:var(--warning);font-size:11px;">[重复]</span>' : ''}</div>
            <div class="path">${formatDate(s.createdTime)}</div>
          </div>
          <button class="btn" style="font-size:11px;padding:2px 8px;" onclick="openFolder('${escapeHtml(s.skillMdPath.replace(/\\/g, '\\\\').replace(/'/g, "\\'"))}')">打开</button>
        </div>
      `).join('')}
    </div>
  `;
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

  html = html.replace(/```([\s\S]*?)```/g, '<pre><code>$1</code></pre>');
  html = html.replace(/`([^`]+)`/g, '<code>$1</code>');
  html = html.replace(/^#### (.*$)/gim, '<h4>$1</h4>');
  html = html.replace(/^### (.*$)/gim, '<h3>$1</h3>');
  html = html.replace(/^## (.*$)/gim, '<h2>$1</h2>');
  html = html.replace(/^# (.*$)/gim, '<h1>$1</h1>');
  html = html.replace(/\*\*\*(.*?)\*\*\*/g, '<strong><em>$1</em></strong>');
  html = html.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
  html = html.replace(/\*(.*?)\*/g, '<em>$1</em>');
  html = html.replace(/^&gt; (.*$)/gim, '<blockquote>$1</blockquote>');
  html = html.replace(/^\- (.*$)/gim, '<ul><li>$1</li></ul>');
  html = html.replace(/^\d+\. (.*$)/gim, '<ol><li>$1</li></ol>');
  html = html.replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2" target="_blank" style="color:var(--accent)">$1</a>');
  html = html.replace(/\n/g, '<br>');
  html = html.replace(/<\/ul><br><ul>/g, '');
  html = html.replace(/<\/ol><br><ol>/g, '');

  return html;
}

// ========== Init ==========
document.addEventListener('DOMContentLoaded', initRouter);
