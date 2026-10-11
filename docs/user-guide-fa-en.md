# 📖 راهنمای جامع کار با نرم‌افزار و کسب درآمد (Robotic Agent)
# 📖 Complete Bilingual User Guide & Monetization Manual (Persian & English)

> **نسخهٔ ۲.۰ بین‌المللی (International Edition v2.0)**
> پشتیبانی از **۶ زبان زندهٔ دنیا** (`فارسی`, `English`, `中文`, `Español`, `العربية`, `Français`) | **۲۱ مهارت آفلاین فایل و سند** | **۲۰ قالب آمادهٔ شغلی (SMB Playbooks)** | **هاب ۲۰۰ ابزار هوش مصنوعی** | **مربی پرامپت‌ساز تعاملی**

---

## 🇮🇷 بخش اول: راهنمای کامل فارسی (Persian User Guide)

### ۱. فلسفهٔ کار و شروع سریع در ۶۰ ثانیه
رباتیک ایجنت بر پایهٔ دو اصل طلایی طراحی شده است:
1. **هیچ تغییری بدون پیش‌نمایش و تأیید شما انجام نمی‌شود (`Mandatory Preview & Human-in-the-Loop`)**.
2. **تمام عملیات دارای دکمهٔ «واگرد آخرین عملیات (Undo)» با بررسی هش `SHA-256` هستند**.

**مراحل استفاده در پنجرهٔ اصلی (`MainWindow`):**
1. از منوی کشویی بالای صفحه، **زبان رابط کاربری** را انتخاب کنید (کلید میان‌بر: `Ctrl+L`).
2. **پوشهٔ مبدأ** و **پوشهٔ مقصد** را انتخاب کنید (یا پوشه را با ماوس بکشید و رها کنید — `Drag & Drop`).
3. درخواست خود را به زبان محاوره‌ای بنویسید؛ مثلاً:
   * *«فایل‌های PDF و DOCX بزرگ‌تر از ۲ مگابایت در زیرپوشه‌ها را کپی کن»*
4. روی **«بررسی و پیش‌نمایش»** کلیک کنید تا فهرست فایل‌ها، حجم کل و فضای آزاد دیسک بررسی شود.
5. روی **«تأیید و اجرا»** کلیک کنید.
6. هر زمان خواستید عملیات را به حالت اول برگردانید، روی **«واگرد آخرین عملیات (Undo)»** کلیک کنید.

---

### ۲. فهرست ۲۱ مهارت آفلاین نرم‌افزار (`SkillRegistry`)

#### الف) مهارت‌های مدیریت هوشمند فایل (۶ مهارت)
| شناسهٔ مهارت | نام مهارت | کاربرد برای کاربران عادی و اداری |
|---|---|---|
| `file.copy` | کپی هوشمند با فیلتر چندگانه | کپی فایل‌ها بر اساس چند پسوند، بازهٔ حجم، تاریخ و زیرپوشه‌ها (تا ۱۰ سطح) با سیاست برخورد (رد کردن / شماره‌گذاری خودکار) |
| `file.rename` | تغییر نام دسته‌ای هوشمند | افزودن پیشوند، پسوند، شمارنده (`001, 002`) و تاریخ به صدها فایل با جدول پیش‌نمایش «نام قدیم $\leftarrow$ نام جدید» |
| `file.organize` | مرتب‌سازی خودکار پوشه | مرتب کردن خودکار پوشهٔ شلوغ `Downloads` یا `Desktop` در ۶ پوشهٔ استاندارد (`Documents`, `Images`, `Videos`, `Audio`, `Archives`, `Code`) |
| `file.deduplicate` | یافتن و قرنطینهٔ فایل‌های تکراری | مقایسهٔ دو مرحله‌ای (حجم + هش `SHA-256`) و انتقال امن فایل‌های تکراری به پوشهٔ قرنطینه بدون حذف دائمی |
| `file.archive` | آرشیو و استخراج امن `ZIP` | فشرده‌سازی و بازکردن فایل‌های ZIP با محافظت کامل در برابر حملات `Zip-Slip` و `Zip-Bomb` |
| `file.backup` | پشتیبان‌گیری افزایشی هوشمند | کپی کردن فقط فایل‌های جدید یا تغییریافته برای بکاپ روزانهٔ سریع |

#### ب) مهارت‌های پردازش آفلاین اسناد و حریم خصوصی (۱۳ مهارت)
| شناسهٔ مهارت | نام مهارت | کاربرد برای کاربران عادی و شرکت‌ها |
|---|---|---|
| `doc.redact_pii` | سانسور اطلاعات محرمانه (`PII`) | شناسایی ایمیل، تلفن، شبا (`IBAN`) و شماره کارت بانکی (با بررسی الگوریتم **Luhn**). **فایل اصلی ۱۰۰٪ حفظ می‌شود** و نسخهٔ پاک‌سازی‌شده با پسوند `_sanitized` ذخیره می‌گردد |
| `doc.scrub_metadata` | حذف متادیتای مخفی عکس و سند | حذف مختصات مکانی `GPS` و سریال دوربین (`EXIF`) از تصاویر JPEG در نسخهٔ `_sanitized` پیش از انتشار در اینترنت |
| `doc.media_optimize` | تبدیل عکس‌ها به PDF و بهینه‌سازی حجم | تبدیل چند عکس از مدارک یا جزوات به یک فایل PDF یکپارچه یا کاهش حجم برای آپلود در سامانه‌های ثبت‌نامی |
| `doc.ocr` | استخراج متن آفلاین (`Offline OCR`) | استخراج لایهٔ متنی از اسناد و تصاویر در ۶ زبان به فایل `.ocr.txt` |
| `doc.invoice_extract` | استخراج فاکتور به اکسل (`CSV`) | استخراج شماره فاکتور، تاریخ، نام فروشنده، مالیات، مبلغ کل و واحد پول به جدول استاندارد CSV |
| `doc.pdf_toolkit` | جعبهٔ ابزار بومی `PDF` | شمارش صفحات، استخراج متن و ادغام چند فایل در یک سند استاندارد `PDF-1.4` بدون اینترنت |
| `doc.csv_clean` | تمیزکاری جداول `CSV` | حذف ردیف‌های خالی و تکراری و یکسان‌سازی حروف `ی/ک` و ارقام فارسی/عربی |
| `doc.diff` | مقایسهٔ دو نسخه از قرارداد (`Diff`) | مقایسهٔ خط‌به‌خط دو سند با الگوریتم `LCS` و تولید گزارش تغییرات (`Markdown Diff`) |
| `doc.folder_report` | گزارش تحلیلی فضای پوشه | تولید گزارش تفکیکی تعداد فایل‌ها، حجم کل و سهم هر پسوند در ۶ زبان |
| `doc.template_merge` | پرکردن خودکار فرم‌ها (`Mail-Merge`) | خواندن جدول `CSV` و جایگزینی متغیرهای `{{Field}}` در قالب قرارداد یا گواهی‌نامه |
| `doc.search` | جست‌وجوی معنایی محلی در محتوا | یافتن فایل‌ها بر اساس مفهوم و کلمات داخل متن (بدون ارسال به ابر) |
| `doc.summarize` | خلاصه‌سازی دسته‌ای و استخراج اقدام‌ها | تولید چکیدهٔ اجرایی اسناد و استخراج خودکار فهرست کارهای قابل پیگیری (`Action Items` / `اقدام`) |
| `doc.convert` | تبدیل فرمت دسته‌ای آفلاین | تبدیل Markdown و CSV به صفحات HTML مستقل دوجهته (`RTL/LTR`) |

#### ج) مهارت‌های کلیپ‌بورد هوشمند و فرم وب (۲ مهارت)
| شناسهٔ مهارت | نام مهارت | کاربرد |
|---|---|---|
| `clipboard.smart` | کلیپ‌بورد هوشمند | پاک‌سازی فرمت متن، تبدیل سلول‌های کپی‌شده از اکسل به جدول Markdown/CSV و سانسور فوری اطلاعات حساس در کلیپ‌بورد |
| `web.form_fill` | اتوماسیون کنترل‌شدهٔ فرم وب | آماده‌سازی فیلدهای فرم وب از دادهٔ محلی با مسدودسازی فیلدهای رمز/بانکی و توقف اجباری پیش از ثبت نهایی |

---

### ۳. هاب ۲۰۰ ابزار هوش مصنوعی و «پرامپت‌ساز تعاملی» (`AiToolsWindow` & `UserGuideWindow`)
* **پیشنهاد هوشمند ابزار (`AI Matchmaker`):** مشکل خود را بنویسید تا از میان ۲۰۰ ابزار در ۸ دسته (چت‌بات، کدنویسی، تصویر، ویدیو، صدا، بهره‌وری، پژوهش، اتوماسیون)، بهترین گزینه پیشنهاد شود.
* **مقایسهٔ رودرروی ابزارها:** ۲ تا ۴ ابزار را انتخاب کنید تا ماتریس مقایسهٔ آن‌ها نمایش داده شود.
* **مربی پرامپت‌ساز تعاملی (`Interactive Prompt Coach`):** هدف خود را به زبان ساده وارد کنید؛ برنامه به‌طور خودکار اطلاعات شخصی/مسیرهای محلی را ماسک می‌کند و یک پرامپت مهندسی‌شدهٔ حرفه‌ای تحویل می‌دهد.

---

### ۴. ساده‌سازی روزمره: حباب Drag & Drop و کلیک‌راست ویندوز
* **حباب هوشمند کشیدن و رها کردن (`Magic Drop-Zone`):** پنجرهٔ راهنما (`F1`) را باز کنید و هر عکس، PDF یا جدول CSV را روی آن بیندازید؛ برنامه فوراً ۳ اقدام مناسب همان فایل را پیشنهاد می‌دهد.
* **منوی کلیک‌راست ویندوز:** با اسکریپت رجیستری سطح کاربر (`WindowsShellIntegrationService`)، گزینهٔ «🤖 انجام با رباتیک ایجنت» به کلیک‌راست پوشه‌ها و فایل‌ها اضافه می‌شود.

---

### ۵. راهنمای کسب درآمد از نرم‌افزار (Monetization Architecture)
در کدهای پروژه (`src/Agent.Core/Monetization/`) سه موتور درآمدزایی آماده تعبیه شده است:
1. **فروش لایسنس `Pro`، `Lifetime Deal` (مادام‌العمر) و `Enterprise` (`LicenseAndPlanManager`):**
   * می‌توانید کلیدهای لایسنس آفلاین امضاشده (`RA-PRO-...`, `RA-LTD-...`, `RA-ENT-...`) را با متد `LicenseAndPlanManager.GenerateSignedLicenseKey` تولید کرده و در سایت‌های بین‌المللی (**Gumroad, LemonSqueezy, AppSumo**) یا درگاه‌های داخلی بفروشید.
2. **درآمد مشارکت در فروش ۲۰۰ ابزار هوش مصنوعی (`AffiliateLinkBuilder`):**
   * هر کلیکی که کاربران (حتی کاربران نسخهٔ رایگان!) روی لینک ابزارهای داخل کاتالوگ انجام می‌دهند، به‌طور خودکار دارای تگ معرف (`?utm_source=robotic_agent&ref=...`) می‌شود.
3. **بازاریابی ویروسی با کارت گرافیکی بهره‌وری (`ViralImpactCardGenerator`):**
   * کاربران با یک کلیک، تصویر `SVG` کارنامهٔ صرفه‌جویی زمان و هزینهٔ خود را روی دسکتاپ ذخیره کرده و در لینکدین و تلگرام منتشر می‌کنند.

---

## 🇬🇧 Part 2: Complete English User Guide & Manual

### 1. Core Philosophy & 60-Second Quick Start
Robotic Agent is built on two non-negotiable guarantees:
1. **Mandatory Preview & Human-in-the-Loop Approval:** No file is ever modified, moved, or created before you inspect and approve the preview.
2. **1-Click Transactional Undo (`RollbackManager`):** Every operation generates a cryptographic `SHA-256` manifest so you can revert changes with a single click.

**How to use the Main Window (`MainWindow`):**
1. Select your language (`fa`, `en`, `zh`, `es`, `ar`, `fr`) from the top bar (Shortcut: `Ctrl+L`).
2. Pick your **Source** and **Destination** folders (or Drag & Drop a folder directly).
3. Type your request in natural language, e.g.:
   * *"Copy PDF and DOCX files larger than 2 MB including subfolders"*
4. Click **Preview** to inspect matched files, total bytes, and disk headroom.
5. Click **Approve & Run**.
6. Click **Undo Last Operation** anytime to roll back safely.

---

### 2. All 21 Offline Skills at a Glance (`SkillRegistry`)
* **File Management (6 Skills):**
  * `file.copy` (Multi-filter copy with recursive depth & conflict policies)
  * `file.rename` (Batch pattern rename with Old $\rightarrow$ New preview table)
  * `file.organize` (Auto-sort clutter into 6 standard category folders)
  * `file.deduplicate` (Two-stage Size + SHA-256 duplicate finder & safe staging)
  * `file.archive` (Safe ZIP compression & extraction with Zip-Slip / Zip-Bomb guards)
  * `file.backup` (Smart incremental backup for new/modified files only)
* **Offline Document Processing & Privacy (13 Skills):**
  * `doc.redact_pii` (Masks emails, phones, IBANs, IPv4, and Luhn-verified credit cards into `*_sanitized` copies while keeping originals 100% intact)
  * `doc.scrub_metadata` (Strips hidden JPEG EXIF/GPS segments and document header metadata into `*_sanitized` copies)
  * `doc.media_optimize` (Combines mobile photos/scans into a single clean PDF or optimizes file size for web portal uploads)
  * `doc.ocr` (Multilingual offline OCR to `.ocr.txt`)
  * `doc.invoice_extract` (Extracts invoice number, date, vendor, tax, total, and currency into formula-injection-safe CSV tables)
  * `doc.pdf_toolkit` (Offline PDF page counter, text extractor, and `PDF-1.4` merger)
  * `doc.csv_clean` (Deduplicates rows and normalizes multilingual digits/scripts)
  * `doc.diff` (Line-by-line LCS document comparison & Markdown diff report)
  * `doc.folder_report` (Storage & extension breakdown report in 6 languages)
  * `doc.template_merge` (Offline `{{Field}}` mail-merge generator from CSV rows)
  * `doc.search` (Local TF-IDF semantic & lexical search across documents)
  * `doc.summarize` (Batch extractive summarizer & action-item checklist extractor)
  * `doc.convert` (Batch Markdown/CSV to standalone RTL/LTR HTML or Markdown tables)
* **Desktop & Web Productivity (2 Skills):**
  * `clipboard.smart` (Instant TSV-to-Markdown/CSV converter, formatting cleaner, and PII-safe clipboard history)
  * `web.form_fill` (Controlled HTTPS form filler that blocks password/card fields and pauses for final human submission)

---

### 3. Multi-Step Workflows & 20 SMB Playbooks (`WorkflowEngine`)
* Run 20 pre-built multi-step playbooks across **Accounting, Legal, HR, Content, Customer Support, and IT**.
* **End-to-End Dry-Run Preview:** Simulates the entire pipeline and resolves step variables (`${sourceDir}`, `${outputDir}`, `${prev.OutputPath}`) before execution.
* **Reverse-Order Transactional Rollback:** If any step fails or you trigger Undo, all completed steps roll back in reverse order.
* **Portable `.agentflow.json` Sharing:** Export and share workflows safely without exposing local system paths.

---

### 4. Interactive Prompt Coach, Magic Drop-Zone & CLI
* Press **`F1`** (or click **📖 راهنمای جامع و مربی هوشمند (FA/EN)** in the top bar) to open `UserGuideWindow`:
  * Switch instantly between **Persian (`RTL`)** and **English (`LTR`)**.
  * Use the **Interactive Prompt Coach** to turn simple goals into structured, PII-scrubbed expert prompts paired with the best tools from the 200 AI Tools Catalog.
  * Drag & drop any file onto the **Magic Drop-Zone** to get 3 tailored quick actions.
  * Activate offline **Pro / Lifetime Deal (`RA-LTD-...`) / Enterprise** keys and export your **Shareable SVG Productivity Impact Card**.
* **Cross-Platform CLI (`agent-cli`):**
  ```bash
  dotnet run --project src/Agent.Cli/Agent.Cli.csproj -- skills --lang en
  dotnet run --project src/Agent.Cli/Agent.Cli.csproj -- playbooks --lang en
  dotnet run --project src/Agent.Cli/Agent.Cli.csproj -- tools --search "invoice" --lang en
  dotnet run --project src/Agent.Cli/Agent.Cli.csproj -- diagnose --lang en
  ```
