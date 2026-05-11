# SkillManager v2

> 去中心化技能库管理工具 —— 基于 Node.js + Express + 原生 Web 技术栈

## 简介

SkillManager 是一个**以备份为主题的技能库管理 + 轻量扫描聚合工具**。它帮助你将散落在电脑各处的 AI 技能（`SKILL.md`）聚合到一个统一的工作区中，并提供查看、搜索、翻译管理、全局地图等能力。

## 核心特性

- **单工作区管理**：指定一个目录作为技能仓库，集中管理，支持迁移
- **全电脑扫描**：自动发现所有盘符下符合 `skills/技能文件夹/SKILL.md` 标准的技能
- **增量入库**：智能去重，同名技能自动比对内容，有差异则重命名为 `_1`、`_2`...
- **中文翻译支持**：每份技能维护独立的 `SKILL.zh.md` 翻译文件，有则显示中文，无则回退英文
- **翻译状态追踪**：实时展示已翻译 / 未翻译统计，暴露翻译文件给外部 Agent 批量处理
- **全局技能地图**：展示全电脑技能分布，高亮重复技能，支持一键跳转到文件夹
- **零前端框架**：纯 HTML/CSS/JS，无构建工具链，极简维护

## 快速开始

### 环境要求

- [Node.js](https://nodejs.org/) >= 18

### 安装与启动

```bash
npm install
npm start
```

启动后会自动打开浏览器访问 `http://localhost:3456`。

### 目录结构

```
SkillManager/
├── package.json
├── start.js
├── README.md
└── src/
    ├── server/         # Express 后端
    │   ├── index.js
    │   ├── routes/
    │   └── services/
    └── client/         # 前端页面
        ├── index.html
        ├── styles.css
        └── app.js
```

## 工作区目录结构

SkillManager 使用单一工作区（默认 `./library`）：

```
library/
├── .agents/
│   └── skills/
│       └── SKILL.md          # 翻译指导技能（自动生成）
├── .library_index.json       # 技能库索引
├── .translation_index.json   # 翻译状态索引
├── .scan_cache.json          # 最近一次扫描缓存
└── [skill-folder]/           # 入库的技能
    ├── SKILL.md
    └── SKILL.zh.md           # 可选：中文翻译
```

## 使用指南

### 1. 扫描入库

进入"扫描入库"页面，点击"开始全电脑扫描"。扫描完成后，技能会按创建时间分为：
- 最近一个月（默认展开）
- 最近半年
- 一年以上

点击"增量入库"即可将新技能复制到工作区。

### 2. 查看与管理技能

"技能库"页面左侧为技能条目（显示原名 + 描述），右侧为详情面板。支持：
- 实时搜索（名字 / 描述）
- 按翻译状态筛选（全部 / 已翻译 / 未翻译）
- 删除技能

### 3. 中文翻译

软件**不内置翻译能力**。翻译通过外部 Agent 完成：

1. 工作区初始化时会自动生成 `.agents/skills/SKILL.md`（翻译指导技能）
2. 在"技能库"页面底部的翻译状态面板中，可查看未翻译列表
3. 外部 Agent 读取各技能的 `SKILL.md`，写入同目录的 `SKILL.zh.md`
4. 刷新页面即可看到中文内容

### 4. 全局技能地图

"全局地图"页面展示：
- 全电脑所有发现的技能位置
- **重复技能高亮**：同名技能出现在多处时聚合展示
- 点击"打开"可直接在资源管理器中定位

## 增量入库规则

以技能文件夹名字为判断依据：
1. 若工作区中**不存在同名**文件夹 → 直接复制
2. 若工作区中**存在同名**文件夹 → 比对 `SKILL.md` 内容 hash
   - hash 相同 → 跳过（视为同一技能）
   - hash 不同 → 自动重命名为 `原名_1`、`原名_2`... 后复制

## API 参考

| 接口 | 方法 | 说明 |
|------|------|------|
| `/api/config` | GET / POST | 读取 / 设置工作区路径 |
| `/api/config/migrate` | POST | 迁移工作区到新路径 |
| `/api/library` | GET | 获取库中技能（支持 `search`、`translated` 过滤） |
| `/api/library/:id/content` | GET | 获取技能详情（含中英文内容） |
| `/api/library/import` | POST | 增量入库（无 body 时从 scan cache 读取） |
| `/api/library/translation/status` | GET | 翻译状态统计 |
| `/api/scan` | POST | 触发全电脑扫描 |
| `/api/scan/status` | GET | 扫描任务状态 |
| `/api/scan/results` | GET | 最近一次扫描结果 |
| `/api/map` | GET | 全局技能地图（含重复分布） |
| `/api/map/open` | POST | 打开资源管理器定位到路径 |

## 技术栈

- **后端**：Node.js + Express
- **前端**：原生 HTML5 + CSS3 + JavaScript（无框架）
- **无构建工具链**：直接运行，零编译等待

## License

MIT
