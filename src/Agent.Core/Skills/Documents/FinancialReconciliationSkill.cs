using Agent.Core.Abstractions;
using Agent.Core.Localization;
using Agent.Core.Security;
using Agent.Core.Skills.Rollback;
using System.Globalization;
using System.Text;

namespace Agent.Core.Skills.Documents;

public enum ReconciliationMatchKind
{
    MatchedExact,
    AmountDiscrepancy,
    UnpaidInvoice,
    UnmatchedBankDeposit
}

public sealed record ReconciliationItem(
    string ReferenceId,
    ReconciliationMatchKind Kind,
    decimal? InvoiceAmount,
    decimal? BankDepositAmount,
    string PartyOrDescription);

public sealed record FinancialReconciliationRequest(
    string InvoicesCsvPath,
    string BankStatementCsvPath,
    string OutputReportPath);

public sealed record FinancialReconciliationPreview(
    string InvoicesCsvPath,
    string BankStatementCsvPath,
    string OutputReportPath,
    IReadOnlyList<ReconciliationItem> Items)
{
    public int MatchedPaidCount => Items.Count(i => i.Kind == ReconciliationMatchKind.MatchedExact);
    public int DiscrepancyCount => Items.Count(i => i.Kind == ReconciliationMatchKind.AmountDiscrepancy);
    public int UnpaidInvoicesCount => Items.Count(i => i.Kind == ReconciliationMatchKind.UnpaidInvoice);
    public int UnmatchedDepositsCount => Items.Count(i => i.Kind == ReconciliationMatchKind.UnmatchedBankDeposit);
}

public sealed record FinancialReconciliationResult(
    int MatchedPaidCount,
    int DiscrepancyCount,
    int UnpaidInvoicesCount,
    int UnmatchedDepositsCount,
    string OutputReportPath,
    OperationManifest RollbackManifest);

/// <summary>
/// Offline Financial &amp; Bank Reconciliation Skill (doc.reconcile_tables):
/// compares an Invoices CSV against a Bank Statement CSV, matching reference IDs
/// and amounts (with Persian/Arabic digit normalization) to spot unpaid invoices,
/// amount discrepancies, and unmatched bank deposits with 1-click Undo.
/// </summary>
public sealed class FinancialReconciliationSkill : ISkill<FinancialReconciliationRequest, FinancialReconciliationPreview, FinancialReconciliationResult>
{
    public const string SkillId = "doc.reconcile_tables";

    private static readonly SkillDescriptor DefaultDescriptor = new(
        Id: SkillId,
        DisplayNames: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "مغایرت‌گیری خودکار بانکی و تطبیق فاکتورها با صورت‌حساب",
            ["en"] = "Automated Bank Statement & Invoice Reconciliation",
            ["zh"] = "银行对账单与发票自动核对（财务对账）",
            ["es"] = "Conciliación Bancaria Automática de Facturas y Extractos",
            ["ar"] = "المطابقة المالية الآلية بين الفواتير وكشف الحساب البنكي",
            ["fr"] = "Rapprochement Bancaire Automatisé (Factures & Relevés)"
        },
        Descriptions: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fa"] = "مقایسهٔ آفلاین جدول فاکتورهای صادرشده با صورت‌حساب بانکی (CSV)، شناسایی فاکتورهای تسویه‌شده، پرداخت‌نشده و مغایرت‌های مبلغی همراه با واگرد.",
            ["en"] = "Reconciles issued invoices CSV against bank statements offline, flagging paid, unpaid, and discrepant rows with Undo.",
            ["zh"] = "离线比对发票表格与银行流水 CSV，自动标记已付、未付及金额差异项并支持撤销。",
            ["es"] = "Cruza facturas emitidas con extractos bancarios en CSV sin conexión, detectando impagos y diferencias.",
            ["ar"] = "يقارن جدول الفواتير بكشف الحساب البنكي محلياً لاكتشاف الفواتير المسددة وغير المسددة والفروقات المالية.",
            ["fr"] = "Compare hors-ligne les factures émises aux relevés bancaires CSV et signale les écarts avec annulation."
        },
        RiskLevel: SkillRiskLevel.ReversibleMutation,
        RequiresPreviewApproval: true,
        SupportsRollback: true,
        WorksOffline: true);

    public SkillDescriptor Descriptor => DefaultDescriptor;

    public FinancialReconciliationPreview Preview(FinancialReconciliationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!File.Exists(request.InvoicesCsvPath))
        {
            throw new FileNotFoundException("فایل CSV فاکتورها پیدا نشد.", request.InvoicesCsvPath);
        }

        if (!File.Exists(request.BankStatementCsvPath))
        {
            throw new FileNotFoundException("فایل CSV صورت‌حساب بانکی پیدا نشد.", request.BankStatementCsvPath);
        }

        var invFull = Path.GetFullPath(request.InvoicesCsvPath);
        var bankFull = Path.GetFullPath(request.BankStatementCsvPath);
        var outFull = Path.GetFullPath(request.OutputReportPath);

        PathGuard.ValidateSelectedFolder(Path.GetDirectoryName(invFull)!, "پوشهٔ فاکتورها");
        PathGuard.ValidateSelectedFolder(Path.GetDirectoryName(bankFull)!, "پوشهٔ صورت‌حساب بانکی");
        PathGuard.ValidateSelectedFolder(Path.GetDirectoryName(outFull)!, "پوشهٔ خروجی گزارش مغایرت‌گیری");

        var invoices = ParseLedgerRows(File.ReadAllLines(invFull, Encoding.UTF8));
        var deposits = ParseLedgerRows(File.ReadAllLines(bankFull, Encoding.UTF8));
        var items = ReconcileLedgers(invoices, deposits);

        return new FinancialReconciliationPreview(invFull, bankFull, outFull, items);
    }

    public async Task<FinancialReconciliationResult> ExecuteAsync(
        FinancialReconciliationRequest request,
        FinancialReconciliationPreview approvedPreview,
        ISkillContext context)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        ArgumentNullException.ThrowIfNull(context);

        var currentPreview = Preview(request);
        if (currentPreview.Items.Count != approvedPreview.Items.Count)
        {
            throw new InvalidOperationException("ردیف‌های جدول مالی از زمان پیش‌نمایش تغییر کرده‌اند؛ دوباره پیش‌نمایش بگیرید.");
        }

        var sb = new StringBuilder();
        sb.AppendLine("# گزارش مغایرت‌گیری بانکی و فاکتورها / Financial Reconciliation Report");
        sb.AppendLine();
        sb.AppendLine($"- **فاکتورهای تسویه‌شده (Matched Paid):** `{currentPreview.MatchedPaidCount}`");
        sb.AppendLine($"- **مغایرت مبلغی (Amount Discrepancies):** `{currentPreview.DiscrepancyCount}`");
        sb.AppendLine($"- **فاکتورهای پرداخت‌نشده (Unpaid Invoices):** `{currentPreview.UnpaidInvoicesCount}`");
        sb.AppendLine($"- **واریزی‌های بدون فاکتور (Unmatched Bank Deposits):** `{currentPreview.UnmatchedDepositsCount}`");
        sb.AppendLine();
        sb.AppendLine("| Reference ID | Status | Invoice Amount | Bank Deposit | Description |");
        sb.AppendLine("|---|---|---:|---:|---|");

        foreach (var item in currentPreview.Items)
        {
            var invStr = item.InvoiceAmount?.ToString("0.00", CultureInfo.InvariantCulture) ?? "-";
            var bankStr = item.BankDepositAmount?.ToString("0.00", CultureInfo.InvariantCulture) ?? "-";
            sb.AppendLine($"| `{item.ReferenceId}` | **{item.Kind}** | {invStr} | {bankStr} | {item.PartyOrDescription} |");
        }

        await File.WriteAllTextAsync(currentPreview.OutputReportPath, sb.ToString(), new UTF8Encoding(false), context.CancellationToken).ConfigureAwait(false);
        var info = new FileInfo(currentPreview.OutputReportPath);
        var sha256 = await RollbackManager.ComputeFileSha256HexAsync(currentPreview.OutputReportPath, context.CancellationToken).ConfigureAwait(false);

        var manifest = new OperationManifest(
            OperationId: Guid.NewGuid().ToString("N"),
            SkillId: SkillId,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Records:
            [
                new OperationRecord(
                    OperationRecordKind.FileCreatedOrCopied,
                    OriginalPath: currentPreview.InvoicesCsvPath,
                    TargetPath: currentPreview.OutputReportPath,
                    SizeBytes: info.Length,
                    ContentSha256Hex: sha256)
            ]);

        return new FinancialReconciliationResult(
            currentPreview.MatchedPaidCount,
            currentPreview.DiscrepancyCount,
            currentPreview.UnpaidInvoicesCount,
            currentPreview.UnmatchedDepositsCount,
            currentPreview.OutputReportPath,
            manifest);
    }

    private static Dictionary<string, (decimal Amount, string Description)> ParseLedgerRows(string[] lines)
    {
        var dict = new Dictionary<string, (decimal Amount, string Description)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            var cells = lines[i].Split(',').Select(c => TextNormalizer.NormalizeDigitsToAscii(c.Trim())).ToArray();
            if (cells.Length < 2)
            {
                continue;
            }

            var refId = cells[0];
            if (string.IsNullOrWhiteSpace(refId))
            {
                continue;
            }

            decimal.TryParse(cells[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var amount);
            var desc = cells.Length >= 3 ? cells[2] : string.Empty;
            dict[refId] = (amount, desc);
        }

        return dict;
    }

    private static IReadOnlyList<ReconciliationItem> ReconcileLedgers(
        Dictionary<string, (decimal Amount, string Description)> invoices,
        Dictionary<string, (decimal Amount, string Description)> deposits)
    {
        var results = new List<ReconciliationItem>();
        var matchedRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (refId, inv) in invoices)
        {
            if (deposits.TryGetValue(refId, out var dep))
            {
                matchedRefs.Add(refId);
                var kind = inv.Amount == dep.Amount
                    ? ReconciliationMatchKind.MatchedExact
                    : ReconciliationMatchKind.AmountDiscrepancy;
                results.Add(new ReconciliationItem(refId, kind, inv.Amount, dep.Amount, inv.Description));
            }
            else
            {
                results.Add(new ReconciliationItem(refId, ReconciliationMatchKind.UnpaidInvoice, inv.Amount, null, inv.Description));
            }
        }

        foreach (var (refId, dep) in deposits)
        {
            if (!matchedRefs.Contains(refId))
            {
                results.Add(new ReconciliationItem(refId, ReconciliationMatchKind.UnmatchedBankDeposit, null, dep.Amount, dep.Description));
            }
        }

        return results;
    }
}
