import { Button } from "@/ui/button.component";
import type { ConnectionStatus } from "../activity.model";

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
            <h2 className="font-heading text-lg font-semibold tracking-tight">
              {connecting ? "Connecting to TUFReplay…" : "Waiting for TUFReplay"}
            </h2>
            <p className="mt-1 text-sm leading-relaxed text-muted-foreground">
              {connecting
                ? "Checking the local ADOFAI connection."
                : "Open ADOFAI and make sure the mod is enabled."}
            </p>
            {!connecting ? (
              <Button className="mt-5" onClick={onRetry}>
                Retry connection
              </Button>
            ) : null}
          </div>
        </div>

        <div className="mt-6 border-t border-border/70 pt-3 text-sm">
          <details className="group py-1.5">
            <summary className="cursor-pointer select-none text-muted-foreground outline-none transition-colors hover:text-foreground focus-visible:text-foreground">
              How to connect
            </summary>
            <ol className="mt-2 space-y-1.5 pl-5 text-xs leading-relaxed text-muted-foreground">
              <li>Start ADOFAI.</li>
              <li>Enable TUFReplay in UnityModManager.</li>
              <li>Make sure AdofaiIpc is running.</li>
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
