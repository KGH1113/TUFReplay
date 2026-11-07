import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import type { RenderJob } from "@/models/render/render-model";
import { cn } from "@/shared/lib/cn";
import { DetailsToggle } from "@/shared/ui/details-toggle";

/** A calm note about the render: what happened and what to do next. */
export function RenderNotice({
  title,
  message,
  children,
}: {
  title?: string;
  message: string;
  children?: ReactNode;
}) {
  return (
    <div role="status" className="space-y-1 rounded-lg bg-foreground/[0.05] px-3.5 py-3 text-sm">
      {title ? <p className="font-semibold">{title}</p> : null}
      <p className={cn("leading-relaxed", title && "text-muted-foreground")}>{message}</p>
      {children}
    </div>
  );
}

/**
 * Why the last render failed, in plain words. The renderer's own message, the
 * affected setting and the recording line stay one step away under Details.
 */
export function RenderFailureNotice({
  code,
  message,
  details,
}: {
  code: string | null;
  message: string;
  details: RenderJob["errorDetails"];
}) {
  const { t, i18n } = useTranslation("render");
  const known =
    code && i18n.exists(`errors.${code}`, { ns: "render" }) ? t(`errors.${code}` as never) : null;
  const field = details?.field?.replace(/^replay\./, "");
  const fieldLabel =
    field && i18n.exists(`errorFields.${field}`, { ns: "render" })
      ? t(`errorFields.${field}` as never)
      : field;
  const technical = [
    known && message && message !== known ? message : null,
    fieldLabel ? t("errorDetailField", { field: fieldLabel }) : null,
    details?.line
      ? t("errorDetailLine", { line: details.line, file: details.file || t("recordingFile") })
      : null,
  ].filter((line): line is string => Boolean(line));

  return (
    <RenderNotice title={t("notice.failedTitle")} message={known || message || t("errors.failed")}>
      {technical.length > 0 ? (
        <DetailsToggle label={t("notice.details")} className="pt-1">
          <div className="space-y-1">
            {technical.map((line) => (
              <p key={line} className="break-words text-xs leading-relaxed text-muted-foreground">
                {line}
              </p>
            ))}
          </div>
        </DetailsToggle>
      ) : null}
    </RenderNotice>
  );
}
