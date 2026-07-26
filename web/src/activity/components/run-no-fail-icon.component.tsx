import noFailIcon from "../assets/no-fail.png";

export function RunNoFailIcon({ enabled }: { enabled?: boolean }) {
  if (!enabled) return null;

  return (
    <span className="grid size-6 shrink-0 place-items-center">
      <img
        src={noFailIcon}
        alt="No Fail mode"
        title="No Fail mode"
        className="block size-5 object-contain"
      />
    </span>
  );
}
