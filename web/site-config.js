/**
 * Robotic Agent — Shared Site CMS & Live Configuration Engine (site-config.js)
 * Powers real-time synchronization between:
 *  - web/admin.html  (Complete Site Administration & Visual CMS Control Center)
 *  - web/index.html  (Main Bilingual Landing Page & ROI Calculator)
 *  - web/portal.html (User Dashboard, 46 Subscription Plans & 6 Payment Gateways)
 */
(function (global) {
  const STORAGE_KEY = 'ROBOTIC_AGENT_SITE_CMS_CONFIG_V2';

  const DEFAULT_GATEWAYS = [
    { id: 'ZarinPal', enabled: true, sandbox: false, region: '🇮🇷 درگاه ایرانی', name: 'زرین‌پال (ZarinPal)', currency: 'IRT', merchantId: 'ZP-MERCHANT-88421-IR', endpoint: 'https://api.zarinpal.com/pg/v4/payment/request.json', redirect: 'https://www.zarinpal.com/pg/StartPay/' },
    { id: 'Zibal', enabled: true, sandbox: false, region: '🇮🇷 درگاه ایرانی', name: 'زیبال (Zibal IPG)', currency: 'IRT', merchantId: 'ZIBAL-LIVE-99201', endpoint: 'https://gateway.zibal.ir/v1/request', redirect: 'https://gateway.zibal.ir/start/' },
    { id: 'IdPay', enabled: true, sandbox: false, region: '🇮🇷 درگاه ایرانی', name: 'آیدی‌پی (IDPay)', currency: 'IRT', merchantId: 'IDPAY-KEY-77410-IR', endpoint: 'https://api.idpay.ir/v1.1/payment', redirect: 'https://idpay.ir/p/ws/' },
    { id: 'Stripe', enabled: true, sandbox: false, region: '🌍 درگاه خارجی', name: 'استرایپ (Stripe Global)', currency: 'USD', merchantId: 'pk_live_51RoboticAgentStripe2026', endpoint: 'https://api.stripe.com/v1/checkout/sessions', redirect: 'https://checkout.stripe.com/c/pay/' },
    { id: 'LemonSqueezy', enabled: true, sandbox: false, region: '🌍 درگاه خارجی', name: 'لمون‌اسکوییزی (LemonSqueezy)', currency: 'USD', merchantId: 'LS-STORE-44810-GLOBAL', endpoint: 'https://api.lemonsqueezy.com/v1/checkouts', redirect: 'https://roboticagent.lemonsqueezy.com/checkout/buy/' },
    { id: 'NowPaymentsCrypto', enabled: true, sandbox: false, region: '🌍 رمزارز جهانی', name: 'نووپیمنتس (USDT / BTC)', currency: 'USD', merchantId: 'NP-CRYPTO-USDT-TRC20', endpoint: 'https://api.nowpayments.io/v1/invoice', redirect: 'https://nowpayments.io/payment/?iid=' }
  ];

  const DEFAULT_PLANS_46 = [
    { n: 1, sku: 'RA-PLAN-01-FREE', enabled: true, cat: 'PERSONAL', fa: '۱. اشتراک پایه رایگان (Free Starter)', en: 'Free Starter', cycle: 'رایگان همیشگی', toman: 0, usd: 0, batch: 50 },
    { n: 2, sku: 'RA-PLAN-02-WEEKLY-PASS', enabled: true, cat: 'PERSONAL', fa: '۲. اشتراک هفتگی سریع (7-Day Express Pass)', en: '7-Day Express Pass', cycle: 'هفتگی', toman: 95000, usd: 2.50, batch: 2000 },
    { n: 3, sku: 'RA-PLAN-03-STUDENT-MONTHLY', enabled: true, cat: 'PERSONAL', fa: '۳. اشتراک دانشجویی و پژوهشی (Student & Academic)', en: 'Student & Academic Monthly', cycle: 'ماهانه', toman: 145000, usd: 3.90, batch: 5000 },
    { n: 4, sku: 'RA-PLAN-04-PRO-MONTHLY', enabled: true, cat: 'PERSONAL', fa: '۴. اشتراک حرفه‌ای ماهانه (Pro Monthly)', en: 'Pro Monthly', cycle: 'ماهانه', toman: 290000, usd: 9.00, batch: 100000 },
    { n: 5, sku: 'RA-PLAN-05-PRO-ANNUAL', enabled: true, cat: 'PERSONAL', fa: '۵. اشتراک حرفه‌ای سالانه (Pro Annual - ۳۲٪ تخفیف)', en: 'Pro Annual', cycle: 'سالانه', toman: 2350000, usd: 74.00, batch: 100000 },
    { n: 6, sku: 'RA-PLAN-06-PRO-LIFETIME', enabled: true, cat: 'PERSONAL', fa: '۶. لایسنس مادام‌العمر حرفه‌ای (Pro Lifetime Deal)', en: 'Pro Lifetime Deal', cycle: 'مادام‌العمر', toman: 2850000, usd: 79.00, batch: 100000 },
    { n: 7, sku: 'RA-PLAN-07-PRIVACY-SHIELD', enabled: true, cat: 'SKILLS', fa: '۷. بستهٔ سپر حریم خصوصی و ضد نشت داده (Privacy Shield)', en: 'Privacy & DLP Shield', cycle: 'ماهانه', toman: 165000, usd: 4.50, batch: 10000 },
    { n: 8, sku: 'RA-PLAN-08-PDF-POWERHOUSE', enabled: true, cat: 'SKILLS', fa: '۸. بستهٔ استودیو پیشرفته PDF و اسناد (PDF Powerhouse)', en: 'PDF & Document Powerhouse', cycle: 'ماهانه', toman: 155000, usd: 4.20, batch: 10000 },
    { n: 9, sku: 'RA-PLAN-09-EXCEL-RECONCILE', enabled: true, cat: 'SKILLS', fa: '۹. بستهٔ مغایرت‌گیری بانکی و اتوماسیون اکسل', en: 'Financial & Excel Reconciliation', cycle: 'ماهانه', toman: 220000, usd: 6.50, batch: 25000 },
    { n: 10, sku: 'RA-PLAN-10-SUBTITLE-STUDIO', enabled: true, cat: 'SKILLS', fa: '۱۰. بستهٔ استودیو زیرنویس و رسانه (Subtitle Studio)', en: 'Subtitle & Media Studio', cycle: 'ماهانه', toman: 140000, usd: 3.90, batch: 5000 },
    { n: 11, sku: 'RA-PLAN-11-MEETING-SECRETARY', enabled: true, cat: 'SKILLS', fa: '۱۱. بستهٔ دستیار جلسات و صورت‌جلسهٔ هوشمند', en: 'Smart Meeting & Minutes Pack', cycle: 'ماهانه', toman: 150000, usd: 4.20, batch: 5000 },
    { n: 12, sku: 'RA-PLAN-12-CONTRACT-NDA-BUILDER', enabled: true, cat: 'SKILLS', fa: '۱۲. بستهٔ قراردادساز، NDA و پیش‌فاکتور دوزبانه', en: 'Bilingual Contract & NDA Builder', cycle: 'ماهانه', toman: 195000, usd: 5.50, batch: 5000 },
    { n: 13, sku: 'RA-PLAN-13-DESKTOP-ORGANIZER', enabled: true, cat: 'SKILLS', fa: '۱۳. بستهٔ مرتب‌سازی خودکار و پاک‌سازی تکراری‌ها', en: 'Smart Organizer & Dedup Pack', cycle: 'ماهانه', toman: 125000, usd: 3.50, batch: 50000 },
    { n: 14, sku: 'RA-PLAN-14-DEV-DEVOPS-KIT', enabled: true, cat: 'SKILLS', fa: '۱۴. بستهٔ ابزارهای برنامه‌نویسان (JSON/YAML/Hash/Env)', en: 'Developer & DevOps Local Kit', cycle: 'ماهانه', toman: 175000, usd: 4.90, batch: 25000 },
    { n: 15, sku: 'RA-PLAN-15-MCP-SERVER-PRO', enabled: true, cat: 'SKILLS', fa: '۱۵. بستهٔ سرور محلی MCP برای Claude و Cursor', en: 'MCP Local Server Connector Pro', cycle: 'ماهانه', toman: 210000, usd: 5.90, batch: 50000 },
    { n: 16, sku: 'RA-PLAN-16-BROWSER-EXT-PRO', enabled: true, cat: 'SKILLS', fa: '۱۶. بستهٔ افزونهٔ مرورگر کروم و اج (Browser Companion Pro)', en: 'Chrome & Edge Extension Pro', cycle: 'ماهانه', toman: 135000, usd: 3.80, batch: 10000 },
    { n: 17, sku: 'RA-PLAN-17-JOB-ACCOUNTING', enabled: true, cat: 'PLAYBOOKS', fa: '۱۷. اشتراک ویژهٔ حسابداران و مدیران مالی', en: 'Accountant & Tax Edition', cycle: 'ماهانه', toman: 260000, usd: 7.50, batch: 30000 },
    { n: 18, sku: 'RA-PLAN-18-JOB-LEGAL', enabled: true, cat: 'PLAYBOOKS', fa: '۱۸. اشتراک ویژهٔ وکلا و مشاوران حقوقی', en: 'Legal & Law Firm Edition', cycle: 'ماهانه', toman: 280000, usd: 8.50, batch: 30000 },
    { n: 19, sku: 'RA-PLAN-19-JOB-HR', enabled: true, cat: 'PLAYBOOKS', fa: '۱۹. اشتراک ویژهٔ منابع انسانی و کارگزینی (HR)', en: 'HR & Recruitment Edition', cycle: 'ماهانه', toman: 240000, usd: 6.90, batch: 30000 },
    { n: 20, sku: 'RA-PLAN-20-JOB-ECOMMERCE', enabled: true, cat: 'PLAYBOOKS', fa: '۲۰. اشتراک ویژهٔ مدیران فروشگاه اینترنتی', en: 'E-Commerce Store Edition', cycle: 'ماهانه', toman: 250000, usd: 7.20, batch: 50000 },
    { n: 21, sku: 'RA-PLAN-21-JOB-CREATOR', enabled: true, cat: 'PLAYBOOKS', fa: '۲۱. اشتراک ویژهٔ یوتیوبرها و تولیدکنندگان محتوا', en: 'YouTuber & Creator Edition', cycle: 'ماهانه', toman: 210000, usd: 5.90, batch: 20000 },
    { n: 22, sku: 'RA-PLAN-22-JOB-RESEARCHER', enabled: true, cat: 'PLAYBOOKS', fa: '۲۲. اشتراک ویژهٔ اساتید دانشگاه و پژوهشگران', en: 'Academic Researcher Edition', cycle: 'ماهانه', toman: 190000, usd: 5.20, batch: 20000 },
    { n: 23, sku: 'RA-PLAN-23-JOB-TRANSLATOR', enabled: true, cat: 'PLAYBOOKS', fa: '۲۳. اشتراک ویژهٔ مترجمان رسمی و دارالترجمه‌ها', en: 'Official Translator Edition', cycle: 'ماهانه', toman: 220000, usd: 6.20, batch: 25000 },
    { n: 24, sku: 'RA-PLAN-24-JOB-MEDICAL', enabled: true, cat: 'PLAYBOOKS', fa: '۲۴. اشتراک ویژهٔ مطب‌ها و کلینیک‌های پزشکی', en: 'Clinic & Healthcare Edition', cycle: 'ماهانه', toman: 270000, usd: 7.90, batch: 30000 },
    { n: 25, sku: 'RA-PLAN-25-JOB-REALESTATE', enabled: true, cat: 'PLAYBOOKS', fa: '۲۵. اشتراک ویژهٔ مشاوران املاک و مستغلات', en: 'Real Estate Agency Edition', cycle: 'ماهانه', toman: 230000, usd: 6.50, batch: 25000 },
    { n: 26, sku: 'RA-PLAN-26-JOB-SUPPORT', enabled: true, cat: 'PLAYBOOKS', fa: '۲۶. اشتراک ویژهٔ تیم‌های پشتیبانی مشتریان (CRM)', en: 'Customer Support Edition', cycle: 'ماهانه', toman: 240000, usd: 6.90, batch: 35000 },
    { n: 27, sku: 'RA-PLAN-27-JOB-SOFTWARE-ENG', enabled: true, cat: 'PLAYBOOKS', fa: '۲۷. اشتراک ویژهٔ مهندسان نرم‌افزار و DevOps', en: 'Software Engineer Edition', cycle: 'ماهانه', toman: 260000, usd: 7.50, batch: 50000 },
    { n: 28, sku: 'RA-PLAN-28-JOB-PHOTOGRAPHER', enabled: true, cat: 'PLAYBOOKS', fa: '۲۸. اشتراک ویژهٔ عکاسان، آتلیه‌ها و طراحان گرافیک', en: 'Photographer & Studio Edition', cycle: 'ماهانه', toman: 225000, usd: 6.40, batch: 50000 },
    { n: 29, sku: 'RA-PLAN-29-JOB-LOGISTICS', enabled: true, cat: 'PLAYBOOKS', fa: '۲۹. اشتراک ویژهٔ شرکت‌های لجستیک، انبارداری و گمرک', en: 'Logistics & Warehouse Edition', cycle: 'ماهانه', toman: 255000, usd: 7.40, batch: 40000 },
    { n: 30, sku: 'RA-PLAN-30-JOB-TEACHER', enabled: true, cat: 'PLAYBOOKS', fa: '۳۰. اشتراک ویژهٔ معلمان، مدارس و آموزشگاه‌ها', en: 'School & Educator Edition', cycle: 'ماهانه', toman: 175000, usd: 4.90, batch: 20000 },
    { n: 31, sku: 'RA-PLAN-31-JOB-SOCIAL-ADMIN', enabled: true, cat: 'PLAYBOOKS', fa: '۳۱. اشتراک ویژهٔ ادمین‌های اینستاگرام و تلگرام', en: 'Social Media Admin Edition', cycle: 'ماهانه', toman: 195000, usd: 5.50, batch: 25000 },
    { n: 32, sku: 'RA-PLAN-32-JOB-PROCUREMENT', enabled: true, cat: 'PLAYBOOKS', fa: '۳۲. اشتراک ویژهٔ واحد تدارکات و خرید شرکت‌ها', en: 'Procurement & Vendor Edition', cycle: 'ماهانه', toman: 245000, usd: 7.00, batch: 30000 },
    { n: 33, sku: 'RA-PLAN-33-JOB-JOURNALIST', enabled: true, cat: 'PLAYBOOKS', fa: '۳۳. اشتراک ویژهٔ خبرنگاران و تحریریهٔ رسانه‌ها', en: 'Journalist & Newsroom Edition', cycle: 'ماهانه', toman: 215000, usd: 6.10, batch: 25000 },
    { n: 34, sku: 'RA-PLAN-34-JOB-FREELANCER', enabled: true, cat: 'PLAYBOOKS', fa: '۳۴. اشتراک ویژهٔ فریلنسرها و مشاوران مستقل', en: 'Freelancer & Consultant Edition', cycle: 'ماهانه', toman: 205000, usd: 5.80, batch: 25000 },
    { n: 35, sku: 'RA-PLAN-35-JOB-ARCHITECTURE', enabled: true, cat: 'PLAYBOOKS', fa: '۳۵. اشتراک ویژهٔ دفاتر معماری، عمران و مهندسی', en: 'Architecture & Civil Eng Edition', cycle: 'ماهانه', toman: 265000, usd: 7.80, batch: 40000 },
    { n: 36, sku: 'RA-PLAN-36-JOB-EVENT-PLANNER', enabled: true, cat: 'PLAYBOOKS', fa: '۳۶. اشتراک ویژهٔ برگزارکنندگان همایش و رویدادها', en: 'Event & Conference Edition', cycle: 'ماهانه', toman: 225000, usd: 6.40, batch: 30000 },
    { n: 37, sku: 'RA-PLAN-37-AI-CREDITS-100K', enabled: true, cat: 'CREDITS', fa: '۳۷. بستهٔ شارژ ۱۰۰ هزار توکن هوش مصنوعی ابری', en: '100K Hybrid Cloud AI Tokens', cycle: 'بستهٔ اعتباری', toman: 110000, usd: 2.99, batch: 10000 },
    { n: 38, sku: 'RA-PLAN-38-AI-CREDITS-500K', enabled: true, cat: 'CREDITS', fa: '۳۸. بستهٔ شارژ ۵۰۰ هزار توکن هوش مصنوعی ابری', en: '500K Hybrid Cloud AI Tokens', cycle: 'بستهٔ اعتباری', toman: 420000, usd: 11.99, batch: 50000 },
    { n: 39, sku: 'RA-PLAN-39-AI-CREDITS-2M', enabled: true, cat: 'CREDITS', fa: '۳۹. بستهٔ شارژ ۲ میلیون توکن هوش مصنوعی ابری', en: '2M Hybrid Cloud AI Tokens', cycle: 'بستهٔ اعتباری', toman: 1350000, usd: 39.00, batch: 100000 },
    { n: 40, sku: 'RA-PLAN-40-VOICE-TRANSCRIPTION-PACK', enabled: true, cat: 'CREDITS', fa: '۴۰. بستهٔ تبدیل صوت به متن و پیاده‌سازی جلسات (۵۰ ساعت)', en: '50-Hour Voice-to-Notes Pack', cycle: 'بستهٔ اعتباری', toman: 380000, usd: 10.90, batch: 20000 },
    { n: 41, sku: 'RA-PLAN-41-PROMPT-LIBRARY-VIP', enabled: true, cat: 'CREDITS', fa: '۴۱. بستهٔ VIP هزار پرامپت مهندسی‌شده و ۲۰۰ ابزار', en: 'VIP 1,000 Prompts & 200 AI Tools Pack', cycle: 'مادام‌العمر', toman: 490000, usd: 14.00, batch: 50000 },
    { n: 42, sku: 'RA-PLAN-42-TEAM-5-SEATS', enabled: true, cat: 'ENTERPRISE', fa: '۴۲. اشتراک تیمی استارتاپ (۵ کاربر هم‌زمان)', en: 'Startup Team 5-Seats', cycle: 'ماهانه', toman: 890000, usd: 29.00, batch: 500000 },
    { n: 43, sku: 'RA-PLAN-43-TEAM-20-SEATS', enabled: true, cat: 'ENTERPRISE', fa: '۴۳. اشتراک شرکتی (۲۰ کاربر هم‌زمان + Audit Log)', en: 'Company Team 20-Seats', cycle: 'ماهانه', toman: 2950000, usd: 89.00, batch: 1000000 },
    { n: 44, sku: 'RA-PLAN-44-ENTERPRISE-SITE-ANNUAL', enabled: true, cat: 'ENTERPRISE', fa: '۴۴. اشتراک سازمانی نامحدود سالانه (Enterprise Site)', en: 'Enterprise Site License Annual', cycle: 'سالانه', toman: 19500000, usd: 499.00, batch: 10000000 },
    { n: 45, sku: 'RA-PLAN-45-WHITE-LABEL-AGENCY', enabled: true, cat: 'ENTERPRISE', fa: '۴۵. لایسنس نمایندگی فروش با برند اختصاصی (White-Label)', en: 'White-Label Reseller Agency', cycle: 'سالانه', toman: 29000000, usd: 799.00, batch: 10000000 },
    { n: 46, sku: 'RA-PLAN-46-MARKETPLACE-PUBLISHER-PRO', enabled: true, cat: 'ENTERPRISE', fa: '۴۶. اکانت توسعه‌دهنده و فروشندهٔ پلاگین (سهم ۷۰٪)', en: 'Marketplace Publisher Pro Account', cycle: 'سالانه', toman: 980000, usd: 29.00, batch: 500000 }
  ];

  function createDefaultConfig() {
    return {
      branding: {
        brandName: '🤖 Robotic Agent v2.0',
        showTopBanner: true,
        topBannerTextFa: '🔥 جشنوارهٔ فروش ویژه: ۳۰٪ تخفیف روی تمامی ۴۶ مدل اشتراک با کد تخفیف NOROOZ30 — پرداخت آنی با زرین‌پال، زیبال، آیدی‌پی و درگاه‌های ارزی!',
        topBannerTextEn: '🔥 Special Offer: 30% OFF across all 46 Subscription Plans with code NOROOZ30 — Instant License Delivery!',
        heroBadgeFa: '۶ زبان زندهٔ دنیا | ۲۴ مهارت آفلاین | ۲۰ قالب شغلی | ۴۶ مدل اشتراک | ۶ درگاه پرداخت',
        heroBadgeEn: '6 Global Languages | 24 Offline Skills | 20 SMB Playbooks | 46 Plans | 6 Payment Gateways',
        heroTitleFa: 'کارهای تکراری فایل، PDF، اکسل و سانسور اطلاعات محرمانه را در چند ثانیه خودکار کنید',
        heroTitleEn: 'Automate Repetitive File, PDF, Spreadsheet & Privacy Tasks in Seconds — 100% Locally',
        heroSubtitleFa: 'بدون آپلود فایل‌های محرمانه در سایت‌های ناشناس! پیش‌نمایش اجباری قبل از اجرا، واگرد تک‌کلیکی با بررسی هش SHA-256، اسکنر ریسک حریم خصوصی و مربی پرامپت‌ساز هوشمند.',
        heroSubtitleEn: 'Never upload confidential documents to untrusted websites again. Mandatory dry-run preview, 1-click SHA-256 transactional Undo, Privacy Leak Risk Scanner, and Interactive Prompt Coach.',
        primaryCtaTextFa: '💳 ورود به پنل کاربری و خرید ۴۶ مدل اشتراک (۶ درگاه)',
        primaryCtaTextEn: '💳 Open User Portal & 46 Subscription Plans',
        primaryColor: '#2563eb',
        accentColor: '#10b981',
        bgColor: '#0b1120',
        footerTextFa: 'ساخته‌شده با معماری امنیت محلی (Local-First) — پشتیبانی از فارسی، انگلیسی، چینی، اسپانیایی، عربی و فرانسوی.',
        footerTextEn: 'Built with Local-First Security Architecture — Supports Persian, English, Chinese, Spanish, Arabic, and French.',
        seoTitle: 'Robotic Agent | دستیار هوشمند دسکتاپ، ۲۴ مهارت آفلاین و ۴۶ مدل اشتراک',
        seoDescription: 'خودکارسازی آفلاین فایل، PDF، اکسل، زیرنویس، صورت‌جلسه و سانسور اطلاعات محرمانه با ۶ درگاه پرداخت.',
        affiliatePartnerId: 'robotic_official',
        maintenanceMode: false
      },
      sections: {
        showHero: true,
        showFeatures: true,
        showRoiCalculator: true,
        showPricing: true,
        showFooter: true
      },
      features: [
        { icon: '🛡️', titleFa: 'اسکنر ریسک حریم خصوصی و سانسور PII', titleEn: 'Privacy Risk Scanner & PII Redaction', descFa: 'شناسایی و ماسک کردن خودکار شماره کارت (الگوریتم Luhn)، شبا، ایمیل، تلفن و مختصات GPS عکس‌ها در نسخهٔ _sanitized با حفظ ۱۰۰٪ فایل اصلی.', descEn: 'Automatically detects and masks credit cards, IBANs, emails, phones, and photo GPS/EXIF metadata into _sanitized copies.' },
        { icon: '💼', titleFa: '۲۰ قالب آمادهٔ شغلی و ۲۴ مهارت آفلاین', titleEn: '20 SMB Job Playbooks & 24 Offline Skills', descFa: 'بستهٔ حسابداری، وکالت، منابع انسانی، فروشگاه اینترنتی، تولید محتوا، استودیو زیرنویس، دستیار جلسات و مغایرت‌گیری بانکی.', descEn: 'Accounting, Legal, HR, E-Commerce, Creator, Subtitle Studio, Meeting Minutes, and Bank Reconciliation.' },
        { icon: '🔌', titleFa: 'سرور محلی MCP + هاب ۲۰۰ ابزار هوش مصنوعی', titleEn: 'Local MCP Server + 200 AI Tools Hub', descFa: 'اتصال ایمن Claude Desktop و Cursor به ۲۴ مهارت آفلاین با دروازهٔ پیش‌نمایش انسانی، به همراه کاتالوگ ۲۰۰ ابزار برتر AI.', descEn: 'Connect Claude Desktop & Cursor safely to 24 local skills with mandatory Human-in-the-Loop preview.' }
      ],
      coupons: [
        { code: 'NOROOZ30', percent: 30, active: true, desc: 'تخفیف ۳۰٪ جشنواره فروش ویژه' },
        { code: 'VIP20', percent: 20, active: true, desc: 'تخفیف ۲۰٪ مشتریان وفادار' },
        { code: 'STUDENT50', percent: 50, active: true, desc: 'تخفیف ۵۰٪ ویژهٔ دانشجویان و پژوهشگران' }
      ],
      orders: [
        { id: 'INV-2026-1041', email: 'ali.rezaei@company.ir', planSku: 'RA-PLAN-06-PRO-LIFETIME', gateway: 'زرین‌پال (ZarinPal)', amount: '۲٬۸۵۰٬۰۰۰ تومان', licenseKey: 'RA-LTD-PLAN06-88F2A1', date: '1405/07/19', status: 'تأییدشده (Paid)' },
        { id: 'INV-2026-1042', email: 'sara.legal@lawfirm.com', planSku: 'RA-PLAN-18-JOB-LEGAL', gateway: 'استرایپ (Stripe Global)', amount: '$8.50 USD', licenseKey: 'RA-PRO-PLAN18-99C4B2', date: '1405/07/19', status: 'تأییدشده (Paid)' },
        { id: 'INV-2026-1043', email: 'cto@startup.io', planSku: 'RA-PLAN-43-TEAM-20-SEATS', gateway: 'نووپیمنتس (USDT / BTC)', amount: '$89.00 USDT', licenseKey: 'RA-TEAM-PLAN43-77D9E0', date: '1405/07/19', status: 'تأییدشده (Paid)' }
      ],
      gateways: JSON.parse(JSON.stringify(DEFAULT_GATEWAYS)),
      plans: JSON.parse(JSON.stringify(DEFAULT_PLANS_46))
    };
  }

  function loadConfig() {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return createDefaultConfig();
      const parsed = JSON.parse(raw);
      const def = createDefaultConfig();
      return {
        branding: Object.assign({}, def.branding, parsed.branding || {}),
        sections: Object.assign({}, def.sections, parsed.sections || {}),
        features: Array.isArray(parsed.features) && parsed.features.length ? parsed.features : def.features,
        coupons: Array.isArray(parsed.coupons) ? parsed.coupons : def.coupons,
        orders: Array.isArray(parsed.orders) ? parsed.orders : def.orders,
        gateways: Array.isArray(parsed.gateways) && parsed.gateways.length === 6 ? parsed.gateways : def.gateways,
        plans: Array.isArray(parsed.plans) && parsed.plans.length === 46 ? parsed.plans : def.plans
      };
    } catch (e) {
      return createDefaultConfig();
    }
  }

  function saveConfig(cfg) {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(cfg));
  }

  function resetConfig() {
    const def = createDefaultConfig();
    saveConfig(def);
    return def;
  }

  function syncExchangeRate(usdToTomanRate) {
    const cfg = loadConfig();
    const rate = Math.max(10000, Number(usdToTomanRate) || 60000);
    cfg.plans.forEach(p => {
      if (p.usd > 0) {
        p.toman = Math.round((p.usd * rate) / 5000) * 5000;
      }
    });
    saveConfig(cfg);
    return cfg;
  }

  function toggleThemeMode() {
    const current = localStorage.getItem('RA_THEME_MODE') || 'dark';
    const next = current === 'dark' ? 'light' : (current === 'light' ? 'contrast' : 'dark');
    localStorage.setItem('RA_THEME_MODE', next);
    applyThemeMode(next);
  }

  function applyThemeMode(mode) {
    const m = mode || localStorage.getItem('RA_THEME_MODE') || 'dark';
    if (m === 'light') {
      document.documentElement.style.setProperty('--bg', '#f8fafc');
      document.documentElement.style.setProperty('--card', '#ffffff');
      document.documentElement.style.setProperty('--surface', '#ffffff');
      document.documentElement.style.setProperty('--text', '#0f172a');
      document.documentElement.style.setProperty('--muted', '#475569');
    } else if (m === 'contrast') {
      document.documentElement.style.setProperty('--bg', '#000000');
      document.documentElement.style.setProperty('--card', '#0a0a0a');
      document.documentElement.style.setProperty('--surface', '#0a0a0a');
      document.documentElement.style.setProperty('--text', '#ffff00');
      document.documentElement.style.setProperty('--muted', '#ffffff');
    } else {
      document.documentElement.style.setProperty('--bg', '#0b1120');
      document.documentElement.style.setProperty('--card', '#111827');
      document.documentElement.style.setProperty('--surface', '#1e293b');
      document.documentElement.style.setProperty('--text', '#f8fafc');
      document.documentElement.style.setProperty('--muted', '#94a3b8');
    }
  }

  function openCommandPalette() {
    let modal = document.getElementById('raCmdPaletteModal');
    if (!modal) {
      modal = document.createElement('div');
      modal.id = 'raCmdPaletteModal';
      modal.style.cssText = 'position:fixed;inset:0;background:rgba(2,6,23,0.82);z-index:10001;display:flex;align-items:flex-start;justify-content:center;padding-top:80px;font-family:Tahoma,sans-serif;direction:rtl;';
      modal.innerHTML = `
        <div style="width:92%;max-width:640px;background:#0f172a;border:2px solid #3b82f6;border-radius:14px;padding:18px;box-shadow:0 20px 50px rgba(0,0,0,0.7);color:#f8fafc;">
          <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:10px;">
            <strong>🔍 پالت فرمان سریع سایت (Ctrl+K Command Palette)</strong>
            <button onclick="document.getElementById('raCmdPaletteModal').style.display='none'" style="background:#ef4444;color:#fff;border:none;padding:4px 10px;border-radius:6px;cursor:pointer;">بستن (Esc)</button>
          </div>
          <input id="raCmdInput" type="text" placeholder="جستجو در ۴۶ اشتراک، ۶ درگاه، ۲۴ مهارت، ۲۰ قالب شغلی و صفحات سایت..." style="width:100%;padding:10px 12px;background:#020617;color:#fff;border:1px solid #334155;border-radius:8px;margin-bottom:12px;" />
          <div id="raCmdResults" style="max-height:310px;overflow-y:auto;display:flex;flex-direction:column;gap:6px;"></div>
        </div>
      `;
      document.body.appendChild(modal);
      document.getElementById('raCmdInput').addEventListener('input', renderCmdResults);
    }
    modal.style.display = 'flex';
    const inp = document.getElementById('raCmdInput');
    inp.value = '';
    renderCmdResults();
    setTimeout(() => inp.focus(), 50);
  }

  function renderCmdResults() {
    const q = (document.getElementById('raCmdInput')?.value || '').trim().toLowerCase();
    const cfg = loadConfig();
    const pages = [
      { title: '🏠 صفحهٔ اصلی و سندباکس سانسور PII (index.html)', url: 'index.html' },
      { title: '💳 پنل کاربری، آزمون انتخاب پلن و ۴۶ اشتراک (portal.html)', url: 'portal.html' },
      { title: '⚙️ پنل مدیریت کل سایت و قیمت‌ها (admin.html)', url: 'admin.html' },
      { title: '🛡️ مرکز اعتماد امنیتی و هش SHA-256 (trust.html)', url: 'trust.html' }
    ];
    const planItems = cfg.plans
      .filter(p => !q || p.fa.toLowerCase().includes(q) || p.en.toLowerCase().includes(q) || p.sku.toLowerCase().includes(q))
      .slice(0, 8)
      .map(p => ({ title: `📦 ${p.fa} — ${p.toman.toLocaleString('fa-IR')} تومان | $${p.usd}`, url: 'portal.html' }));

    const all = pages.concat(planItems);
    const box = document.getElementById('raCmdResults');
    if (box) {
      box.innerHTML = all.map(item => `
        <a href="${item.url}" style="text-decoration:none;color:#e2e8f0;background:#1e293b;padding:9px 12px;border-radius:8px;font-size:0.86rem;border:1px solid #334155;">${item.title}</a>
      `).join('');
    }
  }

  function injectAdminQuickBar() {
    if (document.getElementById('raAdminQuickBar')) return;
    if ('serviceWorker' in navigator) {
      navigator.serviceWorker.register('sw.js').catch(() => {});
    }
    applyThemeMode();
    const cfg = loadConfig();

    if (cfg.branding.showTopBanner) {
      const banner = document.createElement('div');
      banner.id = 'raTopAnnouncementBanner';
      banner.style.cssText = 'background:linear-gradient(90deg,#1d4ed8,#059669);color:#fff;text-align:center;padding:9px 16px;font-size:0.88rem;font-weight:bold;border-bottom:1px solid rgba(255,255,255,0.2);';
      banner.innerHTML = `${cfg.branding.topBannerTextFa} <span id="raFlashTimer" style="background:rgba(0,0,0,0.35);padding:2px 8px;border-radius:6px;margin-right:8px;font-family:monospace;">⏳ 04:59:42</span>`;
      document.body.insertBefore(banner, document.body.firstChild);
    }

    const bar = document.createElement('div');
    bar.id = 'raAdminQuickBar';
    bar.style.cssText = 'position:fixed;bottom:16px;left:16px;z-index:9999;display:flex;flex-wrap:wrap;gap:6px;background:rgba(15,23,42,0.96);border:1px solid #3b82f6;padding:8px 12px;border-radius:12px;box-shadow:0 10px 25px rgba(0,0,0,0.5);font-family:Tahoma,sans-serif;direction:rtl;';
    bar.innerHTML = `
      <a href="admin.html" style="color:#fff;background:#2563eb;text-decoration:none;padding:6px 11px;border-radius:8px;font-size:0.8rem;font-weight:bold;">⚙️ پنل مدیریت سایت</a>
      <a href="index.html" style="color:#cbd5e1;background:#1e293b;text-decoration:none;padding:6px 9px;border-radius:8px;font-size:0.8rem;">🏠 صفحهٔ اصلی</a>
      <a href="portal.html" style="color:#cbd5e1;background:#1e293b;text-decoration:none;padding:6px 9px;border-radius:8px;font-size:0.8rem;">💳 ۴۶ اشتراک</a>
      <a href="trust.html" style="color:#34d399;background:#1e293b;text-decoration:none;padding:6px 9px;border-radius:8px;font-size:0.8rem;">🛡️ امنیت و Trust</a>
      <button onclick="RoboticSiteCMS.openCommandPalette()" style="color:#fbbf24;background:#1e293b;border:1px solid #475569;padding:5px 9px;border-radius:8px;font-size:0.8rem;cursor:pointer;">🔍 Ctrl+K</button>
      <button onclick="RoboticSiteCMS.toggleThemeMode()" style="color:#e2e8f0;background:#1e293b;border:1px solid #475569;padding:5px 9px;border-radius:8px;font-size:0.8rem;cursor:pointer;">🌓 تم</button>
    `;
    document.body.appendChild(bar);

    window.addEventListener('keydown', e => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        openCommandPalette();
      } else if (e.key === 'Escape') {
        const m = document.getElementById('raCmdPaletteModal');
        if (m) m.style.display = 'none';
      }
    });
  }

  global.RoboticSiteCMS = {
    STORAGE_KEY,
    loadConfig,
    saveConfig,
    resetConfig,
    syncExchangeRate,
    toggleThemeMode,
    openCommandPalette,
    injectAdminQuickBar
  };
})(window);
