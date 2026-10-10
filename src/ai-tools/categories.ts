/**
 * گروه‌ها و دسته‌بندی‌های تخصصی
 * ساختار بر اساس تاکسونومی AIxploria (۱۳ گروه اصلی + ۶۶ دسته تخصصی)
 */
import type { AICategory, AIGroup } from "./types.js";

// ──────────────────────────────── Groups ────────────────────────────────

export const AI_GROUPS: AIGroup[] = [
  { id: "productivity",   name: "Productivity & Work",      nameFa: "بهره‌وری و مدیریت کار",        icon: "📈" },
  { id: "assistants",     name: "Assistants & Chatbots",    nameFa: "دستیارها و چت‌بات‌ها",          icon: "🤖" },
  { id: "video",          name: "Video & Filmmaking",       nameFa: "ویدیو و ساخت فیلم",            icon: "🎬" },
  { id: "writing",        name: "Writing & Content",        nameFa: "نویسندگی و محتوای متنی",        icon: "✍️" },
  { id: "art",            name: "Art, Image & Design",      nameFa: "هنر، تصویر و طراحی",           icon: "🎨" },
  { id: "automation",     name: "Automation & Agents",      nameFa: "اتوماسیون و ایجنت‌ها",          icon: "⚙️" },
  { id: "audio",          name: "Audio, Speech & Music",    nameFa: "صدا، گفتار و موسیقی",          icon: "🎵" },
  { id: "business",       name: "Business & Marketing",     nameFa: "کسب‌وکار، بازاریابی و فروش",    icon: "💼" },
  { id: "data-research",  name: "Data & Research",          nameFa: "داده، تحلیل و پژوهش",          icon: "🔬" },
  { id: "code",           name: "Code & Development",       nameFa: "برنامه‌نویسی و توسعه",          icon: "💻" },
  { id: "entertainment",  name: "Entertainment & Lifestyle",nameFa: "سرگرمی و سبک زندگی",           icon: "🎮" },
  { id: "industry",       name: "Industry & Specialized",   nameFa: "صنایع و حوزه‌های تخصصی",        icon: "🏭" },
  { id: "featured",       name: "Featured & Emerging",      nameFa: "منتخب، نوظهور و آینده",        icon: "⭐" },
];

// ─────────────────────────────── Categories ─────────────────────────────

export const AI_CATEGORIES: AICategory[] = [
  // ── 🤖 Assistants ──────────────────────────────────────────────────────
  { id: "ai-chat",          groupId: "assistants",    name: "AI Chat & Assistant",   nameFa: "چت و دستیار هوشمند",        icon: "💬", sourceUrl: "https://www.aixploria.com/en/category/ai-chat-assistant/" },
  { id: "chatbots",         groupId: "assistants",    name: "ChatBots",              nameFa: "چت‌بات‌ها",                  icon: "🤖", sourceUrl: "https://www.aixploria.com/en/category/chatbot-ai/" },
  { id: "ai-characters",    groupId: "assistants",    name: "AI Characters",         nameFa: "شخصیت‌های مجازی",            icon: "🎭", sourceUrl: "https://www.aixploria.com/en/category/best-ai-characters-chatbots-lists/" },
  { id: "life-assistants",  groupId: "assistants",    name: "Life Assistants",       nameFa: "دستیار زندگی روزمره",        icon: "🧭", sourceUrl: "https://www.aixploria.com/en/category/life-assistants/" },
  { id: "legal-assistants", groupId: "assistants",    name: "Legal Assistants",      nameFa: "دستیار حقوقی",               icon: "⚖️", sourceUrl: "https://www.aixploria.com/en/category/legal-assistants/" },
  { id: "llm-models",       groupId: "assistants",    name: "LLM Models",            nameFa: "مدل‌های زبانی بزرگ (LLM)",   icon: "🧠", sourceUrl: "https://www.aixploria.com/en/category/llm-model-ai-en/" },

  // ── 🎨 Art & Design ────────────────────────────────────────────────────
  { id: "image-generators", groupId: "art",           name: "Image Generators",      nameFa: "تولید تصویر",               icon: "🖼️", sourceUrl: "https://www.aixploria.com/en/category/image-ai-en/" },
  { id: "image-editing",    groupId: "art",           name: "Image Editing",         nameFa: "ویرایش تصویر",              icon: "🪄", sourceUrl: "https://www.aixploria.com/en/category/image-editing/" },
  { id: "art",              groupId: "art",           name: "Art",                   nameFa: "هنر دیجیتال",               icon: "🎨", sourceUrl: "https://www.aixploria.com/en/category/art-en/" },
  { id: "logo-creation",    groupId: "art",           name: "Logo Creation",         nameFa: "طراحی لوگو",                icon: "🔷", sourceUrl: "https://www.aixploria.com/en/category/best-ai-logo-generators/" },
  { id: "3d-model",         groupId: "art",           name: "3D Model",              nameFa: "مدل‌سازی سه‌بعدی",           icon: "🧊", sourceUrl: "https://www.aixploria.com/en/category/3d-model/" },

  // ── 🎬 Video ───────────────────────────────────────────────────────────
  { id: "video-generators", groupId: "video",         name: "Video Generators",      nameFa: "تولید ویدیو",               icon: "🎬", sourceUrl: "https://www.aixploria.com/en/category/video-generators/" },
  { id: "text-to-video",    groupId: "video",         name: "Text-to-Video",         nameFa: "تبدیل متن به ویدیو",         icon: "📝", sourceUrl: "https://www.aixploria.com/en/category/text-to-video-en/" },
  { id: "video-editing",    groupId: "video",         name: "Video Editing",         nameFa: "ویرایش ویدیو",              icon: "✂️", sourceUrl: "https://www.aixploria.com/en/category/video-edition/" },
  { id: "avatars",          groupId: "video",         name: "Avatars",               nameFa: "آواتارهای دیجیتال",          icon: "🧑‍💼", sourceUrl: "https://www.aixploria.com/en/category/avatars-en/" },

  // ── 🎵 Audio & Music ───────────────────────────────────────────────────
  { id: "music",            groupId: "audio",         name: "Music",                 nameFa: "ساخت موسیقی",               icon: "🎹", sourceUrl: "https://www.aixploria.com/en/category/music/" },
  { id: "audio-editing",    groupId: "audio",         name: "Audio Editing",         nameFa: "ویرایش صدا",                icon: "🎚️", sourceUrl: "https://www.aixploria.com/en/category/audio-editing/" },
  { id: "text-to-speech",   groupId: "audio",         name: "Text To Speech",        nameFa: "تبدیل متن به گفتار",         icon: "🔊", sourceUrl: "https://www.aixploria.com/en/category/voice-reading/" },
  { id: "voice-cloning",    groupId: "audio",         name: "Voice Cloning",         nameFa: "کلون و کپی صدا",             icon: "🎙️", sourceUrl: "https://www.aixploria.com/en/category/ai-voice-cloning/" },
  { id: "transcriber",      groupId: "audio",         name: "Transcriber",           nameFa: "رونویسی (گفتار به متن)",    icon: "📄", sourceUrl: "https://www.aixploria.com/en/category/transcriber/" },

  // ── ✍️ Writing ─────────────────────────────────────────────────────────
  { id: "writing-seo",      groupId: "writing",       name: "Writing & Web SEO",     nameFa: "نویسندگی و سئو محتوا",       icon: "✍️", sourceUrl: "https://www.aixploria.com/en/category/writing-web-seo/" },
  { id: "text-generators",  groupId: "writing",       name: "Text Generators",       nameFa: "تولید متن",                  icon: "📃", sourceUrl: "https://www.aixploria.com/en/category/ai-text-generators/" },
  { id: "storytelling",     groupId: "writing",       name: "Storytelling",          nameFa: "داستان‌نویسی و قصه‌گویی",    icon: "📖", sourceUrl: "https://www.aixploria.com/en/category/storytelling-generator/" },
  { id: "prompts",          groupId: "writing",       name: "Prompts & Aids",        nameFa: "پرامپت و ابزار کمکی",        icon: "💡", sourceUrl: "https://www.aixploria.com/en/category/prompts-help/" },
  { id: "summarizer",       groupId: "writing",       name: "Summarizer",            nameFa: "خلاصه‌ساز متن",              icon: "🗜️", sourceUrl: "https://www.aixploria.com/en/category/ai-summarizer/" },
  { id: "translation",      groupId: "writing",       name: "Translation",           nameFa: "ترجمه",                      icon: "🌍", sourceUrl: "https://www.aixploria.com/en/category/translation-ai/" },

  // ── 📈 Productivity ────────────────────────────────────────────────────
  { id: "email",            groupId: "productivity",  name: "E-mail",                nameFa: "ایمیل و خبرنامه",            icon: "📧", sourceUrl: "https://www.aixploria.com/en/category/e-mail-en/" },
  { id: "education",        groupId: "productivity",  name: "Education / Studies",   nameFa: "آموزش و تحصیل",              icon: "🎓", sourceUrl: "https://www.aixploria.com/en/category/education-en/" },
  { id: "memory",           groupId: "productivity",  name: "Memory & Notes",        nameFa: "حافظه و یادداشت‌برداری",     icon: "🧠", sourceUrl: "https://www.aixploria.com/en/category/memory-en/" },
  { id: "search-engine",    groupId: "productivity",  name: "Search Engine",         nameFa: "موتور جستجوی هوشمند",        icon: "🔎", sourceUrl: "https://www.aixploria.com/en/category/search-engine/" },
  { id: "presentation",     groupId: "productivity",  name: "Presentation",          nameFa: "ساخت ارائه و اسلاید",        icon: "📽️", sourceUrl: "https://www.aixploria.com/en/category/presentation-en/" },
  { id: "productivity",     groupId: "productivity",  name: "Productivity",          nameFa: "بهره‌وری",                  icon: "✅", sourceUrl: "https://www.aixploria.com/en/category/productivity-en/" },
  { id: "files-spreadsheets", groupId: "productivity",name: "Files & Spreadsheets",  nameFa: "فایل، PDF و صفحه‌گسترده",    icon: "📊", sourceUrl: "https://www.aixploria.com/en/category/files-spreadsheets/" },
  { id: "chatgpt-extensions", groupId: "productivity",name: "ChatGPT Extensions",    nameFa: "افزونه‌های هوش مصنوعی",      icon: "🧩", sourceUrl: "https://www.aixploria.com/en/category/extensions-chatgpt/" },

  // ── 💼 Business ────────────────────────────────────────────────────────
  { id: "business",         groupId: "business",      name: "Business",              nameFa: "مدیریت کسب‌وکار",            icon: "🏢", sourceUrl: "https://www.aixploria.com/en/category/business-study/" },
  { id: "ecommerce",        groupId: "business",      name: "E-commerce",            nameFa: "تجارت الکترونیک",            icon: "🛒", sourceUrl: "https://www.aixploria.com/en/category/e-commerce-en/" },
  { id: "finance",          groupId: "business",      name: "Finance",               nameFa: "مالی و سرمایه‌گذاری",        icon: "💰", sourceUrl: "https://www.aixploria.com/en/category/finance-en/" },
  { id: "marketing",        groupId: "business",      name: "Marketing",             nameFa: "بازاریابی دیجیتال",          icon: "📣", sourceUrl: "https://www.aixploria.com/en/category/marketing-ai/" },
  { id: "social-networks",  groupId: "business",      name: "Social Networks",       nameFa: "شبکه‌های اجتماعی",          icon: "📱", sourceUrl: "https://www.aixploria.com/en/category/social-assistants-en/" },
  { id: "human-resources",  groupId: "business",      name: "Human Resources",       nameFa: "منابع انسانی و استخدام",     icon: "👥", sourceUrl: "https://www.aixploria.com/en/category/human-resources-ai/" },
  { id: "seo",              groupId: "business",      name: "SEO",                   nameFa: "سئو (بهینه‌سازی سایت)",      icon: "🔗", sourceUrl: "https://www.aixploria.com/en/category/seo-ai-tools/" },
  { id: "customer-support", groupId: "business",      name: "Customer Support",      nameFa: "پشتیبانی مشتری",             icon: "🎧", sourceUrl: "https://www.aixploria.com/en/category/customer-support/" },
  { id: "sales",            groupId: "business",      name: "Sales & Conversion",    nameFa: "فروش و افزایش نرخ تبدیل",    icon: "📈", sourceUrl: "https://www.aixploria.com/en/category/sales-conversion-leads/" },

  // ── 🔬 Data & Research ─────────────────────────────────────────────────
  { id: "data-analytics",   groupId: "data-research", name: "Data & Analytics",      nameFa: "داده و تحلیل",               icon: "📊", sourceUrl: "https://www.aixploria.com/en/category/data-analytics-ai/" },
  { id: "ai-detection",     groupId: "data-research", name: "AI Detection",          nameFa: "تشخیص محتوای هوش مصنوعی",    icon: "🕵️", sourceUrl: "https://www.aixploria.com/en/category/ai-detection-en/" },
  { id: "research-science", groupId: "data-research", name: "Research & Science",    nameFa: "پژوهش و علم",                icon: "🔬", sourceUrl: "https://www.aixploria.com/en/category/research-science-en/" },

  // ── 💻 Code & Dev ──────────────────────────────────────────────────────
  { id: "code-assistant",   groupId: "code",          name: "Code Assistant",        nameFa: "دستیار کدنویسی",             icon: "👨‍💻", sourceUrl: "https://www.aixploria.com/en/category/assistant-code-en/" },
  { id: "no-code",          groupId: "code",          name: "No Code / Low Code",    nameFa: "بدون کد / کم‌کد",            icon: "🧱", sourceUrl: "https://www.aixploria.com/en/category/no-code-en/" },
  { id: "developer-tools",  groupId: "code",          name: "Developer Tools",       nameFa: "ابزار توسعه‌دهندگان",        icon: "🛠️", sourceUrl: "https://www.aixploria.com/en/category/developer-tools/" },
  { id: "github-projects",  groupId: "code",          name: "GitHub Projects",       nameFa: "پروژه‌های متن‌باز GitHub",   icon: "🐙", sourceUrl: "https://www.aixploria.com/en/category/github-project-ai/" },
  { id: "websites-design",  groupId: "code",          name: "Websites & Design",     nameFa: "طراحی و ساخت وب‌سایت",       icon: "🌐", sourceUrl: "https://www.aixploria.com/en/category/websites-ai/" },

  // ── 🎮 Entertainment ───────────────────────────────────────────────────
  { id: "face-swap",        groupId: "entertainment", name: "Face Swap & DeepFake",  nameFa: "تعویض چهره و دیپ‌فیک",       icon: "🎭", sourceUrl: "https://www.aixploria.com/en/category/face-swap-deepfake-en/" },
  { id: "fashion",          groupId: "entertainment", name: "Fashion",               nameFa: "مد و فشن",                   icon: "👗", sourceUrl: "https://www.aixploria.com/en/category/fashion-en/" },
  { id: "amazing",          groupId: "entertainment", name: "Amazing & Fun",         nameFa: "جالب و شگفت‌انگیز",          icon: "🤯", sourceUrl: "https://www.aixploria.com/en/category/amazing/" },
  { id: "games",            groupId: "entertainment", name: "Games",                 nameFa: "بازی و سرگرمی",              icon: "🎮", sourceUrl: "https://www.aixploria.com/en/category/games-en/" },
  { id: "dating",           groupId: "entertainment", name: "Dating & Relationships",nameFa: "دوست‌یابی و روابط",          icon: "💘", sourceUrl: "https://www.aixploria.com/en/category/dating-relationships-ai/" },
  { id: "travel",           groupId: "entertainment", name: "Travel",                nameFa: "سفر و گردشگری",              icon: "✈️", sourceUrl: "https://www.aixploria.com/en/category/travel/" },

  // ── 🏭 Industry ────────────────────────────────────────────────────────
  { id: "real-estate",      groupId: "industry",      name: "Real Estate / Architecture", nameFa: "املاک، معماری و دکوراسیون", icon: "🏠", sourceUrl: "https://www.aixploria.com/en/category/real-estate/" },
  { id: "robots-devices",   groupId: "industry",      name: "Robots & Devices",      nameFa: "ربات‌ها و دستگاه‌های هوشمند", icon: "🦾", sourceUrl: "https://www.aixploria.com/en/category/robots-devices-ai/" },
  { id: "healthcare",       groupId: "industry",      name: "Healthcare",            nameFa: "سلامت و پزشکی",              icon: "🩺", sourceUrl: "https://www.aixploria.com/en/category/healthcare/" },
  { id: "assistive-tech",   groupId: "industry",      name: "Assistive Technology",  nameFa: "فناوری‌های یاری‌رسان",        icon: "♿", sourceUrl: "https://www.aixploria.com/en/category/ai-assistive-technology-at/" },

  // ── ⭐ Featured ────────────────────────────────────────────────────────
  { id: "ai-simulation",    groupId: "featured",      name: "AI Simulation",         nameFa: "شبیه‌سازی و دنیای مجازی",     icon: "🌐", sourceUrl: "https://www.aixploria.com/en/category/ai-simulation-3d-world/" },
  { id: "future-tools",     groupId: "featured",      name: "Future Tools",          nameFa: "ابزارهای آینده",             icon: "🚀", sourceUrl: "https://www.aixploria.com/en/category/future-tools-ai/" },
  { id: "supertools",       groupId: "featured",      name: "SuperTools",            nameFa: "ابرابزارها (چندمنظوره)",      icon: "🦸", sourceUrl: "https://www.aixploria.com/en/category/ai-supertools/" },
  { id: "ai-agents",        groupId: "automation",    name: "AI Agents",             nameFa: "ایجنت‌های هوش مصنوعی",        icon: "🕹️", sourceUrl: "https://www.aixploria.com/en/category/best-ai-agents/" },
  { id: "automation",       groupId: "automation",    name: "Automation",            nameFa: "اتوماسیون گردش‌کار",          icon: "🔁", sourceUrl: "https://www.aixploria.com/en/category/automation-ai-workflows/" },

  // ── 🆕 دسته‌های تخصصی جدید (۲۷ مورد) ──────────────────────────────────
  { id: "ai-coaching",      groupId: "assistants",    name: "AI Coaching",           nameFa: "مربی و کوچ هوشمند",           icon: "🏆", sourceUrl: "https://www.aixploria.com/en/category/ai-coaching/" },
  { id: "photo",            groupId: "art",           name: "Photography",           nameFa: "عکاسی",                       icon: "📷", sourceUrl: "https://www.aixploria.com/en/category/photo-ai-en/" },
  { id: "copywriting",      groupId: "writing",       name: "Copywriting",           nameFa: "کپی‌رایتینگ",                 icon: "📣", sourceUrl: "https://www.aixploria.com/en/category/copywriting-ai/" },
  { id: "paraphraser",      groupId: "writing",       name: "Paraphraser",           nameFa: "بازنویسی متن",                icon: "✍️", sourceUrl: "https://www.aixploria.com/en/category/paraphraser-en/" },
  { id: "copy-editors",     groupId: "writing",       name: "Copy Editors",          nameFa: "ویرایشگر متن و گرامر",        icon: "✅", sourceUrl: "https://www.aixploria.com/en/category/copy-editors/" },
  { id: "content-creation", groupId: "writing",       name: "Content Creation",      nameFa: "تولید محتوا",                 icon: "📝", sourceUrl: "https://www.aixploria.com/en/category/content-creation/" },
  { id: "thesis",           groupId: "writing",       name: "Thesis & Academic",     nameFa: "پایان‌نامه و مقاله دانشگاهی", icon: "🎓", sourceUrl: "https://www.aixploria.com/en/category/thesis-ai/" },
  { id: "describer",        groupId: "writing",       name: "Describer",             nameFa: "توصیف‌گر (تصویر به متن)",     icon: "🔍", sourceUrl: "https://www.aixploria.com/en/category/describer/" },
  { id: "prompt-market",    groupId: "writing",       name: "Prompt Marketplace",    nameFa: "بازار پرامپت",                icon: "🛒", sourceUrl: "https://www.aixploria.com/en/category/prompt-market/" },
  { id: "plagiarism",       groupId: "writing",       name: "Plagiarism Detection",  nameFa: "تشخیص سرقت ادبی",            icon: "📄", sourceUrl: "https://www.aixploria.com/en/category/plagiarism-detection/" },
  { id: "notebook",         groupId: "productivity",  name: "Notebook & Notes",      nameFa: "دفترچه و یادداشت",            icon: "📓", sourceUrl: "https://www.aixploria.com/en/category/notebook-ai/" },
  { id: "qr-codes",         groupId: "productivity",  name: "QR Codes",              nameFa: "کدهای QR",                    icon: "🔗", sourceUrl: "https://www.aixploria.com/en/category/qr-code-en/" },
  { id: "meetings",         groupId: "productivity",  name: "Meetings",              nameFa: "جلسات و یادداشت جلسه",        icon: "👥", sourceUrl: "https://www.aixploria.com/en/category/meeting-notes/" },
  { id: "file-conversion",  groupId: "productivity",  name: "File Conversion",       nameFa: "تبدیل فایل",                  icon: "🔄", sourceUrl: "https://www.aixploria.com/en/category/convert-file-en/" },
  { id: "voice-assistant",  groupId: "audio",         name: "Voice Assistant",       nameFa: "دستیار صوتی",                 icon: "🎙️", sourceUrl: "https://www.aixploria.com/en/category/voice-assistant/" },
  { id: "voice-changer",    groupId: "audio",         name: "Voice Changer",         nameFa: "تغییر صدا",                   icon: "🎤", sourceUrl: "https://www.aixploria.com/en/category/voice-changer/" },
  { id: "sound",            groupId: "audio",         name: "Sound Effects",         nameFa: "افکت‌های صوتی",               icon: "🔊", sourceUrl: "https://www.aixploria.com/en/category/sound-effects/" },
  { id: "odd-sfx",          groupId: "audio",         name: "Odd & Fun Sounds",      nameFa: "صداها و ابزارهای عجیب",       icon: "🧪", sourceUrl: "https://www.aixploria.com/en/category/audio-sfx/" },
  { id: "telephone-calls",  groupId: "business",      name: "Phone Calls",           nameFa: "تماس‌های تلفنی",              icon: "📞", sourceUrl: "https://www.aixploria.com/en/category/phone-calls/" },
  { id: "copilot",          groupId: "code",          name: "AI Copilots",           nameFa: "کوپایلوت‌های برنامه‌نویسی",   icon: "✈️", sourceUrl: "https://www.aixploria.com/en/category/ai-copilot/" },
  { id: "databases",        groupId: "code",          name: "Databases",             nameFa: "پایگاه داده",                 icon: "🗄️", sourceUrl: "https://www.aixploria.com/en/category/databases/" },
  { id: "cybersecurity",    groupId: "industry",      name: "Cybersecurity",         nameFa: "امنیت سایبری",                icon: "🛡️", sourceUrl: "https://www.aixploria.com/en/category/cybersecurity/" },
  { id: "traffic",          groupId: "industry",      name: "Traffic & Transport",   nameFa: "ترافیک و حمل‌ونقل",           icon: "🚦", sourceUrl: "https://www.aixploria.com/en/category/traffic-ai/" },
  { id: "smart-homes",      groupId: "industry",      name: "Smart Homes",           nameFa: "خانه هوشمند",                 icon: "🏡", sourceUrl: "https://www.aixploria.com/en/category/smart-homes/" },
  { id: "nutrition",        groupId: "entertainment", name: "Nutrition",             nameFa: "تغذیه و سلامت",               icon: "🍏", sourceUrl: "https://www.aixploria.com/en/category/nutrition-ai/" },
  { id: "diy-makers",       groupId: "entertainment", name: "DIY & Makers",          nameFa: "سازندگان و DIY",              icon: "🔨", sourceUrl: "https://www.aixploria.com/en/category/diy-makers/" },
  { id: "for-adults",       groupId: "entertainment", name: "For Adults (18+)",      nameFa: "مخصوص بزرگسالان (۱۸+)",       icon: "🔞", sourceUrl: "https://www.aixploria.com/en/category/for-adults/" },
];
