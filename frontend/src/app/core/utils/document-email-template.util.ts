export function buildDefaultDocumentEmailTemplate(documentLabel: string): string {
  return [
    'Dear Sir/Madam,',
    '',
    `Kindly find the attached ${documentLabel} for your reference from {{companyName}}`,
    '',
    'Thanks and regards,',
    '{{companyName}}.'
  ].join('\n');
}

export function applyCompanyNameToEmailTemplate(template: string, companyName: string): string {
  const resolvedCompanyName = companyName.trim() || 'Your Company';

  return template
    .replace(/\{\{\s*companyName\s*\}\}/gi, resolvedCompanyName)
    .replace(/\[User company name\]/g, resolvedCompanyName)
    .replace(/\[User Company\]/g, resolvedCompanyName);
}

export function resolveDocumentEmailBody(template: string | null | undefined, documentLabel: string, companyName: string): string {
  const sourceTemplate = template?.trim() || buildDefaultDocumentEmailTemplate(documentLabel);
  return applyCompanyNameToEmailTemplate(sourceTemplate, companyName);
}
