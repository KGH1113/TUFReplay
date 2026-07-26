import { Button } from "@/ui/button.component";
import type { ConnectionStatus } from "../activity.model";
import { getConnectionStatePanelCopy } from "./connection-state-panel.copy";

export function ConnectionStatePanel({
  status,
  error,
  onRetry,
}: {
  status: ConnectionStatus;
  error: string;
  onRetry: () => void;
}) {
  const connecting = status === "connecting";
  const copy = getConnectionStatePanelCopy(status);
  return (
    <div className="grid flex-1 place-items-center px-6 py-10">
      <section className="w-full max-w-md" aria-live="polite">
        <div className="flex items-start gap-3.5">
          <span className="relative mt-1 grid size-8 shrink-0 place-items-center">
            <span className="absolute size-8 rounded-full border border-primary/20" />
            <span
              className={`size-2 rounded-full bg-primary ${connecting ? "animate-pulse" : ""}`}
            />
          </span>
          <div className="min-w-0 flex-1">
            <h2 className="font-heading text-lg font-semibold tracking-tight">{copy.title}</h2>
            <p className="mt-1 text-sm leading-relaxed text-muted-foreground">{copy.description}</p>
            {!connecting ? (
              <Button className="mt-5" onClick={onRetry}>
                {copy.retryLabel}
              </Button>
            ) : null}
          </div>
        </div>

        <div className="mt-6 border-t border-border/70 pt-3 text-sm">
          <details className="group py-1.5">
            <summary className="cursor-pointer select-none text-muted-foreground outline-none transition-colors hover:text-foreground focus-visible:text-foreground">
              {copy.guideLabel}
            </summary>
            <ol className="mt-2 space-y-1.5 pl-5 text-xs leading-relaxed text-muted-foreground">
              {copy.steps.map((step) => (
                <li key={step}>{step}</li>
              ))}
            </ol>
          </details>
          <details className="group py-1.5">
            <summary className="cursor-pointer select-none text-muted-foreground outline-none transition-colors hover:text-foreground focus-visible:text-foreground">
              Details
            </summary>
            <p className="mt-2 break-words rounded-md bg-muted/35 px-3 py-2 font-mono text-[11px] leading-relaxed text-muted-foreground">
              {error || "The local IPC endpoint did not respond."}
            </p>
          </details>
        </div>
      </section>
    </div>
  );
}
