using System.Net;
using System.Text;

namespace Agent.Core.Documents;

public enum LegalDocumentTemplateKind
{
    MutualNda,
    FreelanceServiceAgreement,
    OfficialProformaInvoice,
    FormalPaymentReminderLetter
}

public sealed record RenderedLegalDocument(
    LegalDocumentTemplateKind Kind,
    string TitleFa,
    string TitleEn,
    string BilingualMarkdown,
    string StandaloneBilingualHtml);

/// <summary>
/// Bilingual (Persian &amp; English) Smart Contract, NDA, Proforma Invoice, and
/// Official Letter Builder for freelancers, agencies, and SMBs.
/// </summary>
public static class SmartContractAndLetterBuilder
{
    public static RenderedLegalDocument BuildDocument(
        LegalDocumentTemplateKind kind,
        string partyAName,
        string partyBName,
        string effectiveDate,
        string amountOrScope,
        string jurisdiction = "Tehran / International Arbitration")
    {
        var a = string.IsNullOrWhiteSpace(partyAName) ? "Party A / طرف اول" : partyAName.Trim();
        var b = string.IsNullOrWhiteSpace(partyBName) ? "Party B / طرف دوم" : partyBName.Trim();
        var dt = string.IsNullOrWhiteSpace(effectiveDate) ? "2026-10-10" : effectiveDate.Trim();
        var scope = string.IsNullOrWhiteSpace(amountOrScope) ? "Software & Consulting Services" : amountOrScope.Trim();

        var (titleFa, titleEn, bodyFa, bodyEn) = kind switch
        {
            LegalDocumentTemplateKind.MutualNda => (
                "توافق‌نامهٔ دوجانبهٔ حفظ محرمانگی اطلاعات (NDA)",
                "Mutual Non-Disclosure Agreement (NDA)",
                $"این توافق‌نامه در تاریخ **{dt}** فیمابین **{a}** (طرف اول) و **{b}** (طرف دوم) در خصوص موضوع «**{scope}**» منعقد می‌گردد. طرفین متعهد می‌شوند کلیهٔ اسناد، کدهای منبع، اطلاعات تجاری و داده‌های فنی مبادله‌شده را کاملاً محرمانه نگه داشته و بدون رضایت کتبی طرف مقابل افشا ننمایند.",
                $"This Mutual Non-Disclosure Agreement is entered into as of **{dt}** by and between **{a}** ('Party A') and **{b}** ('Party B') regarding '**{scope}**'. Both parties agree to hold all proprietary documents, source code, and business data in strict confidence."
            ),

            LegalDocumentTemplateKind.FreelanceServiceAgreement => (
                "قرارداد ارائهٔ خدمات تخصصی و توسعهٔ پروژه",
                "Professional Services & Freelance Agreement",
                $"این قرارداد در تاریخ **{dt}** بین کارفرما (**{a}**) و مجری (**{b}**) جهت انجام خدمات و تحویل پروژه با موضوع و مبلغ «**{scope}**» منعقد شد. مالکیت فکری خروجی‌ها پس از تسویهٔ کامل به کارفرما منتقل می‌گردد.",
                $"This Professional Services Agreement is made on **{dt}** between Client (**{a}**) and Contractor (**{b}**) for the scope and compensation of '**{scope}**'. Intellectual property rights transfer upon full settlement."
            ),

            LegalDocumentTemplateKind.OfficialProformaInvoice => (
                "پیش‌فاکتور رسمی خدمات / کالا (Proforma Invoice)",
                "Official Proforma Invoice",
                $"**فروشنده / صادرکننده:** {a}  \n**خریدار / کارفرما:** {b}  \n**تاریخ صدور:** {dt}  \n**شرح خدمات و مبلغ کل:** {scope}",
                $"**Vendor / Issuer:** {a}  \n**Billed To (Client):** {b}  \n**Issue Date:** {dt}  \n**Description & Total Amount:** {scope}"
            ),

            _ => (
                "نامهٔ رسمی پیگیری وضعیت صورت‌حساب",
                "Formal Invoice Settlement Reminder Letter",
                $"احتراماً، از طرف **{a}** به اطلاع شرکت محترم **{b}** می‌رساند که صورت‌حساب مورخ **{dt}** به مبلغ/موضوع «**{scope}**» در انتظار تسویه می‌باشد. پیشاپیش از همکاری شما سپاسگزاریم.",
                $"Dear **{b}**, on behalf of **{a}**, this is a courteous reminder regarding the invoice dated **{dt}** for '**{scope}**'. We appreciate your prompt attention and partnership."
            )
        };

        var md = new StringBuilder();
        md.AppendLine($"# {titleFa}");
        md.AppendLine($"## {titleEn}");
        md.AppendLine();
        md.AppendLine("### 🇮🇷 متن فارسی");
        md.AppendLine(bodyFa);
        md.AppendLine($"- **مرجع حل اختلاف:** {jurisdiction}");
        md.AppendLine();
        md.AppendLine("---");
        md.AppendLine();
        md.AppendLine("### 🇬🇧 English Text");
        md.AppendLine(bodyEn);
        md.AppendLine($"- **Governing Law / Jurisdiction:** {jurisdiction}");

        var html = $$"""
            <!DOCTYPE html>
            <html>
            <head>
              <meta charset="utf-8" />
              <title>{{WebUtility.HtmlEncode(titleFa)}} | {{WebUtility.HtmlEncode(titleEn)}}</title>
              <style>
                body { font-family: "Segoe UI", Tahoma, sans-serif; max-width: 860px; margin: 32px auto; color: #1E293B; line-height: 1.8; }
                .box-fa { direction: rtl; text-align: right; border: 1px solid #CBD5E1; border-radius: 10px; padding: 20px; margin-bottom: 20px; background: #F8FAFC; }
                .box-en { direction: ltr; text-align: left; border: 1px solid #CBD5E1; border-radius: 10px; padding: 20px; background: #FFFFFF; }
                h1, h2 { color: #1D4ED8; margin-top: 0; }
              </style>
            </head>
            <body>
              <div class="box-fa">
                <h1>{{WebUtility.HtmlEncode(titleFa)}}</h1>
                <p>{{WebUtility.HtmlEncode(bodyFa)}}</p>
              </div>
              <div class="box-en">
                <h2>{{WebUtility.HtmlEncode(titleEn)}}</h2>
                <p>{{WebUtility.HtmlEncode(bodyEn)}}</p>
              </div>
            </body>
            </html>
            """;

        return new RenderedLegalDocument(kind, titleFa, titleEn, md.ToString(), html);
    }
}
