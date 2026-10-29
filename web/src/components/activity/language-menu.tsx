import { Globe02Icon } from "@hugeicons/core-free-icons";
import { useTranslation } from "react-i18next";
import { SidebarSettingButton } from "@/components/shell/sidebar-setting-button";
import { setAppLanguage } from "@/i18n/i18n";
import { normalizeLanguage, type SupportedLanguage } from "@/i18n/language";
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
        <SidebarSettingButton
          icon={Globe02Icon}
          label={t("language.label")}
          value={t(
            languageOptions.find((option) => option.value === language)?.labelKey ??
              "language.english",
          )}
        />
      </DropdownMenuTrigger>
      <DropdownMenuContent side="right" align="end" sideOffset={12} className="min-w-40">
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
