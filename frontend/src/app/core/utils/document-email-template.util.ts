export function buildDefaultDocumentEmailTemplate(documentLabel: string): string {
  return [
    'Dear Sir/Madam,',
    '',
    `Kindly find the attached ${documentLabel} for your reference from {{companyName}}.`,
    '',
    'Thanks and regards,',
    '{{companyName}},'
  ].join('\n');
}

export function applyCompanyNameToEmailTemplate(template: string, companyName: string): string {
  const resolvedCompanyName = companyName.trim() || 'Your Company';

  return template
    .replace(/\{\{\s*companyName\s*\}\}/gi, resolvedCompanyName)
    .replace(/\[\s*user company name\s*\]/gi, resolvedCompanyName)
    .replace(/\[\s*user company\s*\]/gi, resolvedCompanyName);
}

function normalizeDocumentEmailTemplate(template: string, documentLabel: string): string {
  const normalizedTemplate = normalizeSignoff(
    template
      .replace(/\r\n/g, '\n')
      .replace(/\r/g, '\n')
      .trim()
  );

  const legacyDefaultTemplate = [
    'Dear Sir/Madam,',
    '',
    `Kindly find the attached ${documentLabel} for your reference from {{companyName}}`,
    '',
    'Thanks and regards,',
    '{{companyName}}.'
  ].join('\n');

  return normalizedTemplate === legacyDefaultTemplate
    ? buildDefaultDocumentEmailTemplate(documentLabel)
    : normalizedTemplate;
}

function normalizeSignoff(template: string): string {
  return template
    .replace(/(^|\n)(Thanks and regards,)\s+([^\n]+)(?=\n|$)/gi, '$1$2\n$3')
    .replace(/(^|\n)(Thanks & regards,)\s+([^\n]+)(?=\n|$)/gi, '$1$2\n$3');
}

export function resolveDocumentEmailBody(template: string | null | undefined, documentLabel: string, companyName: string): string {
  const sourceTemplate = template?.trim()
    ? normalizeDocumentEmailTemplate(template, documentLabel)
    : buildDefaultDocumentEmailTemplate(documentLabel);
  return applyCompanyNameToEmailTemplate(sourceTemplate, companyName);
}
