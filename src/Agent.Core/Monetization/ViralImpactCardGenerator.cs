using Agent.Core.UX;
using System.Globalization;
using System.Net;

namespace Agent.Core.Monetization;

/// <summary>
/// Generates a shareable bilingual (Persian &amp; English) SVG / HTML Productivity
/// Impact Card from RoiDashboardSummary for Product-Led Viral Growth on LinkedIn,
/// Telegram, and X.
/// </summary>
public static class ViralImpactCardGenerator
{
    public static string GenerateShareableSvgCard(
        RoiDashboardSummary roi,
        string userDisplayName = "Robotic Agent User")
    {
        ArgumentNullException.ThrowIfNull(roi);
        var safeName = WebUtility.HtmlEncode(userDisplayName.Trim());
        var opsText = roi.TotalCompletedOperations.ToString("N0", CultureInfo.InvariantCulture);
        var itemsText = roi.TotalItemsAutomated.ToString("N0", CultureInfo.InvariantCulture);
        var hoursText = roi.EstimatedHoursSaved.ToString("0.0", CultureInfo.InvariantCulture);
        var valueUsd = roi.EstimatedMonetaryValueSavedUsd.ToString("0.00", CultureInfo.InvariantCulture);

        return $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="720" height="380" viewBox="0 0 720 380">
              <defs>
                <linearGradient id="bgGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                  <stop offset="0%" stop-color="#0F172A" />
                  <stop offset="100%" stop-color="#1E3A8A" />
                </linearGradient>
              </defs>
              <rect width="720" height="380" rx="20" fill="url(#bgGrad)" />
              <text x="40" y="54" fill="#60A5FA" font-family="Segoe UI, Vazirmatn, sans-serif" font-size="16" font-weight="bold">
                ROBOTIC AGENT — کارنامهٔ بهره‌وری و اتوماسیون هوشمند
              </text>
              <text x="40" y="92" fill="#FFFFFF" font-family="Segoe UI, Vazirmatn, sans-serif" font-size="26" font-weight="bold">
                {{safeName}} — Productivity Impact Report
              </text>

              <rect x="40" y="125" width="195" height="130" rx="14" fill="#1E293B" stroke="#334155" />
              <text x="60" y="165" fill="#94A3B8" font-family="Segoe UI, Vazirmatn, sans-serif" font-size="14">فایل‌ها و اسناد پردازش‌شده</text>
              <text x="60" y="185" fill="#94A3B8" font-family="Segoe UI, sans-serif" font-size="12">Automated Files &amp; Docs</text>
              <text x="60" y="230" fill="#38BDF8" font-family="Segoe UI, sans-serif" font-size="32" font-weight="bold">{{itemsText}}</text>

              <rect x="262" y="125" width="195" height="130" rx="14" fill="#1E293B" stroke="#334155" />
              <text x="282" y="165" fill="#94A3B8" font-family="Segoe UI, Vazirmatn, sans-serif" font-size="14">ساعت کار اداری ذخیره‌شده</text>
              <text x="282" y="185" fill="#94A3B8" font-family="Segoe UI, sans-serif" font-size="12">Hours Saved</text>
              <text x="282" y="230" fill="#34D399" font-family="Segoe UI, sans-serif" font-size="32" font-weight="bold">{{hoursText}} hrs</text>

              <rect x="485" y="125" width="195" height="130" rx="14" fill="#1E293B" stroke="#334155" />
              <text x="505" y="165" fill="#94A3B8" font-family="Segoe UI, Vazirmatn, sans-serif" font-size="14">ارزش مالی صرفه‌جویی</text>
              <text x="505" y="185" fill="#94A3B8" font-family="Segoe UI, sans-serif" font-size="12">Estimated Value Saved</text>
              <text x="505" y="230" fill="#FBBF24" font-family="Segoe UI, sans-serif" font-size="32" font-weight="bold">${{valueUsd}}</text>

              <text x="40" y="315" fill="#E2E8F0" font-family="Segoe UI, Vazirmatn, sans-serif" font-size="14">
                انجام‌شده در {{opsText}} عملیات ۱۰۰٪ محلی و امن | 21 Offline Skills + 20 SMB Playbooks + 200 AI Tools Hub
              </text>
              <text x="40" y="345" fill="#93C5FD" font-family="Segoe UI, sans-serif" font-size="13">
                Built with Robotic Agent — Local-First Multilingual Desktop Assistant
              </text>
            </svg>
            """;
    }
}
