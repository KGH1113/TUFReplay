import { Badge } from "@/ui/badge.component";

export function DashboardHeader() {
  return (
    <header className="flex min-h-14 items-center justify-between gap-3 border-b border-border bg-background px-4 py-2">
      <h1 className="font-heading text-xl font-semibold">TUFReplay</h1>
      <div className="flex items-center gap-2">
        <Badge variant="outline" className="border-[#7DCF00] text-[#7DCF00]">
          online
        </Badge>
      </div>
    </header>
  );
}

