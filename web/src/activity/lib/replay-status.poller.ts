import type { ReplayStatus } from "../activity.model";

export class ReplayStatusPoller {
  private generation = 0;
  private inFlight = false;
  private disposed = false;

  constructor(
    private readonly load: () => Promise<ReplayStatus>,
    private readonly onStatus: (status: ReplayStatus) => void,
    private readonly onError: (cause: unknown) => void,
  ) {}

  async refresh() {
    if (this.disposed || this.inFlight) return;
    const generation = this.generation;
    this.inFlight = true;
    try {
      const status = await this.load();
      if (!this.disposed && generation === this.generation) this.onStatus(status);
    } catch (cause) {
      if (!this.disposed && generation === this.generation) this.onError(cause);
    } finally {
      this.inFlight = false;
    }
  }

  invalidate() {
    this.generation += 1;
  }

  dispose() {
    this.disposed = true;
    this.invalidate();
  }
}
