import { Folder01Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useTranslation } from "react-i18next";
import { RenderVideoPreview } from "@/components/render/render-video-preview";
import type { useRenderControl } from "@/hooks/render/use-render-control";
import { describeFolder } from "@/models/render/render-choices";

type Control = ReturnType<typeof useRenderControl>;

/** The finished video: watch it here, and see where it was saved. */
export function RenderResult({ control, openFailed }: { control: Control; openFailed: boolean }) {
  const { t } = useTranslation("render");
  const job = control.job;
  if (!job) return null;
  const saved = job.localOutputPath || job.outputFile;
  const file = saved ? describeFolder(saved) : null;
  return (
    <div className="space-y-4">
      <RenderVideoPreview key={job.jobId} jobId={job.jobId} readVideo={control.readRenderVideo} />
      {file ? (
        <p className="flex min-w-0 items-center gap-2 text-xs text-muted-foreground">
          <HugeiconsIcon aria-hidden="true" icon={Folder01Icon} size={14} className="shrink-0" />
          <span className="max-w-[60%] shrink-0 truncate text-foreground">{file.name}</span>
          <span className="min-w-0 truncate">{file.parent}</span>
        </p>
      ) : null}
      {openFailed ? (
        <p role="status" className="text-xs leading-relaxed text-muted-foreground">
          {t("result.openFailed")}
        </p>
      ) : null}
    </div>
  );
}
