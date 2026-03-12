type ApiErrorPayload = {
  message?: string;
  Message?: string;
  title?: string;
  detail?: string;
  errors?: string[] | Record<string, string[]>;
  Errors?: string[] | Record<string, string[]>;
};

export function extractApiError(error: unknown, fallback: string): string {
  const httpError = error as { error?: unknown; message?: string };
  const payload = toPayload(httpError?.error);

  if (!payload) {
    return httpError?.message || fallback;
  }

  const firstValidationError =
    firstError(payload.errors) ??
    firstError(payload.Errors);

  return firstValidationError
    ?? payload.message
    ?? payload.Message
    ?? payload.title
    ?? payload.detail
    ?? fallback;
}

function firstError(errors: ApiErrorPayload['errors']): string | undefined {
  if (!errors) {
    return undefined;
  }

  if (Array.isArray(errors)) {
    return errors[0];
  }

  return Object.values(errors).flat()[0];
}

function toPayload(raw: unknown): ApiErrorPayload | null {
  if (!raw) {
    return null;
  }

  if (typeof raw === 'string') {
    try {
      return JSON.parse(raw) as ApiErrorPayload;
    } catch {
      return { message: raw };
    }
  }

  if (typeof raw === 'object') {
    return raw as ApiErrorPayload;
  }

  return null;
}
