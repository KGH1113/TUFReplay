export type ApiErrorKind = "connection" | "protocol" | "domain" | "validation" | "http" | "unknown";

export interface ApiErrorOptions {
  kind: ApiErrorKind;
  code?: string;
  cause?: unknown;
}

export class ApiError extends Error {
  readonly kind: ApiErrorKind;
  readonly code: string | null;
  override readonly cause: unknown;

  constructor(message: string, options: ApiErrorOptions) {
    super(message);
    this.name = "ApiError";
    this.kind = options.kind;
    this.code = options.code ?? null;
    this.cause = options.cause;
  }
}
