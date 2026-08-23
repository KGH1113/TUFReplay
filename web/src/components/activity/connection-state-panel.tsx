import type { IpcVersionMismatchDirection } from "@adofai-ipc/client";
import { useConnectionStatePanelViewModel } from "@/hooks/activity/use-connection-state-panel";
import type { ConnectionStatus } from "@/models/activity/activity-model";
import { Button } from "@/shared/ui/button";

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
    <div className="grid flex-1 place-items-center px-6 py-10">
      <section className="w-full max-w-md" aria-live="polite">
        <div className="flex items-start gap-3.5">
          <span className="relative mt-1 grid size-8 shrink-0 place-items-center">
            <span className="absolute size-8 rounded-full border border-primary/20" />
            <span
              className={`size-2 rounded-full bg-primary ${viewModel.connecting ? "animate-pulse" : ""}`}
            />
          </span>
          <div className="min-w-0 flex-1">
            <h2 className="font-heading text-lg font-semibold tracking-tight">{viewModel.title}</h2>
            <p className="mt-1 text-sm leading-relaxed text-muted-foreground">
              {viewModel.description}
            </p>
            {!viewModel.connecting ? (
              viewModel.primaryAction.kind === "link" ? (
                <Button className="mt-5" asChild>
                  <a href={viewModel.primaryAction.href} target="_blank" rel="noreferrer">
                    {viewModel.primaryAction.label}
                  </a>
                </Button>
              ) : (
                <Button className="mt-5" onClick={viewModel.primaryAction.onPress}>
                  {viewModel.primaryAction.label}
                </Button>
              )
            ) : null}
          </div>
        </div>

        <div className="mt-6 border-t border-border/70 pt-3 text-sm">
          <details className="group py-1.5">
            <summary className="cursor-pointer select-none text-muted-foreground outline-none transition-colors hover:text-foreground focus-visible:text-foreground">
              {viewModel.guideLabel}
            </summary>
            <ol className="mt-2 space-y-1.5 pl-5 text-xs leading-relaxed text-muted-foreground">
              {viewModel.steps.map((step) => (
                <li key={step}>{step}</li>
              ))}
            </ol>
          </details>
          <details className="group py-1.5">
            <summary className="cursor-pointer select-none text-muted-foreground outline-none transition-colors hover:text-foreground focus-visible:text-foreground">
              {viewModel.diagnosticLabel}
            </summary>
            <p className="mt-2 break-words rounded-md bg-muted/35 px-3 py-2 font-mono text-[11px] leading-relaxed text-muted-foreground">
              {viewModel.diagnostic}
            </p>
          </details>
        </div>
      </section>
    </div>
  );
}
