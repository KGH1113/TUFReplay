import { Globe02Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useTranslation } from "react-i18next";
import { setAppLanguage } from "@/i18n/i18n";
import { normalizeLanguage, type SupportedLanguage } from "@/i18n/language";
import { Button } from "@/shared/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from "@/shared/ui/dropdown-menu";

const languageOptions: {
  value: SupportedLanguage;
  labelKey: "language.korean" | "language.english";
}[] = [
  { value: "ko", labelKey: "language.korean" },
  { value: "en", labelKey: "language.english" },
];

export function LanguageMenu() {
  const { t, i18n } = useTranslation("common");
  const language = normalizeLanguage(i18n.resolvedLanguage);

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          type="button"
          variant="ghost"
          size="icon-sm"
          aria-label={t("language.label")}
          title={t("language.label")}
          className="rounded-full text-muted-foreground hover:text-foreground data-[state=open]:bg-muted/60 data-[state=open]:text-foreground"
        >
          <HugeiconsIcon aria-hidden="true" icon={Globe02Icon} size={17} strokeWidth={2} />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="min-w-36">
        <DropdownMenuRadioGroup
          value={language}
          onValueChange={(value) => void setAppLanguage(normalizeLanguage(value))}
        >
          {languageOptions.map((option) => (
            <DropdownMenuRadioItem key={option.value} value={option.value}>
              {t(option.labelKey)}
            </DropdownMenuRadioItem>
          ))}
        </DropdownMenuRadioGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
