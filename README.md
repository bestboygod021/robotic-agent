# رباتیک ایجنت

دستیار ویندوزی محلی برای خودکارسازی امن کارها. پروژه از طراحی وارد مرحلهٔ پیاده‌سازی MVP شده است.

## وضعیت MVP فعلی

- رابط دسکتاپ فارسی با WPF و C#/.NET 10.
- درخواست محدودِ کپی فایل؛ انتخاب دستی پوشهٔ مبدأ و مقصد.
- پیش‌نمایش فایل‌ها، فیلتر پسوند، سقف ۵۰۰ فایل، جلوگیری از بازنویسی و تأیید پیش از اجرا.
- کپی فقط از پوشهٔ انتخاب‌شده روی دیسک محلی و بدون ورود به زیرپوشه‌ها؛ مسیر شبکه‌ای رد می‌شود و فایل اصلی حذف یا جابه‌جا نمی‌شود.
- بازکردن Notepad، Calculator یا Paint فقط اگر در پوشهٔ استاندارد ویندوز موجود باشند؛ از فهرست ثابت، با تأیید کاربر و بدون shell، آرگومان یا دسترسی مدیر. این نسخه فقط برنامه را باز می‌کند و هنوز پنجرهٔ آن را با UI Automation کنترل نمی‌کند.
- تاریخچهٔ محلی JSON که مسیر فایل‌ها را ذخیره نمی‌کند.
- کاتالوگ آفلاین ۲۰۰ ابزار هوش مصنوعی در ۸ دسته، با ۲۵ ابزار در هر دسته، توضیح فارسی، نشان حروفی محلی و پیوند وب؛ فقط با کلیک کاربر در مرورگر پیش‌فرض باز می‌شود.
- دسته‌بند نیتِ سبک (Naive Bayes روی n-gram واژه/نویسه) از نمونه‌های فارسی/انگلیسیِ همراه برنامه ساخته می‌شود؛ هیچ دادهٔ کاربر را یاد نمی‌گیرد. مدل زبانی مولد و اتصال API اجرا نمی‌شوند. خود برنامه اطلاعات را به این سایت‌ها نمی‌فرستد؛ استفاده از سرویس وب پس از کلیک تابع سیاست ارائه‌دهنده است.

## جهت معماری بعدی

هدف جدید، دو مسیر **Local** و **Hybrid** است: کار سبک به‌صورت محلی و زیر بودجهٔ منابع؛ کار سنگین می‌تواند پس از نمایش provider، دادهٔ ارسالی و زمان تقریبی، با تأیید همان تسک به API بیرونی سپرده شود. قراردادهای provider/ETA، probe سخت‌افزار Windows (CPU/RAM/volume/برق و باتری)، پنل تنظیم و ذخیرهٔ محلی بودجهٔ منابع، و ResourceGovernor با سقف‌های غیرقابل‌دورزدن و بازتوزیع سهم‌های دستی اضافه شده‌اند. snapshot سخت‌افزار هنگام شروع، هر ۳۰ ثانیه و بلافاصله پیش از کپی تازه می‌شود. پیش‌نمایش کپی و اجرای آن حداقل ۱ GiB فضای آزاد را به‌عنوان reserve نگه می‌دارند؛ throughput کپی سقف سخت ۱۰ MiB/s دارد و سقف governor برحسب برق شهری/باتری/نامشخص به‌ترتیب ۱۰/۳/۲ MiB/s است، پیش از اعمال شدت و سهم تنظیم‌شده. درصد CPU/RAM هنوز با Job Object یا limiter حین اجرا enforce نمی‌شود، چون runtime مدل محلی وجود ندارد. اتصال API و مدل مولد پیاده‌سازی نشده‌اند. سقف‌ها اولیه‌اند و تضمین جلوگیری از خرابی فیزیکی سخت‌افزار نیستند. «رایگان»بودن APIها به سهمیه و سیاست متغیر provider وابسته است و تضمین نمی‌شود. مسیر Local به‌تنهایی باید کاملاً آفلاین بماند.

حالت پایه برای آزمون روی Windows x64 با ۴ گیگابایت RAM و CPU دو‌هسته‌ای طراحی شده است؛ این هدف هنوز روی سخت‌افزار واقعی benchmark نشده است.

## اجرا در ویندوز و خط فرمان چندسکویی (CLI)

با نصب .NET 10 SDK، اجرای رابط دسکتاپ در ویندوز:

```powershell
dotnet run --project src/Agent.Desktop/Agent.Desktop.csproj
```

اجرای نسخهٔ خط فرمان چندسکویی (`Agent.Cli` در ویندوز، لینوکس و macOS):

```bash
dotnet run --project src/Agent.Cli/Agent.Cli.csproj -- skills --lang fa
dotnet run --project src/Agent.Cli/Agent.Cli.csproj -- playbooks --lang en
dotnet run --project src/Agent.Cli/Agent.Cli.csproj -- tools --search "pdf" --lang fa
dotnet run --project src/Agent.Cli/Agent.Cli.csproj -- plans --lang fa
dotnet run --project src/Agent.Cli/Agent.Cli.csproj -- checkout --sku plan.06.pro_lifetime --gateway ZarinPal --email user@example.com
dotnet run --project src/Agent.Cli/Agent.Cli.csproj -- security-audit
dotnet run --project src/Agent.Cli/Agent.Cli.csproj -- commerce-quote --sku plan.06.pro_lifetime --country IR
dotnet run --project src/Agent.Cli/Agent.Cli.csproj -- portal-pro --email user@example.com --role حسابدار
dotnet run --project src/Agent.Cli/Agent.Cli.csproj -- admin-marketing
dotnet run --project src/Agent.Cli/Agent.Cli.csproj -- diagnose --lang fa
```

## پنل مدیریت سایت، فروشگاه ۴۶ اشتراک، ۶ درگاه پرداخت و ۵۰ قابلیت ارتقای امنیتی/تجاری

- **۲۴ مهارت آفلاین و ۲۰ قالب آمادهٔ شغلی** در `src/Agent.Core/Skills/` و `src/Agent.Core/Workflows/PlaybookCatalog.cs` با حفظ ۱۰۰٪ فایل اصلی در مهارت‌های سانسور PII و پاک‌سازی متادیتا (خروجی با پسوند `_sanitized` و واگرد تک‌کلیکی `SHA-256`).
- **۴۶ مدل اشتراک در ۵ دسته** (`src/Agent.Core/Monetization/SubscriptionCatalog46.cs`) و **۶ درگاه پرداخت** (`src/Agent.Core/Monetization/PaymentGatewayOrchestrator.cs`):
  - **۳ درگاه ایرانی:** زرین‌پال (`ZarinPal`)، زیبال (`Zibal`) و آیدی‌پی (`IDPay`)
  - **۳ درگاه خارجی:** استرایپ (`Stripe`)، لمون‌اسکوییزی (`LemonSqueezy`) و نووپیمنتس کریپتو (`NOWPayments USDT/BTC`)
- **مجموعهٔ کامل وب (`PWA`) و پنل مدیریت بدون کدنویسی (`Visual CMS`):**
  - `web/index.html` — صفحهٔ فرود ۶ زبانه (`fa, en, zh, es, ar, fr`)، سندباکس زندهٔ سانسور اطلاعات محرمانه در مرورگر، کاوشگر ۲۰ قالب شغلی و ماشین‌حساب بازگشت سرمایه (`ROI`).
  - `web/portal.html` (و پنجرهٔ دسکتاپ `UserBillingPortalWindow.xaml`) — پنل کاربری حرفه‌ای، آزمون هوشمند انتخاب پلن، مسیریابی `Geo-IP` درگاه، کیف پول دوارزی، اقساط ۴ مرحله‌ای `BNPL`، فاکتور رسمی `QR`، مدیریت دستگاه‌های فعال، توکن `MCP/CLI`، بکاپ رمزنگاری‌شدهٔ سرتاسری `Zero-Knowledge`، جدول مقایسهٔ ستونی ۴۶ اشتراک و قراردادساز دوزبانه.
  - `web/admin.html` (و پنجرهٔ دسکتاپ `SiteAdminWindow.xaml`) — پنل مدیریت جامع کل سایت، پیش‌نمایش زندهٔ دوقلو (`Split-Screen`)، همگام‌سازی نرخ ارز، تست `A/B`، مدیریت نقش‌های ادمین (`RBAC`)، تسویهٔ ۷۰/۳۰ ناشران افزونه، احراز هویت دومرحله‌ای `TOTP 2FA / WebAuthn`، زنجیرهٔ هش حسابرسی مدیر و شبیه‌ساز ضدتقلب.
  - `web/trust.html` — مرکز اعتماد امنیتی، جدول هش‌های رسمی `SHA-256`، دستورات `Sigstore` و هدرهای امنیتی `CSP / HSTS`.
  - `browser-extension/` — افزونهٔ مرورگر کروم و اج (`Manifest V3`) برای سانسور PII و پرامپت‌سازی در وب.

آزمون‌های هسته و مجموعهٔ فازی امنیتی (تمام ۸ فاز و ۱۰۰ قابلیت)، بدون چارچوب تست یا بستهٔ بیرونی:

```powershell
dotnet run --project tests/Agent.Core.Tests/Agent.Core.Tests.csproj
```

## اسناد

- [راهنمای جامع دوزبانهٔ کار با نرم‌افزار و کسب درآمد (فارسی و انگلیسی)](docs/user-guide-fa-en.md)
- [شناخت پروژه، قابلیت‌های فعلی و ساختار مخزن](docs/project-overview-fa.md)
- [نقشهٔ راه بین‌المللی‌سازی، معماری کدنویسی و فازبندی ۱۰۰ قابلیت](docs/international-roadmap-fa.md)
- [فهرست ۲۰۰ ابزار هوش مصنوعی با توضیح و پیوند ورود](docs/ai-tools-fa.md)
- [معماری ترکیبی Local/Hybrid و مسیریابی تسک‌ها](docs/hybrid-ai-architecture-fa.md)
- [معماری اولیهٔ محلی و نقشهٔ راه](docs/local-first-architecture-fa.md)
- [سند طراحی و ۲۰۰ پیشنهاد اجرایی](docs/blueprint-fa.md)
