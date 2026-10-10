/**
 * 🗄️ دیتاست کامل ابزارهای هوش مصنوعی + توابع کمکی
 * استخراج‌شده از: AIxploria • Futurepedia • There's An AI For That • Toolify
 */
import type { AICategory, AITool, CategoryId, Pricing } from "./types.js";
import { AI_CATEGORIES } from "./categories.js";
import { ASSISTANT_TOOLS } from "./data/assistants.js";
import { MEDIA_TOOLS } from "./data/media-creativity.js";
import { AUDIO_TOOLS } from "./data/audio.js";
import { WRITING_PRODUCTIVITY_TOOLS } from "./data/writing-productivity.js";
import { BUSINESS_DEV_TOOLS } from "./data/business-dev.js";
import { ENTERTAINMENT_INDUSTRY_TOOLS } from "./data/entertainment-industry.js";
import { EXPANSION_TOOLS } from "./data/expansion.js";

/** همه ابزارها (بدون تکرار — هر ابزار یک ورودی با چند دسته) */
export const AI_TOOLS: AITool[] = [
  ...ASSISTANT_TOOLS,
  ...MEDIA_TOOLS,
  ...AUDIO_TOOLS,
  ...WRITING_PRODUCTIVITY_TOOLS,
  ...BUSINESS_DEV_TOOLS,
  ...ENTERTAINMENT_INDUSTRY_TOOLS,
  ...EXPANSION_TOOLS,
];

// ─────────────────────────────── Helpers ───────────────────────────────

const categoryMap = new Map<CategoryId, AICategory>(
  AI_CATEGORIES.map((c) => [c.id, c]),
);

/** توضیح فارسی مدل قیمت‌گذاری */
export const PRICING_LABELS_FA: Record<Pricing, string> = {
  free: "رایگان",
  freemium: "رایگان + نسخه پولی (Freemium)",
  paid: "پولی",
  "free-trial": "امکان رایگان / پولی",
};

export const PRICING_LABELS_EN: Record<Pricing, string> = {
  free: "Free",
  freemium: "Freemium",
  paid: "Paid",
  "free-trial": "Free trial",
};

/** پیدا کردن ابزار با id */
export function getToolById(id: string): AITool | undefined {
  return AI_TOOLS.find((t) => t.id === id);
}

/** ابزارهای یک دسته خاص */
export function getToolsByCategory(categoryId: CategoryId): AITool[] {
  return AI_TOOLS.filter((t) => t.categories.includes(categoryId));
}

/** ابزارهای محبوب (پرچم popular در دایرکتوری‌ها) */
export function getPopularTools(): AITool[] {
  return AI_TOOLS.filter((t) => t.popular);
}

/** ابزارهای رایگان */
export function getFreeTools(): AITool[] {
  return AI_TOOLS.filter((t) => t.pricing === "free");
}

/** ابزارهای متن‌باز */
export function getOpenSourceTools(): AITool[] {
  return AI_TOOLS.filter((t) => t.openSource);
}

/**
 * لینک ورود به ابزار — اگر صفحه ورود اختصاصی مشخص بود همان برمی‌گردد،
 * در غیر این صورت صفحه اصلی ابزار (دکمه‌ی Sign in / Log in معمولاً
 * بالای صفحه است). این رفتار برای همه ابزارها یک لینک معتبر تضمین می‌کند.
 */
export function getLoginUrl(tool: AITool): string {
  return tool.loginUrl ?? tool.url;
}

/**
 * لوگوی واقعی ابزار (favicon دامنه رسمی) به‌صورت زنده از گوگل.
 * مناسب برای نمایش تصویر برند در UI کنار ایموجی `tool.icon`.
 */
export function getIconUrl(tool: AITool, size = 128): string {
  const host = new URL(tool.url).hostname.replace(/^www\./, "");
  return `https://www.google.com/s2/favicons?domain=${host}&sz=${size}`;
}

/** مرور گرامر جستجوی ساده روی نام + توضیح + دسته */
export function searchTools(query: string): AITool[] {
  const q = query.trim().toLowerCase();
  if (!q) return AI_TOOLS;
  return AI_TOOLS.filter(
    (t) =>
      t.name.toLowerCase().includes(q) ||
      t.description.toLowerCase().includes(q) ||
      t.descriptionFa.includes(query) ||
      t.id.includes(q) ||
      t.categories.some((c) => categoryMap.get(c)?.name.toLowerCase().includes(q) ||
        categoryMap.get(c)?.nameFa.includes(q)),
  );
}

/** تعداد ابزار هر دسته */
export function getCategoryCount(categoryId: CategoryId): number {
  return getToolsByCategory(categoryId).length;
}

/** آمار کلی دیتاست */
export function getStats() {
  return {
    totalTools: AI_TOOLS.length,
    totalCategories: AI_CATEGORIES.length,
    popular: getPopularTools().length,
    free: getFreeTools().length,
    openSource: getOpenSourceTools().length,
  };
}
