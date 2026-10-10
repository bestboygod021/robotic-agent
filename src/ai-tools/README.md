# 🤖 AI Tools Directory — ماژول TypeScript

دیتاست جامع **۵۲۷ ابزار هوش مصنوعی** در **۶۶ دسته‌بندی تخصصی** (۱۳ گروه اصلی) —
استخراج و ادغام‌شده از چهار دایرکتوری معتبر جهانی:

| منبع | آدرس | سهم |
|------|-------|-----|
| AIxploria | [aixploria.com/en/ultimate-list-ai](https://www.aixploria.com/en/ultimate-list-ai/) | منبع اصلی: تاکسونومی کامل + Top 10 همه دسته‌ها |
| Futurepedia | [futurepedia.io](https://www.futurepedia.io) | ابزارهای محبوب و ترند |
| There's An AI For That | [theresanaiforthat.com](https://theresanaiforthat.com) | ابزارهای برگزیده (کل ایندکس ۵۴٬۰۰۰+ ابزار است) |
| Toolify | [toolify.ai](https://www.toolify.ai) | ابزارهای تازه و ترند (کل ایندکس ۳۱٬۰۰۰+ ابزار) |

> سایت‌های فوق مجموعاً **ده‌ها هزار** ابزار را فهرست کرده‌اند؛ این دیتاست یک
> **گزینش جامع و کاربردی** از معتبرترین‌ها در هر دسته است (۵۲۷ ابزار). فیلد
> `sources` در هر ابزار نشان می‌دهد در کدام دایرکتوری دیده شده. برای استخراج
> کامل‌تر هر دسته، لینک `sourceUrl` همان دسته در AIxploria آمده است.

## 📁 ساختار

```
src/ai-tools/
├── types.ts                      # تایپ‌ها: AITool, AICategory, Pricing, ...
├── categories.ts                 # ۶۶ دسته تخصصی + ۱۳ گروه (نام فارسی + انگلیسی)
├── tools.ts                      # دیتاست کامل + توابع کمکی (جستجو، فیلتر...)
├── index.ts                      # export یکپارچه
├── data/
│   ├── assistants.ts             # چت/دستیار، چت‌بات، شخصیت، حقوقی، LLM
│   ├── media-creativity.ts       # تصویر، ویدیو، آواتار، لوگو، سه‌بعدی
│   ├── audio.ts                  # موسیقی، صدا، TTS، کلون صدا، رونویسی
│   ├── writing-productivity.ts   # نویسندگی، ترجمه، ایمیل، آموزش، ارائه...
│   ├── business-dev.ts           # کسب‌وکار، فروش، داده، کد، گیت‌هاب...
│   └── entertainment-industry.ts # بازی، سفر، املاک، ربات، سلامت، ایجنت‌ها
scripts/
└── generate-markdown.ts          # تولید AI-TOOLS-LIST.md از کل داده
```

## 🚀 استفاده

```ts
import {
  AI_TOOLS,
  AI_CATEGORIES,
  AI_GROUPS,
  getToolsByCategory,
  getLoginUrl,
  searchTools,
  getPopularTools,
  PRICING_LABELS_FA,
} from "./ai-tools/index.js";

// همه ابزارهای دسته «تولید ویدیو»
const videoTools = getToolsByCategory("video-generators");

// لینک ورود هر ابزار (اگر صفحه ورود اختصاصی ناشناخته بود → صفحه اصلی ابزار)
videoTools.forEach((t) => {
  console.log(t.name, "→", getLoginUrl(t));
});

// جستجو روی نام/توضیح/دسته (انگلیسی و فارسی)
const results = searchTools("ویدیو");

// رندر دسته‌بندی‌ها در UI (فارسی + آیکون)
AI_CATEGORIES.map((c) => ({ title: `${c.icon} ${c.nameFa}` }));
```

## 📊 مدل داده هر ابزار (`AITool`)

| فیلد | نوع | توضیح |
|------|-----|-------|
| `id` | `string` | شناسه یکتا (kebab-case) |
| `name` | `string` | نام ابزار |
| `description` | `string` | توضیح کوتاه (انگلیسی) |
| `categories` | `CategoryId[]` | دسته‌ها (اولی = دسته اصلی) |
| `url` | `string` | آدرس رسمی |
| `loginUrl?` | `string` | لینک مستقیم صفحه ورود (در صورت اطمینان) |
| `pricing` | `"free" \| "freemium" \| "paid" \| "free-trial"` | مدل قیمت‌گذاری |
| `popular?` | `boolean` | پرچم محبوبیت در دایرکتوری‌ها |
| `openSource?` | `boolean` | متن‌باز است |
| `sources?` | `("aixploria" \| "futurepedia" \| "taaft" \| "toolify")[]` | دایرکتوری‌های مبدأ |

## 🛠 دستورها

```bash
npm install          # نصب وابستگی‌ها
npm run typecheck    # بررسی TypeScript (strict)
npm run generate     # تولید AI-TOOLS-LIST.md (نسخه خوانای کامل دیتاست)
```

## ⚠️ نکات

- **لینک ورود:** فقط برای ابزارهایی که مسیر ورود عمومی‌شان با اطمینان مشخص است
  (مثل ChatGPT، Claude، GitHub، Notion، Canva، Midjourney، Runway، ElevenLabs...)
  فیلد `loginUrl` پر شده. برای بقیه، `getLoginUrl(tool)` به‌خودکار به `url`
  برمی‌گردد — جایی که لینک «ورود / ثبت‌نام» در هدر سایت هست. از حدس‌زدن URLهای
  نامطمئن جلوگیری شده تا لینک خراب نداشته باشید.
- **قیمت‌گذاری** بر اساس برچسب‌های AIxploria (رایگان/فریمیوم/پولی/تست‌رایگان)
  نرمال شده است.
- دنیای AI سریع تغییر می‌کند؛ برای افزودن ابزار جدید، کافیست آن را در فایل
  مربوط در `data/` با همین ساختار اضافه کنید و `npm run generate` را اجرا کنید.
