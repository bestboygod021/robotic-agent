/**
 * 🧩 تکمیل دسته‌های کم‌جمعیت تا نزدیک ۲۵ ابزار
 * prompts • email • presentation • no-code • amazing • dating • assistive-tech
 * supertools • copy-editors • describer • prompt-market • plagiarism • notebook
 * qr-codes • meetings • file-conversion • voice-changer • sound • telephone-calls
 * copilot • databases • cybersecurity • for-adults • writing-seo
 * text-to-video • voice-cloning
 */
import type { AITool } from "../types.js";

export const EXPANSION_TOOLS: AITool[] = [
  // ── 💡 Prompts & Aids (+12) ────────────────────────────────────────────
  { id: "flowgpt", name: "FlowGPT", icon: "🌊", description: "Community platform to share and discover the best ChatGPT prompts", descriptionFa: "جامعه اشتراک و کشف بهترین پرامپت‌های ChatGPT", categories: ["prompts", "prompt-market"], url: "https://flowgpt.com", pricing: "free", popular: true },
  { id: "snack-prompt", name: "Snack Prompt", icon: "🍿", description: "Curated prompts with upvotes from the community", descriptionFa: "پرامپت‌های منتخب با رأی جامعه", categories: ["prompts"], url: "https://snackprompt.com", pricing: "free" },
  { id: "promptperfect", name: "PromptPerfect", icon: "🎯", description: "Automatically optimize prompts for any LLM", descriptionFa: "بهینه‌سازی خودکار پرامپت برای هر LLM", categories: ["prompts", "developer-tools"], url: "https://promptperfect.jina.ai", pricing: "freemium" },
  { id: "learnprompting", name: "Learn Prompting", icon: "🎓", description: "Free open-source prompt engineering course", descriptionFa: "دوره رایگان و متن‌باز مهندس پرامپت", categories: ["prompts", "education"], url: "https://learnprompting.org", pricing: "free", popular: true, openSource: true },
  { id: "awesome-chatgpt-prompts", name: "Awesome ChatGPT Prompts", icon: "⭐", description: "GitHub's most starred prompt collection", descriptionFa: "پرستاره‌ترین مجموعه پرامپت در گیت‌هاب", categories: ["prompts", "github-projects"], url: "https://github.com/f/awesome-chatgpt-prompts", pricing: "free", openSource: true, popular: true },
  { id: "promptstorm", name: "PromptStorm", icon: "⛈️", description: "Chrome extension with crafted prompts for ChatGPT/Gemini/Claude", descriptionFa: "افزونه کروم با پرامپت‌های آماده برای چت‌جی‌پی‌تی/جمنای/کلاد", categories: ["prompts", "chatgpt-extensions"], url: "https://promptstorm.app", pricing: "free" },
  { id: "promptvine", name: "PromptVine", icon: "🍇", description: "Collections of conversation templates", descriptionFa: "مجموعه قالب‌های گفت‌وگو", categories: ["prompts"], url: "https://promptvine.com", pricing: "free" },
  { id: "promptfolder", name: "PromptFolder", icon: "🗂", description: "Save, share and discover prompts", descriptionFa: "ذخیره، اشتراک و کشف پرامپت", categories: ["prompts"], url: "https://promptfolder.com", pricing: "freemium" },
  { id: "thepromptindex", name: "The Prompt Index", icon: "📇", description: "Free prompt database with categories", descriptionFa: "پایگاه پرامپت رایگان دسته‌بندی‌شده", categories: ["prompts"], url: "https://www.thepromptindex.com", pricing: "free" },
  { id: "prompts-chat", name: "prompts.chat", icon: "💬", description: "The original awesome prompts website", descriptionFa: "وب‌سایت اصلی پرامپت‌های معروف", categories: ["prompts"], url: "https://prompts.chat", pricing: "free" },
  { id: "promptomania", name: "PromptoMANIA", icon: "🎨", description: "Prompt generator for art models (MJ, SD, DALL·E)", descriptionFa: "جنراتور پرامپت برای مدل‌های هنری", categories: ["prompts", "art"], url: "https://promptomania.com", pricing: "free", popular: true },

  // ── 📧 Email (+9) ──────────────────────────────────────────────────────
  { id: "superhuman", name: "Superhuman", icon: "🦸", description: "The fastest email experience with AI", descriptionFa: "سریع‌ترین تجربه ایمیل با هوش", categories: ["email", "productivity"], url: "https://www.superhuman.com", pricing: "paid", popular: true },
  { id: "shortwave", name: "Shortwave", icon: "📻", description: "AI-native email with summaries and drafting", descriptionFa: "ایمیل هوشمند با خلاصه و پیش‌نویس‌نویسی", categories: ["email"], url: "https://www.shortwave.com", pricing: "freemium", popular: true },
  { id: "missive", name: "Missive", icon: "📮", description: "Team email with AI orked in", descriptionFa: "ایمیل تیمی با هوش توکار", categories: ["email", "business"], url: "https://missiveapp.com", pricing: "freemium" },
  { id: "spark-mail", name: "Spark Mail", icon: "✨", description: "Smart inbox with AI composer", descriptionFa: "اینباکس هوشمند با نویسنده AI", categories: ["email"], url: "https://sparkmailapp.com", pricing: "freemium", popular: true },
  { id: "hey-mail", name: "HEY", icon: "👋", description: "Email's heyday reinvents the inbox", descriptionFa: "بازطراحی صندوق ورودی با HEY", categories: ["email"], url: "https://hey.com", pricing: "paid", popular: true },
  { id: "mailbutler", name: "Mailbutler", icon: "🤵", description: "Gmail/Outlook extension with AI compose", descriptionFa: "افزونه جیمیل/اوت‌لوک با نویسنده AI", categories: ["email", "chatgpt-extensions"], url: "https://www.mailbutler.io", pricing: "freemium" },
  { id: "gmail-ai", name: "Gmail (Gemini)", icon: "📧", description: "Google's email with Gemini AI writing", descriptionFa: "ایمیل گوگل با نگارش هوشمند Gemini", categories: ["email"], url: "https://mail.google.com", pricing: "free", popular: true },
  { id: "outlook-ai", name: "Outlook (Copilot)", icon: "📨", description: "Microsoft email with Copilot", descriptionFa: "ایمیل مایکروسافت با کوپایلوت", categories: ["email"], url: "https://outlook.live.com", pricing: "free", popular: true },
  { id: "zoho-mail", name: "Zoho Mail (Zia)", icon: "📬", description: "Business email with Zia AI", descriptionFa: "ایمیل کسب‌وکاری با Zia AI", categories: ["email", "business"], url: "https://www.zoho.com/mail/", loginUrl: "https://accounts.zoho.com/signin", pricing: "freemium" },

  // ── 📽️ Presentation (+8) ────────────────────────────────────────────────
  { id: "decktopus", name: "Decktopus", icon: "🐙", description: "World's #1 AI presentation generator", descriptionFa: "جنراتور ارائه AI شماره یک جهان", categories: ["presentation"], url: "https://www.decktopus.com", pricing: "freemium", popular: true },
  { id: "presentations-ai", name: "Presentations.ai", icon: "🖥️", description: "AI presentations that build themselves", descriptionFa: "ارائه‌های AI که خودشان ساخته می‌شوند", categories: ["presentation"], url: "https://www.presentations.ai", pricing: "freemium", popular: true },
  { id: "plus-ai", name: "Plus AI", icon: "➕", description: "AI presentations inside Google Slides", descriptionFa: "ارائه هوشمند داخل گوگل اسلایدز", categories: ["presentation", "chatgpt-extensions"], url: "https://www.plusdocs.com", pricing: "freemium", popular: true },
  { id: "storydoc", name: "Storydoc", icon: "📑", description: "Interactive decks instead of static PDFs with AI", descriptionFa: "دک تعاملی به جای PDF با هوش", categories: ["presentation", "sales"], url: "https://www.storydoc.com", pricing: "freemium" },
  { id: "visme", name: "Visme", icon: "🎨", description: "Design hub for presentations and infographics", descriptionFa: "هاب طراحی برای ارائه و اینفوگرافیک", categories: ["presentation", "art"], url: "https://www.visme.co", loginUrl: "https://www.visme.co/login/", pricing: "freemium", popular: true },
  { id: "genially", name: "Genially", icon: "🧞", description: "Interactive animated presentations AI", descriptionFa: "ارائه تعاملی و متحرک با هوش", categories: ["presentation"], url: "https://genially.com", pricing: "freemium", popular: true },
  { id: "prezi", name: "Prezi AI", icon: "🌀", description: "Zoomable AI-powered presentations", descriptionFa: "ارائه زوم‌شونده با قدرت AI", categories: ["presentation"], url: "https://prezi.com/ai/", pricing: "freemium", popular: true },
  { id: "mentimeter", name: "Mentimeter", icon: "📊", description: "Interactive presentations with audience AI", descriptionFa: "ارائه تعاملی با AI مخاطب", categories: ["presentation", "education"], url: "https://www.mentimeter.com", pricing: "freemium", popular: true },

  // ── 🧱 No Code (+8) ────────────────────────────────────────────────────
  { id: "bubble", name: "Bubble", icon: "🫧", description: "Build full web apps with no code and AI assist", descriptionFa: "اپ کامل وب بدون کد با کمک AI", categories: ["no-code"], url: "https://bubble.io", loginUrl: "https://bubble.io/user/login", pricing: "freemium", popular: true },
  { id: "glide", name: "Glide", icon: "🛼", description: "Turn spreadsheets into apps with AI", descriptionFa: "تبدیل صفحه‌گسترده به اپ با هوش", categories: ["no-code", "files-spreadsheets"], url: "https://www.glideapps.com", pricing: "freemium", popular: true },
  { id: "softr", name: "Softr", icon: "🧊", description: "Build client portals from Airtable with AI", descriptionFa: "ساخت پورتال مشتری از Airtable با AI", categories: ["no-code"], url: "https://www.softr.io", pricing: "freemium", popular: true },
  { id: "adalo", name: "Adalo", icon: "📱", description: "No-code mobile app builder", descriptionFa: "سازنده اپ موبایل بدون کد", categories: ["no-code"], url: "https://www.adalo.com", pricing: "freemium", popular: true },
  { id: "flutterflow", name: "FlutterFlow", icon: "🦋", description: "Visual AI app builder on Flutter", descriptionFa: "سازنده اپ بصری AI روی فلاتر", categories: ["no-code", "developer-tools"], url: "https://flutterflow.io", pricing: "freemium", popular: true },
  { id: "retool", name: "Retool", icon: "🔧", description: "Build internal tools at speed with AI", descriptionFa: "ساخت سریع ابزار داخلی با AI", categories: ["no-code", "developer-tools"], url: "https://retool.com", pricing: "freemium", popular: true },
  { id: "toddle", name: "Toddle", icon: "🐣", description: "Visual web apps with built-in AI features", descriptionFa: "اپ وب بصری با قابلیت‌های AI داخلی", categories: ["no-code"], url: "https://toddle.dev", pricing: "freemium" },
  { id: "thunkable", name: "Thunkable", icon: "🧩", description: "No-code native app creation AI", descriptionFa: "ساخت اپ نیتیو بدون کد با AI", categories: ["no-code"], url: "https://thunkable.com", pricing: "freemium" },

  // ── 🤯 Amazing (+8) ────────────────────────────────────────────────────
  { id: "this-person-does-not-exist", name: "This Person Does Not Exist", icon: "👤", description: "AI-generated human faces that aren't real", descriptionFa: "چهره‌های انسانی تولیدشده با AI که واقعی نیستند", categories: ["amazing"], url: "https://thispersondoesnotexist.com", pricing: "free", popular: true },
  { id: "quick-draw", name: "Quick, Draw!", icon: "✏️", description: "Google's AI guesses your doodles", descriptionFa: "AI گوگل نقاشی‌های تو را حدس می‌زند", categories: ["amazing", "games"], url: "https://quickdraw.withgoogle.com", pricing: "free", popular: true },
  { id: "google-ai-experiments", name: "Google AI Experiments", icon: "🧪", description: "Playful AI demos with music, drawing and more", descriptionFa: "دموهای سرگرم‌کننده AI: موسیقی، نقاشی و بیشتر", categories: ["amazing"], url: "https://experiments.withgoogle.com/collection/ai", pricing: "free", popular: true },
  { id: "infinite-conversation", name: "Infinite Conversation", icon: "♾️", description: "An endless AI-generated podcast between Hertz and Rogan", descriptionFa: "پادکست بی‌پایان AI بین دو شخص", categories: ["amazing", "text-to-speech"], url: "https://infiniteconversation.com", pricing: "free", popular: true },
  { id: "talk-to-books", name: "Talk to Books", icon: "📚", description: "Explore books using experimental AI", descriptionFa: "کاوش کتاب‌ها با AI تجربی", categories: ["amazing", "education"], url: "https://books.google.com/talktobooks/", pricing: "free" },
  { id: "cat-gpt", name: "CatGPT", icon: "🐱", description: "Meow-powered AI assistant", descriptionFa: "دستیار AI قدرت‌گرفته از میو", categories: ["amazing", "ai-chat"], url: "https://cat-gpt.com", pricing: "free" },
  { id: "magic-sketchpad", name: "Magic Sketchpad", icon: "🎨", description: "Draw with an AI that completes strokes", descriptionFa: "نقاشی با AI که خطوط را کامل می‌کند", categories: ["amazing", "art"], url: "https://magic-sketchpad.glitch.me", pricing: "free" },
  { id: "nginx-thing", name: "Infinite Craft", icon: "🌋", description: "Combine elements endlessly with AI", descriptionFa: "ترکیب عناصر بی‌پایان با هوش", categories: ["amazing", "games"], url: "https://neal.fun/infinite-craft/", pricing: "free", popular: true },

  // ── ♿ Assistive (+3) ──────────────────────────────────────────────────
  { id: "orcam", name: "OrCam MyEye", icon: "👓", description: "AI wearable reader for visually impaired", descriptionFa: "خواننده پوشیدنی AI برای نابینایان", categories: ["assistive-tech"], url: "https://www.orcam.com", pricing: "paid", popular: true },
  { id: "esight", name: "eSight", icon: "🥽", description: "Electronic glasses powered by AI vision", descriptionFa: "عینک الکترونیکی با بینایی AI", categories: ["assistive-tech"], url: "https://www.esighteyewear.com", pricing: "paid" },
  { id: "whispp", name: "Whispp", icon: "🗣️", description: "AI converts whispers into natural speech for the speech-impaired", descriptionFa: "تبدیل زمزمه به گفتار طبیعی برای افراد ناتوان گفتاری", categories: ["assistive-tech", "text-to-speech"], url: "https://whispp.com", pricing: "paid", popular: true },

  // ── 🦸 SuperTools (+4) ─────────────────────────────────────────────────
  { id: "1min-ai", name: "1min.AI", icon: "⏱️", description: "All AI models and tools in one app", descriptionFa: "همه مدل و ابزار AI در یک اپ", categories: ["supertools", "ai-chat"], url: "https://1min.ai", loginUrl: "https://app.1min.ai/login", pricing: "freemium", popular: true },
  { id: "merlin-ai", name: "Merlin AI", icon: "🧙", description: "AI copilot in your browser for any website", descriptionFa: "کوپایلوت AI در مرورگر برای هر سایت", categories: ["supertools", "life-assistants"], url: "https://www.getmerlin.in", pricing: "freemium", popular: true },
  { id: "abacus-ai", name: "Abacus.AI", icon: "🧮", description: "Enterprise AI super-assistant: models, agents, chat", descriptionFa: "ابر-دستیار سازمانی: مدل، ایجنت، چت", categories: ["supertools", "developer-tools"], url: "https://abacus.ai", pricing: "paid", popular: true },
  { id: "askmanyai", name: "AskManyAI", icon: "❓", description: "Ask 100+ AI models at once", descriptionFa: "به بیش از ۱۰۰ مدل AI هم‌زمان سؤال کن", categories: ["supertools", "ai-chat"], url: "https://askmany.ai", pricing: "freemium" },

  // ── ✅ Copy Editors (+8) ────────────────────────────────────────────────
  { id: "prowritingaid", name: "ProWritingAid", icon: "🖋️", description: "Grammar checker, style editor and writing mentor", descriptionFa: "گرامرچک، ویرایشگر سبک و منتور نویسندگی", categories: ["copy-editors", "writing-seo"], url: "https://prowritingaid.com", pricing: "freemium", popular: true },
  { id: "whitesmoke", name: "WhiteSmoke", icon: "💨", description: "English writing correction AI", descriptionFa: "تصحیح نوشتار انگلیسی با AI", categories: ["copy-editors"], url: "https://www.whitesmoke.com", pricing: "paid" },
  { id: "ginger", name: "Ginger Software", icon: "🫚", description: "Grammar and punctuation checker AI", descriptionFa: "بررسی گرامر و نقطه‌گذاری با AI", categories: ["copy-editors"], url: "https://www.gingersoftware.com", pricing: "freemium", popular: true },
  { id: "slickwrite", name: "Slick Write", icon: "🖌", description: "Free powerful writing feedback tool", descriptionFa: "ابزار قدرتمند بازخورد نگارش رایگان", categories: ["copy-editors"], url: "https://www.slickwrite.com", pricing: "free" },
  { id: "writer-com", name: "Writer", icon: "📘", description: "Enterprise AI writing platform with style guardrails", descriptionFa: "پلتفرم نویسندگی سازمانی با چارچوب سبک", categories: ["copy-editors", "business"], url: "https://writer.com", pricing: "paid", popular: true },
  { id: "hemingway-editor", name: "Hemingway Editor", icon: "📖", description: "Make your writing bold and clear", descriptionFa: "نوشتن را قوی و شفاف کن", categories: ["copy-editors"], url: "https://hemingwayapp.com", pricing: "free", popular: true },
  { id: "scribens", name: "Scribens", icon: "✒️", description: "Free grammar checker in multiple languages", descriptionFa: "گرامرچک رایگان به چند زبان", categories: ["copy-editors", "translation"], url: "https://www.scribens.com", pricing: "free" },
  { id: "paper-rater", name: "PaperRater", icon: "🎓", description: "Free online proofreader with AI", descriptionFa: "نمونه‌خوان آنلاین رایگان با AI", categories: ["copy-editors", "plagiarism"], url: "https://paperrater.com", pricing: "free" },

  // ── 🔍 Describer (+5) ────────────────────────────────────────────────────
  { id: "azure-vision", name: "Azure AI Vision", icon: "👁", description: "Image analysis and description APIs", descriptionFa: "API تحلیل و توصیف تصویر", categories: ["describer", "developer-tools"], url: "https://azure.microsoft.com/en-us/products/ai-services/ai-vision", pricing: "freemium" },
  { id: "google-vision", name: "Google Cloud Vision", icon: "🌐", description: "Understand the content of images with AI", descriptionFa: "درک محتوای تصویر با هوش", categories: ["describer", "developer-tools"], url: "https://cloud.google.com/vision", pricing: "freemium", popular: true },
  { id: "clarifai", name: "Clarifai", icon: "🧠", description: "Full-stack AI recognition platform", descriptionFa: "پلتفرم فول‌استک تشخیص AI", categories: ["describer", "developer-tools"], url: "https://clarifai.com", loginUrl: "https://clarifai.com/login", pricing: "freemium", popular: true },
  { id: "img2prompt", name: "Img2Prompt (Replicate)", icon: "🖼", description: "Extract prompts from images", descriptionFa: "استخراج پرامپت از تصویر", categories: ["describer", "prompts"], url: "https://replicate.com/methexis-inc/img2prompt", pricing: "free", popular: true },
  { id: "astica-vision", name: "astica Vision", icon: "🔬", description: "Deep AI image description", descriptionFa: "توصیف عمیق تصویر با هوش", categories: ["describer"], url: "https://www.astica.ai/vision/", pricing: "free" },

  // ── 🛒 Prompt Market (+3) ─────────────────────────────────────────────
  { id: "laprompt", name: "LaPrompt", icon: "🏪", description: "Buy and sell AI art prompts", descriptionFa: "خرید و فروش پرامپت هنری AI", categories: ["prompt-market", "art"], url: "https://laprompt.com", pricing: "free" },
  { id: "prompti", name: "Prompti.ai", icon: "🎫", description: "Prompt marketplace with quality ratings", descriptionFa: "بازار پرامپت با رتبه کیفیت", categories: ["prompt-market"], url: "https://prompti.ai", pricing: "free" },
  { id: "prompt-hunt-market", name: "Prompt Hunt", icon: "🦌", description: "Discovery platform for creative prompts", descriptionFa: "پلتفرم کشف پرامپت خلاقانه", categories: ["prompt-market", "art"], url: "https://www.prompthunt.com", pricing: "freemium" },

  // ── 📄 Plagiarism (+5) ─────────────────────────────────────────────────
  { id: "quetext", name: "Quetext", icon: "🔍", description: "Deep plagiarism detection with AI reports", descriptionFa: "تشخیص عمیق کپی با گزارش AI", categories: ["plagiarism"], url: "https://www.quetext.com", pricing: "freemium", popular: true },
  { id: "duplichecker", name: "Duplichecker", icon: "✅", description: "Free plagiarism checker popular with students", descriptionFa: "کپی‌چک رایگان محبوب دانش‌آموزان", categories: ["plagiarism"], url: "https://www.duplichecker.com", pricing: "free", popular: true },
  { id: "plagscan", name: "PlagScan", icon: "📊", description: "Professional plagiarism detection", descriptionFa: "تشخیص حرفه‌ای سرقت ادبی", categories: ["plagiarism", "education"], url: "https://www.plagscan.com", pricing: "paid" },
  { id: "plagtracker", name: "PlagTracker", icon: "🕵", description: "Academic plagiarism checker", descriptionFa: "کپی‌چک دانشگاهی", categories: ["plagiarism"], url: "https://www.plagtracker.com", pricing: "free" },
  { id: "smallseotools-plag", name: "SmallSEOTools Plagiarism", icon: "🔧", description: "Free online plagiarism checker", descriptionFa: "کپی‌چک آنلاین رایگان", categories: ["plagiarism", "seo"], url: "https://smallseotools.com/plagiarism-checker/", pricing: "free", popular: true },

  // ── 📓 Notebook (+7) ───────────────────────────────────────────────────
  { id: "evernote", name: "Evernote", icon: "🐘", description: "Veteran notes app with AI search", descriptionFa: "اپ قدیمی یادداشت با جست‌وجوی AI", categories: ["notebook", "productivity"], url: "https://evernote.com", loginUrl: "https://www.evernote.com/Login.action", pricing: "freemium", popular: true },
  { id: "one-note", name: "OneNote (Copilot)", icon: "📒", description: "Microsoft's notebook with AI", descriptionFa: "دفترچه مایکروسافت با AI", categories: ["notebook"], url: "https://www.onenote.com", pricing: "free", popular: true },
  { id: "heptabase", name: "Heptabase", icon: "🗺️", description: "Visual note-taking for deep learners", descriptionFa: "یادداشت‌برداری بصری برای یادگیرندگان عمیق", categories: ["notebook", "education"], url: "https://heptabase.com", pricing: "paid", popular: true },
  { id: "logseq", name: "Logseq", icon: "📖", description: "Open-source privacy-first PKM", descriptionFa: "مدیریت دانش متن‌باز حریم‌محور", categories: ["notebook", "github-projects"], url: "https://logseq.com", pricing: "free", openSource: true, popular: true },
  { id: "tana", name: "Tana", icon: "🧠", description: "AI-native networked notes (supertags)", descriptionFa: "یادداشت شبکه‌ای بومی AI با سوپرتگ", categories: ["notebook"], url: "https://tana.inc", pricing: "freemium", popular: true },
  { id: "roam-research", name: "Roam Research", icon: "🌐", description: "Notes as networked thought", descriptionFa: "یادداشت به‌عنوان اندیشه شبکه‌ای", categories: ["notebook"], url: "https://roamresearch.com", pricing: "paid", popular: true },
  { id: "amplenote", name: "Amplenote", icon: "📝", description: "Notes that link back to your to-dos", descriptionFa: "یادداشت متصل به تودوها", categories: ["notebook", "productivity"], url: "https://www.amplenote.com", pricing: "freemium" },

  // ── 🔗 QR Codes (+4) ───────────────────────────────────────────────────
  { id: "flowcode", name: "Flowcode", icon: "🌊", description: "QR codes for brands with analytics", descriptionFa: "کدهای QR برندمحور با تحلیل", categories: ["qr-codes", "marketing"], url: "https://www.flowcode.com", pricing: "freemium", popular: true },
  { id: "unitag", name: "Unitag QR", icon: "🏷", description: "Custom QR code designer", descriptionFa: "طراح QR سفارشی", categories: ["qr-codes"], url: "https://www.unitag.io", pricing: "freemium" },
  { id: "webqr", name: "WebQR", icon: "🌐", description: "Free QR code generator + scanner online", descriptionFa: "جنراتور و اسکنر رایگان QR آنلاین", categories: ["qr-codes"], url: "https://webqr.com", pricing: "free" },
  { id: "qrfy", name: "QRFY", icon: "📨", description: "Dynamic QR codes with editing", descriptionFa: "QR داینامیک با ویرایش", categories: ["qr-codes"], url: "https://qrfy.com", pricing: "freemium" },

  // ── 👥 Meetings (+6) ───────────────────────────────────────────────────
  { id: "read-ai", name: "Read AI", icon: "📖", description: "AI meeting copilot with recaps and analytics", descriptionFa: "کوپایلوت جلسات AI با خلاصه و تحلیل", categories: ["meetings", "life-assistants"], url: "https://www.read.ai", pricing: "freemium", popular: true },
  { id: "avoma", name: "Avoma", icon: "🥑", description: "All-in-one meeting assistant and revenue intelligence", descriptionFa: "دستیار جلسه همه‌کاره و هوش درآمد", categories: ["meetings", "sales"], url: "https://avoma.com", pricing: "freemium", popular: true },
  { id: "sembly", name: "Sembly AI", icon: "📋", description: "Meeting notes, summaries and action items", descriptionFa: "یادداشت جلسه، خلاصه و اقدامات", categories: ["meetings", "notebook"], url: "https://www.sembly.ai", pricing: "freemium" },
  { id: "circleback", name: "Circleback", icon: "⭕", description: "AI meeting notes with meticulous action items", descriptionFa: "یادداشت جلسه AI با اقدامات دقیق", categories: ["meetings", "notebook"], url: "https://circleback.ai", pricing: "freemium", popular: true },
  { id: "meetjamie", name: "jamie", icon: "🍋", description: "Your AI note taker that respects privacy", descriptionFa: "یادداشت‌گیر AI شما با رعایت حریم خصوصی", categories: ["meetings", "notebook"], url: "https://www.meetjamie.ai", pricing: "freemium", popular: true },
  { id: "grain", name: "Grain", icon: "🌾", description: "Capture, clip and share meeting moments", descriptionFa: "ضبط، کلیپ و اشتراک لحظات جلسه", categories: ["meetings", "video-editing"], url: "https://grain.com", pricing: "freemium", popular: true },

  // ── 🔄 File Conversion (+8) ─────────────────────────────────────────────
  { id: "cloudconvert", name: "CloudConvert", icon: "☁️", description: "Convert anything to anything online", descriptionFa: "تبدیل هر فایل به هرچیزی آنلاین", categories: ["file-conversion"], url: "https://cloudconvert.com", pricing: "freemium", popular: true },
  { id: "convertio", name: "Convertio", icon: "🔁", description: "Fast free file conversion to 300+ formats", descriptionFa: "تبدیل سریع رایگان به بیش از ۳۰۰ فرمت", categories: ["file-conversion"], url: "https://convertio.co", pricing: "freemium", popular: true },
  { id: "zamzar", name: "Zamzar", icon: "🧙", description: "File conversion since 2006", descriptionFa: "تبدیل فایل از ۲۰۰۶", categories: ["file-conversion"], url: "https://www.zamzar.com", pricing: "freemium", popular: true },
  { id: "freeconvert", name: "FreeConvert", icon: "🆓", description: "Free online file converter", descriptionFa: "مبدل رایگان آنلاین فایل", categories: ["file-conversion"], url: "https://www.freeconvert.com", pricing: "free" },
  { id: "ilovepdf", name: "iLovePDF", icon: "❤️", description: "Every PDF tool you need with AI features", descriptionFa: "تمام ابزار PDF لازم با قابلیت‌های AI", categories: ["file-conversion", "files-spreadsheets"], url: "https://www.ilovepdf.com", pricing: "freemium", popular: true },
  { id: "smallpdf", name: "Smallpdf", icon: "📄", description: "Compress, convert and edit PDFs", descriptionFa: "فشرده‌سازی، تبدیل و ویرایش PDF", categories: ["file-conversion", "files-spreadsheets"], url: "https://smallpdf.com", loginUrl: "https://smallpdf.com/login", pricing: "freemium", popular: true },
  { id: "tinywow", name: "TinyWow", icon: "🛠️", description: "Free online tools for files, video and writing", descriptionFa: "ابزار رایگان برای فایل، ویدیو و نگارش", categories: ["file-conversion", "supertools"], url: "https://tinywow.com", pricing: "free", popular: true },
  { id: "pdf2go", name: "PDF2Go", icon: "🚗", description: "Edit and convert PDFs online free", descriptionFa: "ویرایش و تبدیل PDF آنلاین رایگان", categories: ["file-conversion"], url: "https://www.pdf2go.com", pricing: "freemium" },

  // ── 🎤 Voice Changer (+2) ────────────────────────────────────────────────
  { id: "magiccall", name: "MagicCall", icon: "📞", description: "Prank voice changer app with AI voices", descriptionFa: "اپ شوخی تغییر صدا با صداهای AI", categories: ["voice-changer", "telephone-calls"], url: "https://magiccall.co", pricing: "freemium" },
  { id: "parodist", name: "Parodist", icon: "🎭", description: "Celebrity-style voice pranks with AI", descriptionFa: "شوخی صدای سبک سلبریتی با AI", categories: ["voice-changer", "for-adults"], url: "https://parodist.app", pricing: "free" },

  // ── 🔊 Sound (+3) ──────────────────────────────────────────────────────
  { id: "zapsplat", name: "Zapsplat", icon: "🎧", description: "Free sound effects and music library", descriptionFa: "کتابخانه رایگان افکت صوتی و موسیقی", categories: ["sound"], url: "https://www.zapsplat.com", pricing: "free", popular: true },
  { id: "soundstripe", name: "Soundstripe", icon: "🎵", description: "AI-curated stock music for creators", descriptionFa: "موسیقی استوک منتخب AI برای سازندگان", categories: ["sound", "music"], url: "https://www.soundstripe.com", pricing: "paid", popular: true },
  { id: "sonniss", name: "Sonniss", icon: "🎛", description: "Professional game audio and SFX", descriptionFa: "صدای بازی حرفه‌ای و SFX", categories: ["sound", "games"], url: "https://sonniss.com", pricing: "freemium" },

  // ── 📞 Phone Calls (+5) ────────────────────────────────────────────────
  { id: "dialpad", name: "Dialpad", icon: "☎️", description: "AI voice, video and contact center platform", descriptionFa: "پلتفرم صدا، ویدیو و مرکز تماس AI", categories: ["telephone-calls", "customer-support"], url: "https://www.dialpad.com", pricing: "paid", popular: true },
  { id: "aircall", name: "Aircall", icon: "📟", description: "Cloud phone system with AI call summaries", descriptionFa: "سیستم تلفن ابری با خلاصه تماس AI", categories: ["telephone-calls", "business"], url: "https://aircall.io", pricing: "paid", popular: true },
  { id: "callrail", name: "CallRail", icon: "🚂", description: "Call tracking and conversation intelligence AI", descriptionFa: "ردیابی تماس و هوش مکالمه", categories: ["telephone-calls", "marketing"], url: "https://www.callrail.com", pricing: "paid" },
  { id: "plaud", name: "PLAUD Note", icon: "🎙️", description: "AI voice recorder that transcribes calls", descriptionFa: "ضبط‌کننده صدا AI که تماس‌ها را رونویسی می‌کند", categories: ["telephone-calls", "transcriber"], url: "https://www.plaud.ai", pricing: "paid", popular: true },
  { id: "ringcx", name: "RingCX (RingCentral)", icon: "📱", description: "AI contact center and cloud telephony", descriptionFa: "مرکز تماس AI و تلفنی ابری", categories: ["telephone-calls", "customer-support"], url: "https://www.ringcentral.com", pricing: "paid" },

  // ── ✈️ Copilot (+2) ────────────────────────────────────────────────────
  { id: "shopify-sidekick", name: "Shopify Sidekick", icon: "🛍️", description: "AI commerce copilot for Shopify merchants", descriptionFa: "کوپایلوت تجارت AI برای فروشندگان Shopify", categories: ["copilot", "ecommerce"], url: "https://www.shopify.com/magic", loginUrl: "https://accounts.shopify.com", pricing: "paid" },
  { id: "figjam-ai", name: "FigJam AI", icon: "🎨", description: "AI whiteboard copilot inside Figma", descriptionFa: "کوپایلوت تخته سفید AI در فیگما", categories: ["copilot", "websites-design"], url: "https://www.figma.com/figjam/ai/", loginUrl: "https://www.figma.com/login", pricing: "freemium" },

  // ── 🗄️ Databases (+5) ─────────────────────────────────────────────────
  { id: "pinecone", name: "Pinecone", icon: "🌲", description: "Vector database built for AI apps", descriptionFa: "پایگاه داده برداری برای اپ‌های AI", categories: ["databases", "developer-tools"], url: "https://www.pinecone.io", loginUrl: "https://app.pinecone.io", pricing: "freemium", popular: true },
  { id: "weaviate", name: "Weaviate", icon: "🕸️", description: "Open AI-native vector database", descriptionFa: "پایگاه برداری متن‌باز بومی AI", categories: ["databases", "github-projects"], url: "https://weaviate.io", pricing: "free", openSource: true, popular: true },
  { id: "chroma", name: "Chroma", icon: "🎨", description: "The AI-native open-source embedding database", descriptionFa: "پایگاه امبدینگ متن‌باز بومی AI", categories: ["databases", "github-projects"], url: "https://www.trychroma.com", pricing: "free", openSource: true, popular: true },
  { id: "qdrant", name: "Qdrant", icon: "🦀", description: "High-performance vector database in Rust", descriptionFa: "پایگاه برداری پرقدرت با Rust", categories: ["databases", "github-projects"], url: "https://qdrant.tech", pricing: "free", openSource: true, popular: true },
  { id: "milvus", name: "Milvus", icon: "🐋", description: "Open vector store at massive scale", descriptionFa: "پایگاه برداری باز در مقیاس عظیم", categories: ["databases", "github-projects"], url: "https://milvus.io", pricing: "free", openSource: true, popular: true },

  // ── 🛡️ Cybersecurity (+5) ─────────────────────────────────────────────
  { id: "darktrace", name: "Darktrace", icon: "🌑", description: "Self-learning cybersecurity AI for enterprises", descriptionFa: "هوش امنیتی خودآموز برای سازمان‌ها", categories: ["cybersecurity", "business"], url: "https://darktrace.com", pricing: "paid", popular: true },
  { id: "crowdstrike", name: "CrowdStrike Falcon", icon: "🦅", description: "AI endpoint protection at cloud scale", descriptionFa: "محافظ نقطه‌پایانی AI در مقیاس ابری", categories: ["cybersecurity"], url: "https://www.crowdstrike.com", loginUrl: "https://falcon.crowdstrike.com/login/", pricing: "paid", popular: true },
  { id: "sentinelone", name: "SentinelOne", icon: "🛡️", description: "Autonomous AI threat hunting platform", descriptionFa: "پلتفرم شکار تهدید خودگردان AI", categories: ["cybersecurity"], url: "https://www.sentinelone.com", pricing: "paid", popular: true },
  { id: "vectra-ai", name: "Vectra AI", icon: "🎯", description: "AI-powered detection of cyber threats", descriptionFa: "تشخیص تهدید سایبری با قدرت AI", categories: ["cybersecurity"], url: "https://www.vectra.ai", pricing: "paid" },
  { id: "abnormal-security", name: "Abnormal Security", icon: "📧", description: "AI email security against BEC attacks", descriptionFa: "امنیت ایمیل AI علیه حملات BEC", categories: ["cybersecurity", "email"], url: "https://abnormalsecurity.com", pricing: "paid" },

  // ── 🔞 For Adults (+2) ────────────────────────────────────────────────
  { id: "soulfun-ai", name: "SoulFun AI", icon: "💜", description: "AI companion chats for adults", descriptionFa: "چت همراه AI مخصوص بزرگسالان", categories: ["for-adults", "ai-characters"], url: "https://soulfun.ai", pricing: "freemium", sources: ["aixploria"] },
  { id: "candydating", name: "Candy AI", icon: "🍬", description: "Mature AI companion platform", descriptionFa: "پلتفرم همراه بالغ AI", categories: ["for-adults", "dating"], url: "https://candy.ai", pricing: "freemium", popular: true },

  // ── ✍️ Writing & Web SEO (+3) ─────────────────────────────────────────
  { id: "kwfinder", name: "KWFinder (Mangools)", icon: "🔑", description: "Find easy keywords with AI difficulty scores", descriptionFa: "یافتن کلمات کلیدی آسان با امتیاز دشواری AI", categories: ["writing-seo", "seo"], url: "https://mangools.com/kwfinder/", pricing: "freemium", popular: true },
  { id: "wincher", name: "Wincher", icon: "🪇", description: "AI rank tracker and keyword insights", descriptionFa: "ردیاب رتبه و بینش کلمات کلیدی AI", categories: ["writing-seo", "seo"], url: "https://www.wincher.com", pricing: "freemium" },
  { id: "marketingminer", name: "Marketing Miner", icon: "⛏️", description: "SEO data tools for marketers", descriptionFa: "ابزار داده SEO برای بازاریابان", categories: ["writing-seo", "seo"], url: "https://www.marketingminer.com", pricing: "freemium" },

  // ── 📝 Text-to-Video (+3) ─────────────────────────────────────────────
  { id: "vizard", name: "Vizard AI", icon: "🪄", description: "AI clips for TikTok/Shorts from long videos", descriptionFa: "کلیپ AI برای تیک‌تاک/شورتز از ویدیوهای بلند", categories: ["text-to-video", "video-editing"], url: "https://vizard.ai", pricing: "freemium", popular: true },
  { id: "rask-ai", name: "Rask AI", icon: "🌍", description: "AI video translator and dubbed generator", descriptionFa: "مترجم ویدیو و جنراتور دوبله AI", categories: ["text-to-video", "translation"], url: "https://www.rask.ai", pricing: "freemium", popular: true },
  { id: "clipfly", name: "Clipfly", icon: "🎬", description: "All-in-one AI video tools by Fotor", descriptionFa: "ابزار ویدیو همه‌کاره AI از Fotor", categories: ["text-to-video", "video-editing"], url: "https://www.clipfly.ai", pricing: "free" },

  // ── 🎙️ Voice Cloning (+3) ─────────────────────────────────────────────
  { id: "kits-ai", name: "Kits AI", icon: "🎤", description: "Ethical AI voice models for musicians", descriptionFa: "مدل‌های صوتی اخلاقی AI برای موزیسین‌ها", categories: ["voice-cloning", "music"], url: "https://www.kits.ai", pricing: "freemium", popular: true },
  { id: "uberduck", name: "Uberduck", icon: "🦆", description: "AI vocals and voice cloning for creators", descriptionFa: "وکل‌سازی و کلون صدای AI برای سازندگان", categories: ["voice-cloning", "voice-changer"], url: "https://uberduck.ai", pricing: "freemium", popular: true },
  { id: "coqui-xtts", name: "Coqui XTTS", icon: "🐸", description: "Open-source voice cloning in 17 languages", descriptionFa: "کلون صدای متن‌باز به ۱۷ زبان", categories: ["voice-cloning", "github-projects"], url: "https://github.com/coqui-ai/TTS", pricing: "free", openSource: true, popular: true },

  // ── 💘 Dating (+2) ────────────────────────────────────────────────────
  { id: "dates-ai", name: "Dates.ai", icon: "💘", description: "AI matchmaker that evolves with your conversations", descriptionFa: "دوست‌یاب AI که با گفت‌وگوها تکامل می‌یابد", categories: ["dating"], url: "https://www.dates.ai", pricing: "freemium", popular: true },
  { id: "iris-dating", name: "iris Dating", icon: "👁", description: "AI matching based on what visually attracts you", descriptionFa: "تطبیق AI بر اساس چیزی که بصری تو را جذب می‌کند", categories: ["dating"], url: "https://www.irisdating.com", pricing: "free", sources: ["aixploria"] },
];
