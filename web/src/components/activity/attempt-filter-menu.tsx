import { FilterIcon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import type { JudgmentDifficulty } from "@/models/activity/activity-model";
import type { AttemptFilters } from "@/models/activity/attempt-filters";
import { cn } from "@/shared/lib/cn";
import { Button } from "@/shared/ui/button";
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/shared/ui/dropdown-menu";

/** Narrows the attempts list; the menu stays open so several filters can be combined. */
export function AttemptFilterMenu({
  filters,
  options,
  activeCount,
  onChange,
  onClear,
}: {
  filters: AttemptFilters;
  options: { pitches: number[]; difficulties: JudgmentDifficulty[] };
  activeCount: number;
  onChange: (filters: AttemptFilters) => void;
  onClear: () => void;
}) {
  const { t } = useTranslation("activity");
  const toggle = <Key extends "clearsOnly" | "lockedOnly" | "withRecordings" | "hideNoFail">(
    key: Key,
  ) => onChange({ ...filters, [key]: !filters[key] });
  const toggleValue = <Value,>(values: Value[], value: Value) =>
    values.includes(value) ? values.filter((item) => item !== value) : [...values, value];

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          type="button"
          size="sm"
          variant="ghost"
          className={cn("shrink-0 text-muted-foreground", activeCount > 0 && "text-foreground")}
        >
          <span className="relative grid place-items-center">
            <HugeiconsIcon aria-hidden="true" icon={FilterIcon} className="size-4" />
            {activeCount > 0 ? (
              <span className="absolute -top-1.5 -right-1.5 grid size-3.5 place-items-center rounded-full bg-primary text-[9px] font-bold tabular-nums text-primary-foreground">
                {activeCount}
              </span>
            ) : null}
          </span>
          {t("filter.button")}
          {activeCount > 0 ? (
            <span className="sr-only">{t("filter.activeCount", { count: activeCount })}</span>
          ) : null}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-60">
        <FilterItem checked={filters.clearsOnly} onToggle={() => toggle("clearsOnly")}>
          {t("filter.clearsOnly")}
        </FilterItem>
        <FilterItem checked={filters.lockedOnly} onToggle={() => toggle("lockedOnly")}>
          {t("filter.lockedOnly")}
        </FilterItem>
        <FilterItem checked={filters.withRecordings} onToggle={() => toggle("withRecordings")}>
          {t("filter.withRecordings")}
        </FilterItem>
        <FilterItem checked={filters.hideNoFail} onToggle={() => toggle("hideNoFail")}>
          {t("filter.hideNoFail")}
        </FilterItem>
        {options.pitches.length > 0 ? (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuLabel>{t("sort.pitch")}</DropdownMenuLabel>
            {options.pitches.map((pitch) => (
              <FilterItem
                key={pitch}
                checked={filters.pitches.includes(pitch)}
                onToggle={() =>
                  onChange({ ...filters, pitches: toggleValue(filters.pitches, pitch) })
                }
              >
                {pitch}%
              </FilterItem>
            ))}
          </>
        ) : null}
        {options.difficulties.length > 0 ? (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuLabel>{t("filter.difficulty")}</DropdownMenuLabel>
            {options.difficulties.map((difficulty) => (
              <FilterItem
                key={difficulty}
                checked={filters.difficulties.includes(difficulty)}
                onToggle={() =>
                  onChange({
                    ...filters,
                    difficulties: toggleValue(filters.difficulties, difficulty),
                  })
                }
              >
                {t(`run.difficultyName.${difficulty}`)}
              </FilterItem>
            ))}
          </>
        ) : null}
        <DropdownMenuSeparator />
        <DropdownMenuItem disabled={activeCount === 0} onSelect={onClear}>
          {t("filter.clear")}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function FilterItem({
  checked,
  onToggle,
  children,
}: {
  checked: boolean;
  onToggle: () => void;
  children: ReactNode;
}) {
  return (
    <DropdownMenuCheckboxItem
      checked={checked}
      onCheckedChange={onToggle}
      onSelect={(event) => event.preventDefault()}
    >
      {children}
    </DropdownMenuCheckboxItem>
  );
}
