import { type ReactNode, useEffect, useId, useRef, useState } from "react";
import { cn } from "@/shared/lib/cn";
import { Slider } from "@/shared/ui/slider";
import { Switch } from "@/shared/ui/switch";
import { ToggleGroup, ToggleGroupItem } from "@/shared/ui/toggle-group";

/** A named group of settings, with an optional note on the right of its name. */
export function OptionGroup({
  label,
  aside,
  children,
}: {
  label: string;
  aside?: ReactNode;
  children: ReactNode;
}) {
  return (
    <section className="space-y-2">
      <div className="flex min-h-5 items-baseline justify-between gap-3">
        <h3 className="text-sm font-medium">{label}</h3>
        {aside}
      </div>
      {children}
    </section>
  );
}

/**
 * One choice from a few well-known values, as large cards: the value people
 * know in big type, a plain word for it underneath. Arrow keys move between them.
 */
export function ChoiceCards<T extends string>({
  label,
  choices,
  value,
  onChange,
  className,
}: {
  label: string;
  choices: { value: T; title: string; detail?: string }[];
  /** Null when none matches, such as a custom video size. */
  value: T | null;
  onChange: (value: T) => void;
  className?: string;
}) {
  return (
    <ToggleGroup
      type="single"
      aria-label={label}
      value={value ?? ""}
      onValueChange={(next) => {
        if (next) onChange(next as T);
      }}
      className={cn("grid w-full items-stretch gap-2", className)}
    >
      {choices.map((choice) => (
        <ToggleGroupItem
          key={choice.value}
          value={choice.value}
          className={cn(
            "h-auto min-w-0 flex-col items-start justify-start gap-0 rounded-xl border px-3 py-2.5 text-left font-normal whitespace-normal text-foreground transition-[background-color,border-color,transform] active:scale-[0.98]",
            "border-border bg-foreground/[0.03] hover:bg-foreground/[0.06]",
            "data-[state=on]:border-primary/60 data-[state=on]:bg-primary/10 data-[state=on]:ring-1 data-[state=on]:ring-primary/40 data-[state=on]:hover:bg-primary/12",
          )}
        >
          <span className="text-base font-semibold tabular-nums">{choice.title}</span>
          {choice.detail ? (
            <span className="text-xs leading-snug text-muted-foreground">{choice.detail}</span>
          ) : null}
        </ToggleGroupItem>
      ))}
    </ToggleGroup>
  );
}

/**
 * One setting on one line: what it is on the left, how to change it on the
 * right. A note, if any, runs underneath at full width so it is never cut off.
 */
export function SettingRow({
  label,
  hint,
  labelId,
  children,
}: {
  label: ReactNode;
  hint?: string;
  labelId?: string;
  children: ReactNode;
}) {
  return (
    <div className="py-1.5">
      <div className="flex min-h-9 items-center justify-between gap-4">
        <p id={labelId} className="shrink-0 text-sm whitespace-nowrap">
          {label}
        </p>
        <div className="flex min-w-0 flex-1 items-center justify-end gap-3">{children}</div>
      </div>
      {hint ? <p className="pb-1 text-xs leading-relaxed text-muted-foreground">{hint}</p> : null}
    </div>
  );
}

/** An on/off setting on one line. */
export function SwitchSetting({
  label,
  checked,
  onChange,
}: {
  label: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
}) {
  const id = useId();
  return (
    <div className="flex min-h-12 items-center justify-between gap-4 py-1.5">
      <label htmlFor={id} className="text-sm">
        {label}
      </label>
      <Switch id={id} checked={checked} onCheckedChange={onChange} />
    </div>
  );
}

/** A few small choices joined into one control. Arrow keys move between them. */
export function Segmented<T extends string>({
  label,
  choices,
  value,
  onChange,
  disabled,
  stretch,
}: {
  label: string;
  choices: { value: T; title: string }[];
  value: T | null;
  onChange: (value: T) => void;
  disabled?: boolean;
  /** Fill the width, each choice taking an equal share. */
  stretch?: boolean;
}) {
  return (
    <ToggleGroup
      type="single"
      variant="outline"
      size="sm"
      spacing={0}
      aria-label={label}
      value={value ?? ""}
      disabled={disabled}
      onValueChange={(next) => {
        if (next) onChange(next as T);
      }}
      className={stretch ? "w-full" : undefined}
    >
      {choices.map((choice) => (
        <ToggleGroupItem
          key={choice.value}
          value={choice.value}
          className={cn(
            "px-3 text-xs font-medium text-muted-foreground tabular-nums data-[state=on]:bg-primary/15 data-[state=on]:text-foreground",
            stretch && "flex-1",
          )}
        >
          {choice.title}
        </ToggleGroupItem>
      ))}
    </ToggleGroup>
  );
}

/** A slider setting on one line, its value shown as it moves. */
export function SliderSetting({
  label,
  hint,
  value,
  min,
  max,
  step,
  format,
  disabled,
  onChange,
}: {
  label: string;
  hint?: string;
  value: number;
  min: number;
  max: number;
  step: number;
  format: (value: number) => string;
  disabled?: boolean;
  onChange: (value: number) => void;
}) {
  const [draft, setDraft] = useState(value);
  const dragging = useRef(false);
  useEffect(() => {
    if (!dragging.current) setDraft(value);
  }, [value]);
  return (
    <SettingRow label={label} hint={hint}>
      <Slider
        aria-label={label}
        aria-valuetext={format(draft)}
        min={min}
        max={max}
        step={step}
        value={[Math.min(max, Math.max(min, draft))]}
        disabled={disabled}
        className="min-w-20 max-w-56 flex-1"
        onValueChange={([next]) => {
          dragging.current = true;
          setDraft(next);
        }}
        onValueCommit={([next]) => {
          dragging.current = false;
          onChange(next);
        }}
      />
      <span className="w-24 shrink-0 text-right text-xs tabular-nums text-muted-foreground">
        {format(draft)}
      </span>
    </SettingRow>
  );
}
