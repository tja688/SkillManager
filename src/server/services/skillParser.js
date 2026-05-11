const fs = require('fs');
const path = require('path');

/**
 * 解析 SKILL.md 文件
 * @param {string} skillMdPath
 * @returns {{description: string, skillTitle: string, whenToUse: string, content: string}}
 */
function parseSkillMd(skillMdPath) {
  if (!fs.existsSync(skillMdPath)) {
    return { description: '', skillTitle: '', whenToUse: '', content: '' };
  }

  const content = fs.readFileSync(skillMdPath, 'utf-8');
  return parseSkillContent(content);
}

/**
 * 解析 SKILL.md 文本内容
 * @param {string} content
 * @returns {{description: string, skillTitle: string, whenToUse: string, content: string}}
 */
function parseSkillContent(content) {
  let description = '';
  let skillTitle = '';
  let whenToUse = '';

  // 1. 解析 YAML frontmatter
  const frontmatterMatch = content.match(/^---\s*\n([\s\S]*?)\n---\s*\n/);
  if (frontmatterMatch) {
    const fm = frontmatterMatch[1];
    const descMatch = fm.match(/^description:\s*(.+)$/m);
    if (descMatch) {
      description = descMatch[1].trim();
    }
  }

  // 2. 解析一级标题
  const titleMatch = content.match(/^#\s+(.+)$/m);
  if (titleMatch) {
    skillTitle = titleMatch[1].trim();
  }

  // 3. 解析 When to Use / 使用场景 段落
  const sectionRegex = /^##\s*(When to Use|Overview|About|使用场景|能做什么|功能)/im;
  const sectionMatch = content.match(sectionRegex);
  if (sectionMatch) {
    const sectionStart = content.indexOf(sectionMatch[0]);
    const afterSection = content.slice(sectionStart + sectionMatch[0].length);
    // 取到下一个 ## 或文件结束，最多 15 行
    const nextHeading = afterSection.search(/^##\s/m);
    let sectionText = nextHeading > -1 ? afterSection.slice(0, nextHeading) : afterSection;
    const lines = sectionText.split('\n').filter(l => l.trim() !== '');
    whenToUse = lines.slice(0, 15).join('\n').trim();
  }

  return { description, skillTitle, whenToUse, content };
}

module.exports = { parseSkillMd, parseSkillContent };
