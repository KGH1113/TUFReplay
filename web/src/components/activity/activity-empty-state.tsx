import { Menu01Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/shared/ui/button";
import { Empty, EmptyDescription, EmptyHeader, EmptyTitle } from "@/shared/ui/empty";

/** Full-height message for the main area, with the drawer toggle kept reachable on narrow windows. */
export function MainAreaState({
  onOpenSidebar,
  children,
}: {
  onOpenSidebar: () => void;
  children: ReactNode;
}) {
  const { t } = useTranslation("activity");
  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <div className="flex h-12 shrink-0 items-center px-3 lg:hidden">
        <Button
          type="button"
          variant="ghost"
          size="icon-sm"
          aria-label={t("sidebar.open")}
          onClick={onOpenSidebar}
        >
          <HugeiconsIcon aria-hidden="true" icon={Menu01Icon} className="size-5" />
        </Button>
      </div>
      {children}
    </div>
  );
}

export function ActivityEmptyState({ title, body }: { title: string; body: string }) {
  return (
    <div className="grid min-h-0 flex-1 place-items-center px-6">
      <Empty>
        <EmptyHeader>
          <EmptyTitle>{title}</EmptyTitle>
          <EmptyDescription className="max-w-sm">{body}</EmptyDescription>
        </EmptyHeader>
      </Empty>
    </div>
  );
}
