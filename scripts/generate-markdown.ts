/**
 * تولید یک سند Markdown خوانا از کل دیتاست
 * اجرا: npm run generate  →  خروجی: AI-TOOLS-LIST.md
 */
import { writeFileSync } from "node:fs";
import {
  AI_CATEGORIES,
  AI_GROUPS,
  AI_TOOLS,
  PRICING_LABELS_FA,
  getLoginUrl,
  getStats,
  getToolsByCategory,
} from "../src/ai-tools/index.js";

const lines: string[] = [];
const stats = getStats();

lines.push("# 🤖 فهرست جامع ابزارهای هوش مصنوعی");
lines.push("");
lines.push(
  `**${stats.totalTools} ابزار** در **${stats.totalCategories} دسته تخصصی** — استخراج و ادغام‌شده از چهار دایرکتوری معتبر:`,
);
lines.push(
  "[AIxploria](https://www.aixploria.com/en/ultimate-list-ai/) • [Futurepedia](https://www.futurepedia.io) • [There's An AI For That](https://theresanaiforthat.com) • [Toolify](https://www.toolify.ai)",
);
lines.push("");
lines.push("> ✅ برای هر ابزار: لینک رسمی سایت + لینک ورود (Login) + نوع قیمت‌گذاری");
lines.push("> 📌 اگر صفحه ورود اختصاصی ناشناخته بود، لینک ورود = صفحه اصلی (دکمه Sign in در همان‌جاست).");
lines.push("");
lines.push("---");

for (const group of AI_GROUPS) {
  const categories = AI_CATEGORIES.filter((c) => c.groupId === group.id);
  if (categories.length === 0) continue;
  lines.push("");
  lines.push(`## ${group.icon} ${group.nameFa} (${group.name})`);
  lines.push("");

  for (const cat of categories) {
    const tools = getToolsByCategory(cat.id);
    if (tools.length === 0) continue;
    lines.push(`### ${cat.icon} ${cat.nameFa} — ${cat.name} (${tools.length} ابزار)`);
    lines.push(`🔗 [همه ابزارهای این دسته در AIxploria](${cat.sourceUrl})`);
    lines.push("");
    lines.push("| # | ابزار | توضیح | ورود | قیمت |");
    lines.push("|---|-------|-------|------|------|");
    let rank = 0;
    for (const tool of [...tools].sort(
      (a, b) => Number(b.popular ?? false) - Number(a.popular ?? false),
    )) {
      rank++;
      const star = tool.popular ? " ⭐" : "";
      const oss = tool.openSource ? " 🔓" : "";
      lines.push(
        `| ${rank} | ${tool.icon} **[${tool.name}](${tool.url})**${star}${oss} | ${tool.descriptionFa} | [ورود](${getLoginUrl(tool)}) | ${PRICING_LABELS_FA[tool.pricing]} |`,
      );
    }
    lines.push("");
  }
}

lines.push("---");
lines.push("");
lines.push(
  `— ⭐ = محبوب در دایرکتوری‌ها • 🔓 = متن‌باز — تولیدشده به‌صورت خودکار از \`src/ai-tools/\` توسط اسکریپت TypeScript`,
);
lines.push("");

writeFileSync(new URL("../AI-TOOLS-LIST.md", import.meta.url), lines.join("\n"), "utf8");

console.log(`✅ AI-TOOLS-LIST.md generated`);
console.log(
  `   ${stats.totalTools} tools • ${stats.totalCategories} categories • ${stats.free} free • ${stats.openSource} open-source`,
);
