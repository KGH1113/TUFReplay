import { useConnectionStatePanelViewModel } from "@/hooks/activity/use-connection-state-panel";
import type { ConnectionStatus } from "@/models/activity/activity-model";
import type { IpcVersionMismatchDirection } from "@/ports/local-message-peer";
import { cn } from "@/shared/lib/cn";
import { Button } from "@/shared/ui/button";

/** What is wrong with the game connection, and the steps that fix it, in that order. */
export function ConnectionStatePanel({
  status,
  error,
  onRetry,
  versionMismatch = null,
}: {
  status: ConnectionStatus;
  error: string;
  onRetry: () => void;
  versionMismatch?: IpcVersionMismatchDirection | null;
}) {
  const viewModel = useConnectionStatePanelViewModel({
    status,
    error,
    onRetry,
    versionMismatch,
  });
  return (
    <div className="grid min-h-0 flex-1 place-items-center overflow-y-auto px-6 py-10">
      <section className="w-full max-w-md" aria-live="polite">
        <div className="flex items-start gap-4">
          <span className="relative mt-0.5 grid size-10 shrink-0 place-items-center">
            <span
              aria-hidden="true"
              className={cn(
                "absolute inset-0 rounded-full",
                viewModel.connecting
                  ? "bg-primary/10 motion-safe:animate-ping motion-safe:[animation-duration:2s]"
                  : "bg-warning/10",
              )}
            />
            <span
              aria-hidden="true"
              className={cn(
                "relative size-2.5 rounded-full",
                viewModel.connecting ? "bg-primary" : "bg-warning",
              )}
            />
          </span>
          <div className="min-w-0 flex-1">
            <h2 className="text-xl font-semibold tracking-tight text-balance">{viewModel.title}</h2>
            <p className="mt-1 text-sm leading-relaxed text-muted-foreground">
              {viewModel.description}
            </p>
          </div>
        </div>

        <div className="mt-6 rounded-2xl border border-border bg-surface p-4">
          <p className="text-xs font-semibold text-muted-foreground">{viewModel.guideLabel}</p>
          <ol className="mt-3 grid gap-2.5">
            {viewModel.steps.map((step, index) => (
              <li key={step} className="grid grid-cols-[1.5rem_minmax(0,1fr)] gap-2.5 text-sm">
                <span
                  aria-hidden="true"
                  className="grid size-6 place-items-center rounded-full bg-foreground/[0.07] text-xs font-semibold tabular-nums text-muted-foreground"
                >
                  {index + 1}
                </span>
                <span className="pt-0.5 leading-relaxed">{step}</span>
              </li>
            ))}
          </ol>
        </div>

        {!viewModel.connecting ? (
          <Button className="mt-5" onClick={viewModel.primaryAction.onPress}>
            {viewModel.primaryAction.label}
          </Button>
        ) : null}

        <details className="group mt-6 text-sm">
          <summary className="cursor-pointer text-xs text-subtle-foreground outline-none transition-colors select-none hover:text-foreground focus-visible:text-foreground">
            {viewModel.diagnosticLabel}
          </summary>
          <p className="mt-2 break-words rounded-lg bg-foreground/[0.04] px-3 py-2 font-mono text-[11px] leading-relaxed text-muted-foreground">
            {viewModel.diagnostic}
          </p>
        </details>
      </section>
    </div>
  );
}
