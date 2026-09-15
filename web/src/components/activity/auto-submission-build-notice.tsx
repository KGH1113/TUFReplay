import { useTranslation } from "react-i18next";
import { getAutoSubmissionCompatibility, type Health } from "@/models/health/health-model";
import { TUFREPLAY_WEB_BUILD } from "@/shared/config/tufreplay-build-info";

export function AutoSubmissionBuildNotice({ health }: { health: Health | null }) {
  const { t } = useTranslation("activity");
  const compatibility = getAutoSubmissionCompatibility(TUFREPLAY_WEB_BUILD.flavor, health);

  if (
    compatibility.available ||
    (compatibility.reason !== "mod_build" && compatibility.reason !== "protocol_version")
  ) {
    return null;
  }

  const copyKey =
    compatibility.reason === "protocol_version"
      ? "autoSubmissionCompatibility.protocolMismatch"
      : "autoSubmissionCompatibility.modBuildMismatch";

  return (
    <aside
      role="status"
      aria-live="polite"
      className="rounded-xl border border-amber-400/25 bg-amber-400/8 px-3 py-2 text-xs leading-relaxed text-muted-foreground"
    >
      {t(copyKey)}
    </aside>
  );
}
