import { useTranslation } from "react-i18next";
import noFailIcon from "@/shared/assets/activity/no-fail.png";

export function RunNoFailIcon({ enabled }: { enabled?: boolean }) {
  const { t } = useTranslation("activity");
  if (!enabled) return null;
  const label = t("run.noFail");

  return (
    <span className="grid size-6 shrink-0 place-items-center">
      <img src={noFailIcon} alt={label} title={label} className="block size-5 object-contain" />
    </span>
  );
}
