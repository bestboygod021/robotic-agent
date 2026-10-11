(function () {
  let lang = 'fa';
  const root = document.getElementById('extRoot');
  const input = document.getElementById('inputText');
  const status = document.getElementById('statusMsg');
  const scrubBtn = document.getElementById('scrubPiiBtn');
  const coachBtn = document.getElementById('coachPromptBtn');
  const toggleBtn = document.getElementById('toggleLangBtn');

  function redactSensitivePii(text) {
    let count = 0;
    let out = text.replace(/\b[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}\b/g, () => { count++; return '[REDACTED_EMAIL]'; });
    out = out.replace(/\b(?:IR\d{24}|[A-Z]{2}\d{2}[A-Z0-9]{11,30})\b/g, () => { count++; return '[REDACTED_IBAN]'; });
    out = out.replace(/\b(?:\d[ -]?){15}\d\b/g, () => { count++; return '[REDACTED_CARD]'; });
    out = out.replace(/(?:\+\d{1,3}[\s-]?|\b09\d{2}[\s-]?)\d{3}[\s-]?\d{3,4}\b/g, () => { count++; return '[REDACTED_PHONE]'; });
    return { text: out, count: count };
  }

  scrubBtn.addEventListener('click', () => {
    const res = redactSensitivePii(input.value || '');
    input.value = res.text;
    status.textContent = lang === 'fa'
      ? `✓ تعداد ${res.count} مورد دادهٔ حساس ماسک شد.`
      : `✓ Redacted ${res.count} sensitive PII item(s).`;
  });

  coachBtn.addEventListener('click', () => {
    const res = redactSensitivePii(input.value || '');
    input.value = lang === 'fa'
      ? `# نقش: متخصص باتجربه\n# هدف: ${res.text}\n# قالب خروجی: دقیق، گام‌به‌گام و ساختاریافته`
      : `# Role: Senior Domain Specialist\n# Objective: ${res.text}\n# Output Format: Clear, actionable, and step-by-step`;
    status.textContent = lang === 'fa'
      ? '✨ پرامپت مهندسی‌شده و امن آمادهٔ کپی است.'
      : '✨ Safe engineered prompt is ready to copy.';
  });

  toggleBtn.addEventListener('click', () => {
    lang = lang === 'fa' ? 'en' : 'fa';
    root.lang = lang;
    root.dir = lang === 'fa' ? 'rtl' : 'ltr';
    scrubBtn.textContent = lang === 'fa' ? '🛡️ سانسور اطلاعات حساس (PII)' : '🛡️ Scrub Sensitive PII';
    coachBtn.textContent = lang === 'fa' ? '✨ تبدیل به پرامپت حرفه‌ای' : '✨ Build Expert Prompt';
  });
})();
