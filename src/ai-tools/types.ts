/**
 * ═══════════════════════════════════════════════════════════════════
 *  AI Tools Directory — Types
 *  دیتاست جامع ابزارهای هوش مصنوعی
 *  منابع: AIxploria • Futurepedia • There's An AI For That • Toolify
 * ═══════════════════════════════════════════════════════════════════
 */

/** مدل قیمت‌گذاری ابزار (معادل برچسب‌های AIxploria: Gratuit/Freemium/Payant/Essai-Gratuit) */
export type Pricing = "free" | "freemium" | "paid" | "free-trial";

/** دایرکتوری‌هایی که ابزار از آن‌ها استخراج شده */
export type DirectorySource = "aixploria" | "futurepedia" | "taaft" | "toolify";

/** شناسه گروه‌های اصلی (بر اساس دسته‌بندی کلان AIxploria) */
export type GroupId =
  | "productivity"
  | "assistants"
  | "video"
  | "writing"
  | "art"
  | "automation"
  | "audio"
  | "business"
  | "data-research"
  | "code"
  | "entertainment"
  | "industry"
  | "featured";

/** شناسه همه دسته‌بندی‌های تخصصی (۹۳ دسته) */
export type CategoryId =
  // ── Assistants ─────────────────────────────
  | "ai-chat"
  | "chatbots"
  | "ai-characters"
  | "life-assistants"
  | "legal-assistants"
  | "llm-models"
  | "ai-coaching"
  // ── Art & Design ───────────────────────────
  | "image-generators"
  | "image-editing"
  | "art"
  | "logo-creation"
  | "3d-model"
  | "photo"
  // ── Video ──────────────────────────────────
  | "video-generators"
  | "text-to-video"
  | "video-editing"
  | "avatars"
  // ── Audio & Music ──────────────────────────
  | "music"
  | "audio-editing"
  | "text-to-speech"
  | "voice-cloning"
  | "transcriber"
  | "voice-assistant"
  | "voice-changer"
  | "sound"
  | "odd-sfx"
  // ── Writing ────────────────────────────────
  | "writing-seo"
  | "text-generators"
  | "storytelling"
  | "prompts"
  | "summarizer"
  | "translation"
  | "copywriting"
  | "paraphraser"
  | "copy-editors"
  | "content-creation"
  | "thesis"
  | "describer"
  | "prompt-market"
  | "plagiarism"
  // ── Productivity ───────────────────────────
  | "email"
  | "education"
  | "memory"
  | "search-engine"
  | "presentation"
  | "productivity"
  | "files-spreadsheets"
  | "chatgpt-extensions"
  | "notebook"
  | "qr-codes"
  | "meetings"
  | "file-conversion"
  // ── Business ───────────────────────────────
  | "business"
  | "ecommerce"
  | "finance"
  | "marketing"
  | "social-networks"
  | "human-resources"
  | "seo"
  | "customer-support"
  | "sales"
  | "telephone-calls"
  // ── Data & Research ────────────────────────
  | "data-analytics"
  | "ai-detection"
  | "research-science"
  // ── Code & Dev ─────────────────────────────
  | "code-assistant"
  | "no-code"
  | "developer-tools"
  | "github-projects"
  | "websites-design"
  | "copilot"
  | "databases"
  // ── Entertainment ──────────────────────────
  | "face-swap"
  | "fashion"
  | "amazing"
  | "games"
  | "dating"
  | "travel"
  | "nutrition"
  | "diy-makers"
  | "for-adults"
  // ── Industry ───────────────────────────────
  | "real-estate"
  | "robots-devices"
  | "healthcare"
  | "assistive-tech"
  | "cybersecurity"
  | "traffic"
  | "smart-homes"
  // ── Featured ───────────────────────────────
  | "ai-simulation"
  | "future-tools"
  | "supertools"
  | "ai-agents"
  | "automation";

/** گروه اصلی (دسته‌بندی کلان) */
export interface AIGroup {
  id: GroupId;
  /** نام انگلیسی گروه */
  name: string;
  /** نام فارسی گروه */
  nameFa: string;
  icon: string;
}

/** دسته‌بندی تخصصی */
export interface AICategory {
  id: CategoryId;
  /** گروهی که این دسته زیرمجموعه آن است */
  groupId: GroupId;
  /** نام انگلیسی دسته */
  name: string;
  /** نام فارسی دسته */
  nameFa: string;
  icon: string;
  /** لینک دسته در منبع اصلی (AIxploria) برای استخراج بیشتر */
  sourceUrl: string;
}

/** یک ابزار هوش مصنوعی */
export interface AITool {
  /** شناسه یکتا (kebab-case) */
  id: string;
  /** نام ابزار */
  name: string;
  /** توضیح کوتاه انگلیسی */
  description: string;
  /** توضیح کوتاه فارسی */
  descriptionFa: string;
  /** آیکن ابزار (ایموجی — برای لوگوی واقعی از getIconUrl() استفاده کنید) */
  icon: string;
  /** دسته‌بندی‌ها (اولین مورد = دسته اصلی) */
  categories: CategoryId[];
  /** آدرس رسمی ابزار */
  url: string;
  /**
   * لینک مستقیم صفحه ورود (فقط برای ابزارهایی که مسیر ورود عمومی‌شان
   * مشخص است). اگر خالی بود، از `getLoginUrl()` استفاده کنید که به
   * صفحه اصلی ابزار بازمی‌گردد (دکمه Sign in در همان‌جاست).
   */
  loginUrl?: string;
  /** مدل قیمت‌گذاری */
  pricing: Pricing;
  /** ابزار شناخته‌شده/محبوب در دایرکتوری‌ها */
  popular?: boolean;
  /** متن‌باز است */
  openSource?: boolean;
  /** دایرکتوری‌هایی که این ابزار در آن‌ها دیده شده */
  sources?: DirectorySource[];
}
