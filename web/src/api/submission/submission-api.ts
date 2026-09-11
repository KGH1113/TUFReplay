import type {
  SubmissionPage,
  SubmissionRun,
  SubmissionStatus,
} from "@/models/submission/submission-model";

export interface SubmissionApi {
  connect(): Promise<SubmissionStatus>;
  disconnect(): Promise<SubmissionStatus>;
  status(): Promise<SubmissionStatus>;
  setDisabled(disabled: boolean): Promise<SubmissionStatus>;
  list(before?: number): Promise<SubmissionPage>;
  get(id: string): Promise<SubmissionRun>;
  submit(id: string): Promise<SubmissionRun>;
  remove(id: string): Promise<void>;
}
