import noFailIcon from "../assets/no-fail.png";

export function RunNoFailIcon({ enabled }: { enabled?: boolean }) {
  if (!enabled) return null;

  return (
    <img
      src={noFailIcon}
      alt="No Fail mode"
      title="No Fail mode"
      className="size-[18px] shrink-0 object-contain"
    />
  );
}
