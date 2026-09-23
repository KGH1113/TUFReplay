export interface Tester {
  userId: string;
  label: string;
  active: boolean;
  updatedAt: string;
  updatedBy: string;
}

export interface TesterEvent {
  id: string;
  userId: string;
  action: "grant" | "revoke";
  actor: string;
  reason: string;
  createdAt: string;
}

export interface TesterRepository {
  list(): Promise<{ testers: Tester[]; events: TesterEvent[] }>;
  grant(userId: string, label: string, actor: string, reason: string): Promise<Tester>;
  revoke(userId: string, actor: string, reason: string): Promise<Tester | null>;
}

const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function parseUserId(value: unknown): string {
  if (typeof value !== "string" || !uuid.test(value.trim())) {
    throw new InputError("TUF 계정 UUID를 확인해 주세요.");
  }
  return value.trim().toLowerCase();
}

export function parseText(value: unknown, name: string, limit: number, required: boolean): string {
  if (typeof value !== "string" || value.trim().length > limit || (required && !value.trim())) {
    throw new InputError(`${name}을(를) 확인해 주세요.`);
  }
  return value.trim();
}

export class InputError extends Error {}
