import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import type { TransferredFile } from "@/ports/received-file";
import { Button } from "@/shared/ui/button";

export function RenderVideoPreview({
  jobId,
  readVideo,
}: {
  jobId: string;
  readVideo: (jobId: string) => Promise<TransferredFile>;
}) {
  const { t } = useTranslation("render");
  const [url, setUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [attempt, setAttempt] = useState(0);

  // biome-ignore lint/correctness/useExhaustiveDependencies: A retry starts a fresh transfer.
  useEffect(() => {
    const abort = new AbortController();
    let objectUrl: string | null = null;
    setLoading(true);
    setError(false);
    setUrl(null);
    void (async () => {
      try {
        const file = await readVideo(jobId);
        if (abort.signal.aborted) return;
        if (!Number.isSafeInteger(file.size) || file.size < 0)
          throw new Error("Invalid video size.");
        const blob = await new Response(file.stream({ signal: abort.signal }), {
          headers: { "Content-Type": file.type || "application/octet-stream" },
        }).blob();
        if (abort.signal.aborted) return;
        if (blob.size !== file.size) throw new Error("Incomplete video transfer.");
        objectUrl = URL.createObjectURL(blob);
        setUrl(objectUrl);
      } catch {
        if (!abort.signal.aborted) setError(true);
      } finally {
        if (!abort.signal.aborted) setLoading(false);
      }
    })();
    return () => {
      abort.abort();
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [jobId, readVideo, attempt]);

  return (
    <div className="space-y-2">
      {url ? (
        // biome-ignore lint/a11y/useMediaCaption: Rendered gameplay videos do not have caption tracks.
        <video
          key={url}
          controls
          playsInline
          className="aspect-video w-full bg-black"
          src={url}
          onError={() => setError(true)}
        />
      ) : loading ? (
        <p role="status" className="text-sm text-muted-foreground">
          {t("videoPreviewLoading")}
        </p>
      ) : null}
      {loading ? <p className="text-xs text-muted-foreground">{t("videoPreviewHelp")}</p> : null}
      {error ? (
        <p role="alert" className="text-xs text-destructive">
          {t("videoPreviewError")}
        </p>
      ) : null}
      {error ? (
        <Button type="button" variant="outline" onClick={() => setAttempt((value) => value + 1)}>
          {t("retry")}
        </Button>
      ) : null}
    </div>
  );
}
